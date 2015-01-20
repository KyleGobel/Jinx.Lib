using System;
using System.Diagnostics;
using System.Linq;
using Jinx.Lib;
using Jinx.Lib.Data;
using Jinx.Lib.Models;
using Jinx.Scheduler.Jobs;
using Microsoft.Practices.ObjectBuilder2;
using Microsoft.Practices.Unity;
using Quartz;
using Serilog;

namespace Jinx.Scheduler
{
    public class SchedulerJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            var data = Globals.Ioc.Resolve<IJinxDataRepo>();
            
            var jobs = data.GetJobs().Where(x => x.Enabled);
            foreach (var job in jobs)
            {
                var triggerKey = new TriggerKey(job.TriggerKeyName, job.TriggerKeyGroup);
                var trigger = context.Scheduler.GetTrigger(triggerKey) as ICronTrigger;

                if (trigger == null)
                {
                    //schedule
                    ScheduleJob(context.Scheduler, job);
                }
                else
                {
                    //check for updates
                    if (trigger.CronExpressionString == job.CronExpression)
                        continue;

                    //changed update job
                    context.Scheduler.UnscheduleJob(triggerKey);
                    //schedule job
                    ScheduleJob(context.Scheduler, job);
                }
            }
        }

        IJobDetail GetJobDetailFactory(JinxJob job)
        {
            var data = Globals.Ioc.Resolve<IJinxDataRepo>();
            IJobDetail details = null;
            switch (job.JobType)
            {
                case "SqlServerQuery":
                    var dictionary = data.GetJobDetail(job.JobId);
                    details = JobBuilder.Create<RunSqlServerQueryJob>()
                        .WithIdentity(job.JobKeyName, job.JobKeyGroup)
                        .WithDescription(job.Description)
                        .Build();
                    dictionary.ForEach(x => details.JobDataMap.Add(x));
                    break;
            }
            return details;
        }
        private void ScheduleJob(IScheduler scheduler, JinxJob job)
        {
            var data = Globals.Ioc.Resolve<IJinxDataRepo>();
            var triggerKey = new TriggerKey(job.TriggerKeyName, job.TriggerKeyGroup);

            var newJob = GetJobDetailFactory(job);
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
                Log.Error(x, "error creating trigger");
                throw;
            }

            scheduler.ScheduleJob(newJob, newTrigger);
            data.LogEvent(EventTypes.JobScheduled, new
            {
                Job = job,
                CronExpression = job.CronExpression,
                JobDataMap = newJob.JobDataMap.ToDictionary(x => x.Key, x => x.Value)
            });
        }
    }
}