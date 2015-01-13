using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using Jinx.Lib;
using Jinx.Scheduler.Jobs;
using Microsoft.Practices.Unity;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Listener;
using Serilog;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;
using StackExchange.Redis;

namespace Jinx.Scheduler
{
    class Program
    {
        public static IUnityContainer Container;
        public static List<JinxSchedule> JobSchedule;
        public static List<JinxDataStore> DataStores;

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                //Sinks
                .WriteTo.ColoredConsole()
                .WriteTo.RollingFile(ConfigurationManager.AppSettings.Get("LogPath"), LogEventLevel.Information)
                //Enrichers
                .Enrich.WithProperty("AppName", "Jinx Scheduler")
                .Enrich.WithMachineName()
#if DEBUG
                .Enrich.WithProperty("Env", "dev")
#else
                .Enrich.WithProperty("Env", "prd")
#endif
                .CreateLogger();

        }
        private static void Main(string[] args)
        {
            ConfigureLogging();
            JsConfig.EmitCamelCaseNames = true;
            JsConfig.PropertyConvention = PropertyConvention.Lenient;
            JobSchedule = new List<JinxSchedule>();
            DataStores = new List<JinxDataStore>();
            Container = new UnityContainer();
            RegisterRedis(Container);

            TestStuff();
            ISchedulerFactory factory = new StdSchedulerFactory();

            IScheduler sched = factory.GetScheduler();

            var job = JobBuilder.Create<SchedulerJob>()
                .WithIdentity("Scheduler", "Jinx")
                .Build();

            var trig = TriggerBuilder.Create()
                .WithIdentity("SchedulerTrigger", "Jinx")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(1)
                    .RepeatForever())
                .Build();

