using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jinx.Lib
{
    public sealed class JobTypes
    {
        public static JobTypes SqlServerQuery = new JobTypes("SqlServerQuery");
        public static JobTypes ExecutePostgresSql = new JobTypes("ExecutePostgresSql");

        public readonly string Name;
        private JobTypes(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public static IEnumerable<JobTypes> GetAllTypes()
        {
            return new[] {SqlServerQuery, ExecutePostgresSql};
        }

        public static JobTypes FromName(string name)
        {
            return GetAllTypes().FirstOrDefault(x => x.Name == name);
        }
    }
}