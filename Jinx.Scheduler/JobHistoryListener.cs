using System;
using Jinx.Lib;
using Microsoft.Practices.Unity;
using Quartz;
using Quartz.Listener;
using ServiceStack;
using StackExchange.Redis;

namespace Jinx.Scheduler
{
    public class JobHistoryListener : JobListenerSupport
    {
        public const string HistoryBaseKey = "HistoryLog";
        public override string Name
        {
            get { return "JobHistoryListener"; }
        }

        private readonly IDatabase _redisDb;
        public JobHistoryListener()
        {
            _redisDb = Globals.Ioc.Resolve<IDatabase>();
        }

        public override void JobToBeExecuted(IJobExecutionContext context)
        {
            var e = new {Message = "Starting Job", JobQuartzKey = context.JobDetail.Key};
            JinxOps.LogHistoryEvent(_redisDb,e);
            Serilog.Log.Information("Starting {JobKey}", context.JobDetail.Key);

        }

        public override void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            if (jobException != null)
            {
                var ex = GetLowestException(jobException);
                var e = new
                {
                    Message = "Error in job",
                    JobQuartzKey = context.JobDetail.Key,
                    ExceptionType = ex.GetType().Name,
                    ExceptionMessage = ex.Message,
                    FullExceptionDetails = ex.ToString()
                };
                JinxOps.LogHistoryEvent(_redisDb, e);
                Serilog.Log.Error(jobException.InnerException ?? jobException,"Error in job {JobKey}", context.JobDetail.Key);
            }
            var endMsg = new
            {
                Message = "Job complete",
                RunTime = context.JobRunTime.ToString("g"),
                JobQuartzKey = context.JobDetail.Key
            };
            JinxOps.LogHistoryEvent(_redisDb, endMsg);
            Serilog.Log.Information("{JobKey} complete.  Run time was {Runtime}", context.JobDetail.Key,
                context.JobRunTime);
        }

        private Exception GetLowestException(Exception jobException)
        {
            if (jobException.InnerException == null)
                return jobException;

            return GetLowestException(jobException.InnerException);
        }
    }
}