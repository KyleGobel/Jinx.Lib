using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using Jinx.Lib;
using Jinx.Lib.Data;
using Microsoft.Practices.Unity;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Serilog;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;
using StackExchange.Redis;

namespace Jinx.Scheduler
{
    class Program
    {
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
            Globals.Ioc = new UnityContainer();
            ConfigureLogging();
            Configure(Globals.Ioc);
            Globals.Ioc.Resolve<IJinxDataRepo>().LogEvent(EventTypes.ApplicationStart);
            
            JobSchedule = new List<JinxSchedule>();
            DataStores = new List<JinxDataStore>();

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

        private static void CreateDataStore(string storeName, string longName)
        {
            var keys = new RedisKeys(storeName);
            var redisDb = Globals.Ioc.Resolve<IDatabase>();
            redisDb.SetAdd(RedisKeys.Ids, keys.BaseKey());

            var store = new JinxDataStore
            {
                BaseKeyName = keys.BaseStoreName,
                Enabled = true,
                Name = longName
            };

            redisDb.StringSet(keys.BaseKey(), store.ToJson());
        }


            
        private static void TestStuff()
        {
            CreateDataStore("GoogleCampaigns", "Google Campaigns Transform");
            CreateDataStore("GoogleCampTransform", "I don't know what i'm doing");
            var keys = new RedisKeys("GoogleCampaigns");
            var redisDb = Globals.Ioc.Resolve<IDatabase>();

            var testingJob = new JinxJobInfo
            {
                CronExpression = "0/30 * * 1/1 * ? *",
                JobKeyGroup = "O&OSpend",
                JobKeyName = "O&OSpendExport",
                JobType = JobTypes.SqlServerQuery.ToString(),
                TriggerKeyGroup = "O&O",
                TriggerKeyName = "O&OSpendExportTrigger"
            };

            var sqlJob = new SqlServerQueryJinxJob
            {
                DatabaseConnectionKey = "PkcMobDb01-Email",
                Query = "select * from spend",
            };

            var sqlServerKey = keys.BaseKey() + ":SqlServerQueryConfig";
            redisDb.StringSet(sqlServerKey, sqlJob.ToJson());
            redisDb.HashSet(keys.Jobs(), sqlServerKey, testingJob.ToJson());
            CreateTransform("GoogleCampTransform");
        }

        public static void CreateTransform(string storeName)
        {
            var redisDb = Globals.Ioc.Resolve<IDatabase>();
            var keys = new RedisKeys(storeName);

            var transformKey = keys.BaseKey() + ":Transform";

            var jobInfo = new JinxJobInfo
            {
                CronExpression = "0/30 * * 1/1 * ? *",
                JobKeyGroup = "O&OSpend",
                JobKeyName = "O&OSpendTransform",
                JobType = JobTypes.Transform.ToString(),
                TriggerKeyGroup = "O&O",
                TriggerKeyName = "O&OSpendTranformTrigger"           
            };
            redisDb.HashSet(keys.Jobs(), transformKey, jobInfo.ToJson());

            var transformJob = new TransformJinxJob
            {
                DataKey = "Jinx:DataStores:GoogleCampaigns:Data",
                TransformJs = "function main(srcItems) { return _.map(srcItems, function(i) { console.log('inserting item ' + i); return  { date : i.date }; }); }",
                DestinationKey = keys.BaseKey() + ":DataOut"
            };
            redisDb.StringSet(transformKey, transformJob.ToJson());
        }

        private static void Configure(IUnityContainer container)
        {
            //serializer config
            JsConfig.EmitCamelCaseNames = true;
            JsConfig.PropertyConvention = PropertyConvention.Lenient;

            //redis
            var redisServer = ConfigurationManager.AppSettings["RedisServer"];
            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisServer);
            container.RegisterInstance(connectionMultiplexer);
            container.RegisterInstance(typeof (IDatabase), container.Resolve<ConnectionMultiplexer>().GetDatabase(2));

            //data store
            container.RegisterInstance(typeof (IJinxDataRepo), new PostgresDataRepo());
        }
    }

    public class SqlServerBulkInsertJinxJob
    {
        public Dictionary<string, string> PropertyToColumnMappings { get; set; } 
        public string DataKey { get; set; }
        public string DatabaseConnectionKey { get; set; }
    }

    public class TransformJinxJob
    {
        public string TransformJs { get; set; }
        public string DataKey { get; set; }
        public string DestinationKey { get; set; }
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
        public string BaseKeyName { get; set; }
    }

    public class TestJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var key = context.JobDetail.Key;
            var db = Globals.Ioc.Resolve<ConnectionMultiplexer>().GetDatabase();
            var d = db.StringGet("urn:image:54b21644");
            Console.WriteLine("Job: {0}, returned data: {1}", key, d);
        }
    }

    public class RunSqlServerBulkInsertJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var ops = new JinxOps();
        //    ops.RunBulkInsertToSqlServer();
        }
    }
}
