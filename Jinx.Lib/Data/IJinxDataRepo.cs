using System;
using System.Collections.Generic;
using Jinx.Lib.Models;

namespace Jinx.Lib.Data
{
    public interface IJinxDataRepo
    {
        List<JinxJob> GetJobs();
        Dictionary<string, object> GetJobDetail(int jobId);
        void LogEvent(EventTypes eventType, object data = null);

        void LogJobHistory(int jobId, TimeSpan runTime, JobTypes jobType, object jobData = null, object exceptionData = null);

    }
}