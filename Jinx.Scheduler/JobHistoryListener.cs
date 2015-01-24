using System;
using System.Linq;
using Jinx.Lib;
using Jinx.Lib.Data;
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
        private readonly IJinxDataRepo _data;
        public JobHistoryListener()
        {
            _redisDb = Globals.Ioc.Resolve<IDatabase>();
            _data = Globals.Ioc.Resolve<IJinxDataRepo>();
        }

        public override void JobToBeExecuted(IJobExecutionContext context)
        {
            var e = new {Message = "Starting Job", JobQuartzKey = context.JobDetail.Key};
            Serilog.Log.Information("Starting {JobKey}", context.JobDetail.Key);

        }

        public override void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            if (jobException != null)
            {
                Serilog.Log.Error(jobException.InnerException ?? jobException,"Error in job {JobKey}", context.JobDetail.Key);
            }

            Serilog.Log.Information("{JobKey} complete.  Run time was {Runtime}", context.JobDetail.Key,
                context.JobRunTime);

            var map = context.JobDetail.JobDataMap.ToDictionary(x => x.Key, x => x.Value);
            if (map.ContainsKey("jobId") && map.ContainsKey("jobType"))
            {
                _data.LogJobHistory((int)map["jobId"],context.JobRunTime,JobTypes.FromName(map["jobType"].ToString()), map, GetExceptionData(jobException));
            }
        }

        private object GetExceptionData(Exception ex)
        {
            if (ex == null)
                return null;
            ex = ex.GetInnerMostException();

            return new
            {
                Type = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                Message = ex.Message,
            };
        }
        private Exception GetLowestException(Exception jobException)
        {
            if (jobException.InnerException == null)
                return jobException;

            return GetLowestException(jobException.InnerException);
        }
    }
}