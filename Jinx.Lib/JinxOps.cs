﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Chronos;
using Chronos.Configuration;
using Chronos.Dapper.Chronos.Dapper;
using Jinx.Scheduler;
using ServiceStack;
using ServiceStack.Text;
using StackExchange.Redis;

namespace Jinx.Lib
{
    public interface IJinxRedisConfiguration
    {
        string DataSuffixKey { get; }
        string ColumnMappingsSuffixKey { get; }
        string ConfigSuffixKey { get; }
        int ItemsToTakePerIteration { get; }


        //Hash keys in the config hash
        string ConfigHashDatabaseKeyKey { get; }
        string ConfigHashTableNameKey { get; }
        string ConfigHashDatabaseTypeKey { get; }

    }
    public class JinxRedisConfigDefault : IJinxRedisConfiguration
    {
        public string ConfigSuffixKey
        {
            get { return "SqlInsertConfig"; }
        }
        public string DataSuffixKey
        {
            get { return "Data"; }
        }
        public string ColumnMappingsSuffixKey
        {
            get { return "ColumnMappings"; }
        }
        public int ItemsToTakePerIteration { get { return 10000; }}
        public string ConfigHashDatabaseKeyKey { get { return "databaseKey"; }  }
        public string ConfigHashTableNameKey { get { return "tableName"; } }
        public string ConfigHashDatabaseTypeKey { get { return "databaseType"; }  }
    }

    public static class JinxExtensions
    {
        public static string ToRedisKey(this string baseKey, string suffixKey)
        {
            return string.Format("{0}:{1}", baseKey, suffixKey);
        }
    }
    public class JinxOps
    {
        public void InsertToRedis<T>(List<T> data, IDatabase redisDb, string baseRedisKey,
            IJinxRedisConfiguration redisConfig = null)
        {
            redisConfig = redisConfig ?? new JinxRedisConfigDefault();
            var dataKey = baseRedisKey.ToRedisKey(redisConfig.DataSuffixKey);
            using (JsConfig.With(emitCamelCaseNames:true, dateHandler: DateHandler.ISO8601, propertyConvention: PropertyConvention.Lenient))
            {
                var jsonData = data.Select(x => (RedisValue)x.ToJson()).ToArray();
                redisDb.ListRightPush(dataKey, jsonData);               
            }
        }

        public static void LogHistoryEvent(IDatabase redisDb, object payload)
        {
            redisDb.ListLeftPush("HistoryLog", payload.ToJson());
        }

        public static void CreateSqlServerQueryJob(string query, string cronExpression, string connectionStringKey, string storeKey, string groupName = null, string jobName = null)
        {
            var multiplex = ConnectionMultiplexer.Connect(ConfigUtilities.GetAppSetting("Redis"));
            IDatabase redisDb = multiplex.GetDatabase(db: int.Parse(ConfigUtilities.GetAppSetting("RedisDatabase")));

            var keys = new RedisKeys(storeKey);
            redisDb.ListLeftPush(RedisKeys.Ids, keys.BaseKey());

            var triggerName = jobName != null ? jobName + "Trigger" : Guid.NewGuid().ToString("N");
            var job = new JinxJobInfo
            {
                JobType = "SqlServerQuery",
                JobKeyGroup = groupName ?? Guid.NewGuid().ToString("N"),
                JobKeyName =  jobName ?? Guid.NewGuid().ToString("N"),
                TriggerKeyGroup =  groupName ?? Guid.NewGuid().ToString("N"),
                TriggerKeyName = triggerName,
                CronExpression = cronExpression
            };

            var keyToSqlServerJob = keys.JobKey(JobTypes.SqlServerQuery);

            redisDb.HashSet(keys.Jobs(), keyToSqlServerJob, job.ToJson());

            redisDb.StringSet(keyToSqlServerJob,
                (new SqlServerQueryJinxJob {DatabaseConnectionKey = connectionStringKey, Query = query}).ToJson());
        }

        public void InsertToRedisFromSqlQuery(IDatabase redisDb, string baseRedisKey,
            IJinxRedisConfiguration redisConfig = null)
        {
            redisConfig = redisConfig ?? new JinxRedisConfigDefault();
            var dataKey = baseRedisKey.ToRedisKey(redisConfig.DataSuffixKey);
            var connectionStringKey = "";
            var query = "";

            var connStr = ConfigUtilities.GetConnectionString(connectionStringKey);
            
            using (var connection = new SqlConnection(connStr))
            {
                connection.Open();
                var rows = connection.Query(query);
                InsertToRedis<dynamic>(rows.ToList(),redisDb,baseRedisKey,redisConfig);
            }

        }
        public void RunBulkInsertToSqlServer(IDatabase redisDb, string baseRedisKey, IJinxRedisConfiguration redisConfig)
        {
            redisConfig = redisConfig ?? new JinxRedisConfigDefault();

            var dataKey = string.Format("{0}:{1}", baseRedisKey, redisConfig.DataSuffixKey);
            var mappingsKey = string.Format("{0}:{1}", baseRedisKey, redisConfig.ColumnMappingsSuffixKey);
            var storeConfigKey = string.Format("{0}:{1}", baseRedisKey, redisConfig.ConfigSuffixKey);


            var dataLength = redisDb.ListLength(dataKey);

            if (dataLength == 0)
                return;

            if (dataLength > redisConfig.ItemsToTakePerIteration)
                dataLength = redisConfig.ItemsToTakePerIteration;

            var values = redisDb.ListRange(dataKey, 0, dataLength);

            var json = "[" + values.Aggregate((x, y) => x + "," + y) + "]";

            var configDict = redisDb.HashGetAll(storeConfigKey).ToDictionary(x => x.Name, x => x.Value);
            var mappingsDict = redisDb.HashGetAll(mappingsKey).ToDictionary(x => (string)x.Name, x => (string)x.Value);
            var bcp = new SqlServerBulkInserter(configDict[redisConfig.ConfigHashDatabaseTypeKey])
            {
                ColumnMappings = new Mappings()
                    .ClearMappings()
                    .AddStringDictionary(mappingsDict)
            };
            bcp.Insert(new ServiceStackSerializer(), json, configDict[redisConfig.ConfigHashTableNameKey]);
            redisDb.ListTrim(dataKey, dataLength, -1);
        }
    }
}
