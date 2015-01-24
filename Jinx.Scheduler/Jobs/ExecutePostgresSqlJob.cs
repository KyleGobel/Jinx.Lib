using Chronos.Configuration;
using Chronos.Dapper.Chronos.Dapper;
using Jinx.Lib;
using Npgsql;
using Quartz;

namespace Jinx.Scheduler.Jobs
{
    public class ExecutePostgresSqlJob : JinxJobBase
    {
        public ExecutePostgresSqlJob() : base(JobTypes.ExecutePostgresSql)
        { }

        public override void ExecuteJob(IJobExecutionContext context)
        {
            var dataMap = context.JobDetail.JobDataMap;

            var sql = dataMap.GetString("sql");
            var connectionKey = dataMap.GetString("connectionKey");

            ExecuteSql(sql, connectionKey);
        }

        private void ExecuteSql(string sql, string connectionKey)
        {
            var connectionString = ConfigUtilities.GetConnectionString(connectionKey);
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Execute(sql);
            }
        }
    }
}