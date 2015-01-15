using System.Dynamic;

namespace Jinx.Lib
{
    public class RedisKeys
    {
        public static string BasePrefix = "DataStores";
        private const string Seperator = ":";

        public readonly string BaseStoreName;
        public RedisKeys(string name)
        {
            BaseStoreName = name;
        }



        public static string Ids
        {
            get
            {
                return string.Join(Seperator, BasePrefix, "Ids");
            }
        }


        public static string BaseKey(string dataStoreName)
        {
            return string.Join(Seperator, BasePrefix, dataStoreName);
        }

        public static string Data(string dataStoreName)
        {
            return string.Join(Seperator, BasePrefix, dataStoreName, "Data");
        }

        public string JobKey(JobTypes type)
        {
            switch (type)
            {
                case JobTypes.SqlServerQuery:
                    return BaseKey(BaseKey()) + Seperator + "SqlServerQuery";
                default:
                    return "";
            }
        }
        public string Data()
        {
            return Data(BaseStoreName);
        }
        public string BaseKey()
        {
            return BaseKey(BaseStoreName);
        }
        public static string Jobs(string dataStoreName)
        {
            return string.Join(Seperator, BasePrefix, dataStoreName, "Jobs");
        }

        public string Jobs()
        {
            return Jobs(BaseStoreName);
        }
    }

    public enum KeyTypes
    {
        BaseKey = 0,
        Ids = 1,
        Jobs = 2,
    }
}