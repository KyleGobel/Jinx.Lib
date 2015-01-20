using System;
using System.Collections.Generic;
using System.Linq;
using Chronos;
using Chronos.Configuration;
using Chronos.Dapper.Chronos.Dapper;
using Jinx.Lib.Models;
using Npgsql;
using ServiceStack;

namespace Jinx.Lib.Data
{
    public class PostgresDataRepo : IJinxDataRepo
    {
        private readonly string _connectionString;
        public PostgresDataRepo()
        {
            _connectionString = ConfigUtilities.GetConnectionString("Postgres");
            InitSchema();
        }

        private void InitSchema()
        {
            using (var conneciton = new NpgsqlConnection(_connectionString))
            {
                conneciton.Open();
                conneciton.Execute(EmbeddedSql.GetSqlQuery("db_init"));
            }
        }

        public List<JinxJob> GetJobs()
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                return connection.QueryWithMap<JinxJob>("select * from jobs", x => x.ToTitleCase());
            }
        }

        public Dictionary<string, object> GetJobDetail(int jobId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                var json = connection.Query<string>(
                    "select d.details from job_details d inner join jobs j on j.job_details_id = d.job_details_id where j.job_id = @jobId",
                    new {jobId = jobId}).SingleOrDefault();

                return json.FromJson<Dictionary<string, object>>();
            }
        }

        public void LogEvent(EventTypes eventType, object data = null)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                connection.Execute(
                    "insert into events (timestamp, event_name, data) values (@timestamp, @eventName, @data)",
                    new {timestamp = DateTime.UtcNow, eventName = eventType.ToString(), data = data.ToJson()});
            }
        }
    }
}