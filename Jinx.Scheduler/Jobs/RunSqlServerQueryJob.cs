using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Jinx.Lib;
using Microsoft.Practices.Unity;
using Quartz;
using Serilog;
using ServiceStack;
using ServiceStack.Text;
using StackExchange.Redis;

namespace Jinx.Scheduler.Jobs
{
    public static class DataReaderExtensions
    {
        public static void ToJson(this IDataReader reader, Action<string> handler)
        {
            while (reader.Read())
            {
                var dict = new Dictionary<string, object>();
                var fieldCount = reader.FieldCount;
                for (var i = 0; i < fieldCount; i++)
                {
                    dict.Add(reader.GetName(i), reader[i]);
                }
                using (JsConfig.With(emitCamelCaseNames: true, dateHandler: DateHandler.ISO8601,
                        propertyConvention: PropertyConvention.Lenient))
                {
                    handler(dict.ToJson());
                }
            }
        }
    }
    public class RunSqlServerQueryJob : JinxJobBase
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<RunSqlServerQueryJob>();

        public RunSqlServerQueryJob() : base(JobTypes.SqlServerQuery)
        { }

        public override void ExecuteJob(IJobExecutionContext context)
        {
            var dataMap = context.JobDetail.JobDataMap;

            var baseKey = dataMap.GetString("redisStorageKey");
            var connKey = dataMap.GetString("connectionKey");
            var query = dataMap.GetString("query");

            GetDataFromQuery(connKey, query, baseKey);
        }

        public void GetDataFromQuery(string connectionKey, string query, string baseStoreName)
        {
            Log.Verbose("Getting data from query {Query}, with a connectionKey of {ConnectionKey} and a redis base store {BaseStoreName}", query, connectionKey, baseStoreName);
            var connStr = ConfigurationManager.ConnectionStrings[connectionKey].ConnectionString;
            var redisDb = Globals.Ioc.Resolve<IDatabase>();
            using (var connection = new SqlConnection(connStr))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                {
                    var reader = command.ExecuteReader();
                    var jsonData = new List<string>();
                    var totalCount = 0;
                    if (reader.HasRows)
                    {
                        reader.ToJson(x =>
                        {
                            totalCount++;
                            jsonData.Add(x);
                            if (jsonData.Count > 500)
                            {
                                Log.Verbose("Pushing {Count} records to redis.  {TotalRecords} pushed", jsonData.Count, totalCount);
                                redisDb.ListRightPush(RedisKeys.Data(baseStoreName), jsonData.Select(y => (RedisValue)y).ToArray());
                                jsonData.Clear();
                            }
                        });
                    }
                    if (jsonData.Any())
                    {
                        Log.Verbose("Pushing {Count} records to redis.  {TotalRecords} pushed", jsonData.Count, totalCount);
                        redisDb.ListRightPush(RedisKeys.Data(baseStoreName), jsonData.Select(y => (RedisValue)y).ToArray());
                        jsonData.Clear();
                    }
                    reader.Close();
                }
            }
        }
    }
}