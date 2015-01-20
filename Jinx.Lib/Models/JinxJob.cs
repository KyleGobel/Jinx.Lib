namespace Jinx.Lib.Models
{
    public class JinxJob
    {
        public int JobId { get; set; }
        public int JobDetailId { get; set; }
        public string Name { get; set; } 
        public string Description { get; set; }
        public string GroupName { get; set; }
        public bool Enabled { get; set; }
        public string JobType { get; set; }
        public string JobKeyName { get; set; }
        public string JobKeyGroup { get; set; }
        public string TriggerKeyName { get; set; }
        public string TriggerKeyGroup { get; set; }
        public string CronExpression { get; set; }
    }
}