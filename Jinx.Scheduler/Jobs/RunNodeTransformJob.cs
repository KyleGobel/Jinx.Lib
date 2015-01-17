using System.Diagnostics;
using System.IO;
using Quartz;
using Serilog;

namespace Jinx.Scheduler.Jobs
{
    public class RunNodeTransformJob : IJob
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<RunSqlServerQueryJob>();
        public void Execute(IJobExecutionContext context)
        {
            var jobKey = context.JobDetail.Key;
            var dataMap = context.JobDetail.JobDataMap;

            var transformJobKey = dataMap.GetString("transformJobKey");

            RunNode(transformJobKey);

        }

        private void RunNode(string jobKey)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = @"C:\Dev\github.com\KyleGobel\Jinx.Lib\xform",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    FileName = "node.exe",
                    Arguments = "index.js " + jobKey,
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.OutputDataReceived += proc_OutputDataReceived;
            proc.WaitForExit();
        }

        private void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log.Information("Node: {Data}",e.Data);
        }

    }
}