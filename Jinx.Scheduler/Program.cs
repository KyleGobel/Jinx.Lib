using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jinx.Lib;
using Microsoft.Practices.Unity;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Listener;
using Quartz.Xml.JobSchedulingData20;
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
        private static void Main(string[] args)
        {
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
            var redisDb = Container.Resolve<ConnectionMultiplexer>().GetDatabase();
            redisDb.SetAdd("Jinx:DataStores:Ids", "Testing:Job1");

            var testDataStore = new JinxDataStore
            {
                BaseKey = "Jinx:DataStores:TestingStore",
                Enabled = true,
                Name = "Just a test store"
            };
            var testingJob = new JinxJob
            {
                CronExpression = "0 0/1 * 1/1 * ? *",
                JobKeyGroup = "Testing",
                JobKeyName = "Testjob",
                JobType = "TestJob",
                TriggerKeyGroup = "Testing",
                TriggerKeyName = "TestTrigger"
            };

            redisDb.StringSet("Testing:Job1", testDataStore.ToJson());
            redisDb.HashSet(testDataStore.BaseKey + ":Jobs", "anything", testingJob.ToJson());
        }

        private static void RegisterRedis(IUnityContainer container)
        {
            var redisServer = ConfigurationManager.AppSettings["RedisServer"];
            var connectionMultiplexer = ConnectionMultiplexer.Connect(redisServer);

            container.RegisterInstance(connectionMultiplexer);
        }
    }

    public class SchedulerJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var redisDb = Program.Container.Resolve<ConnectionMultiplexer>().GetDatabase();
            var dataStoresKeys = redisDb.SetMembers("Jinx:DataStores:Ids");

            Program.DataStores = dataStoresKeys.Select(dsKey =>
                ((string) redisDb.StringGet((string) dsKey))
                    .FromJson<JinxDataStore>())
                .ToList();

            foreach (var store in Program.DataStores.Where(x => x.Enabled))
            {
                var jobs = redisDb.HashGetAll(store.BaseKey + ":Jobs")
                    .Select(x =>((string) x.Value).FromJson<JinxJob>());

                foreach (var job in jobs)
                {
                    var triggerKey = new TriggerKey(job.TriggerKeyName, job.TriggerKeyGroup);
                    var trigger = context.Scheduler.GetTrigger(triggerKey) as ICronTrigger;

                    if (trigger == null)
                    {
                        //trigger is wrong or doesn't exits, create it
                        ScheduleJob(context.Scheduler, job);
                    }
                    else
                    {
                        if (trigger.CronExpressionString == job.CronExpression) 
                            continue;

                        //cron expression has changed, update it
                        context.Scheduler.UnscheduleJob(triggerKey);
                        ScheduleJob(context.Scheduler, job);
                    }
                }
            }
        }

        private void ScheduleJob(IScheduler scheduler, JinxJob job)
        {
            var jobKey = new JobKey(job.JobKeyName, job.JobKeyGroup);
            var triggerKey = new TriggerKey(job.TriggerKeyName, job.TriggerKeyGroup);
            IJobDetail newJob = null;
            if (job.JobType == "TestJob")
            {
                newJob = CreateTestJob(jobKey);
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

        private IJobDetail CreateTestJob(JobKey key)
        {
            var job = JobBuilder.Create<TestJob>()
                .WithIdentity(key)
                .Build();
            return job;
        }

    }

    public class JinxJob
    {
        public string JobType { get; set; }
        public string JobKeyName { get; set; }
        public string JobKeyGroup { get; set; }
        public string TriggerKeyName { get; set; }
        public string TriggerKeyGroup { get; set; }
        public string CronExpression { get; set; }
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
            Console.WriteLine("Listener: {0} is about to start", context.JobDetail.Key);
        }

        public override void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            Console.WriteLine("Listener: {0} has finished. Exception was {1}", context.JobDetail.Key, jobException);
            Console.WriteLine("Listener: Run time was {0}", context.JobRunTime);
        }
    }
}