            sched.ScheduleJob(job, trig);
            sched.ListenerManager.AddJobListener(new JobHistoryListener(), GroupMatcher<JobKey>.AnyGroup());
            sched.Start();
        }

        private static void TestStuff()
        {
            var redisDb = Container.Resolve<IDatabase>();
            redisDb.SetAdd("Jinx:DataStores:Ids", "Jinx:DataStores:GoogleCampaigns");

            var googleCampaignsFromSql = new JinxDataStore
            {
                BaseKey = "Jinx:DataStores:GoogleCampaigns",
                Enabled = true,
                Name = "Google Campaigns Transform"
            };

            var testingJob = new JinxJobInfo
            {
                CronExpression = "0/30 * * 1/1 * ? *",
                JobKeyGroup = "O&OSpend",
                JobKeyName = "O&OSpendExport",
                JobType = "SqlServerQuery",
                TriggerKeyGroup = "O&O",
                TriggerKeyName = "O&OSpendExportTrigger"
            };

            var sqlJob = new SqlServerQueryJinxJob
            {
                DatabaseConnectionKey = "PkcMobDb01-Email",
                Query = "select * from spend",
            };

            redisDb.StringSet("Jinx:DataStores:GoogleCampaigns:SqlServerQueryConfig", sqlJob.ToJson());
            redisDb.StringSet("Jinx:DataStores:GoogleCampaigns", googleCampaignsFromSql.ToJson());
            redisDb.HashSet(googleCampaignsFromSql.BaseKey + ":Jobs", "Jinx:DataStores:GoogleCampaigns:SqlServerQueryConfig", testingJob.ToJson());
        }

        private static void RegisterRedis(IUnityContainer container)
        {
            var redisServer = ConfigurationManager.AppSettings["RedisServer"];
            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisServer);

            container.RegisterInstance(connectionMultiplexer);
            container.RegisterInstance(typeof (IDatabase), container.Resolve<ConnectionMultiplexer>().GetDatabase(2));
        }
    }

    public class SchedulerJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var redisDb = Program.Container.Resolve<IDatabase>();
            var dataStoresKeys = redisDb.SetMembers("Jinx:DataStores:Ids");

            Program.DataStores = dataStoresKeys.Select(dsKey =>
                ((string) redisDb.StringGet((string) dsKey))
                    .FromJson<JinxDataStore>())
                .ToList();

            foreach (var store in Program.DataStores.Where(x => x.Enabled))
            {
                var jobsHash = redisDb.HashGetAll(store.BaseKey + ":Jobs")
                    .ToDictionary(x => (string) x.Name, x => ((string) x.Value).FromJson<JinxJobInfo>());


                foreach (var job in jobsHash.Select(x => Tuple.Create(x.Key, x.Value)))
                {
                    var triggerKey = new TriggerKey(job.Item2.TriggerKeyName, job.Item2.TriggerKeyGroup);
                    var trigger = context.Scheduler.GetTrigger(triggerKey) as ICronTrigger;

                    if (trigger == null)
                    {
                        //trigger is wrong or doesn't exits, create it
                        ScheduleJob(store, context.Scheduler, job.Item1,job.Item2);
                    }
                    else
                    {
                        if (trigger.CronExpressionString == job.Item2.CronExpression) 
                            continue;

                        //cron expression has changed, update it
                        context.Scheduler.UnscheduleJob(triggerKey);
                        ScheduleJob(store,context.Scheduler, job.Item1,job.Item2);
                    }
                }
            }
        }

        private void ScheduleJob(JinxDataStore store, IScheduler scheduler, string jobHashKey, JinxJobInfo job)
        {
            var jobKey = new JobKey(job.JobKeyName, job.JobKeyGroup);
            var triggerKey = new TriggerKey(job.TriggerKeyName, job.TriggerKeyGroup);
            IJobDetail newJob = null;
            if (job.JobType == "TestJob")
            {
                newJob = CreateTestJob(jobKey);
            }
            else if (job.JobType == "SqlServerQuery")
            {
                var redisDb = Program.Container.Resolve<IDatabase>();
                var sqlJob = ((string) redisDb.StringGet(store.BaseKey + ":SqlServerQueryConfig")).FromJson<SqlServerQueryJinxJob>();
                if (sqlJob == null)
                {
                    Log.Error("Couldn't find job object at key {Key} for type {Type}", store.BaseKey + ":SqlServerQueryConfig", "SqlServerQueryConfig");
                    return;
                }
                newJob = CreateSqlServerQueryJob(sqlJob,jobKey, store.BaseKey);
            }

            ITrigger newTrigger = null;
            try
            {
                newTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .WithCronSchedule(job.CronExpression)
                    .Build();
            }
            catch (Exception x)
            {
                Console.Write(x.Message);
                throw;
            }

            scheduler.ScheduleJob(newJob, newTrigger);
        }

        private IJobDetail CreateSqlServerQueryJob(SqlServerQueryJinxJob sqlJob, JobKey key, string baseStoreKey)
        {
            var job = JobBuilder.Create<RunSqlServerQueryJob>()
                .WithIdentity(key)
                .UsingJobData("query", sqlJob.Query)
                .UsingJobData("connectionKey", sqlJob.DatabaseConnectionKey)
                .UsingJobData("baseStore", baseStoreKey)
                .Build();

            return job;
        }

        private IJobDetail CreateTestJob(JobKey key)
        {
            var job = JobBuilder.Create<TestJob>()
                .WithIdentity(key)
                .Build();
            return job;
        }
    }

    public class JinxJobInfo 
    {
        public string JobType { get; set; }
        public string JobKeyName { get; set; }
        public string JobKeyGroup { get; set; }
        public string TriggerKeyName { get; set; }
        public string TriggerKeyGroup { get; set; }
        public string CronExpression { get; set; }
    }

    public class SqlServerQueryJinxJob 
    {
        public string DatabaseConnectionKey { get; set; }
        public string Query { get; set; }
    }
    public class JinxSchedule
    {
        public string DataStoreKey { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }

    public class JinxDataStore
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string BaseKey { get; set; }
    }

    public class TestJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var key = context.JobDetail.Key;
            var db = Program.Container.Resolve<ConnectionMultiplexer>().GetDatabase();
            var d = db.StringGet("urn:image:54b21644");
            Console.WriteLine("Job: {0}, returned data: {1}", key, d);
        }
    }

    public class RunNodeJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var jobKey = context.JobDetail.Key;

        }
    }

    public class BulkInsertSqlServerJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var ops = new JinxOps();
        //    ops.RunBulkInsertToSqlServer();
        }
    }

    public class JobHistoryListener : JobListenerSupport
    {
        public override string Name
        {
            get { return "JobHistoryListener"; }
        }

        public override void JobToBeExecuted(IJobExecutionContext context)
        {
            Serilog.Log.Information("Starting {JobKey}", context.JobDetail.Key);
        }

        public override void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            if (jobException != null)
            {
                Serilog.Log.Error(jobException.InnerException ?? jobException,"Error in job {JobKey}", context.JobDetail.Key);
            }
            Serilog.Log.Information("{JobKey} complete.  Run time was {Runtime}",context.JobDetail.Key, context.JobRunTime);
        }
    }
}
