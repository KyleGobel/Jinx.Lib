using Jinx.Lib;
using Quartz;

namespace Jinx.Scheduler.Jobs
{
    public abstract class JinxJobBase : IJob
    {
        public static JobTypes JobType;

        protected JinxJobBase(JobTypes jobType)
        {
            JobType = jobType;
        }

        public void Execute(IJobExecutionContext context)
        {
            context.JobDetail.JobDataMap.Add("jobType", JobType.Name);
            ExecuteJob(context); 
        }

        public abstract void ExecuteJob(IJobExecutionContext context);
    }
}