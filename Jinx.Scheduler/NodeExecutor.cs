using System;
using System.Diagnostics;

namespace Jinx.Scheduler
{
    public class NodeExecutor
    {
        public static void Start(string straightJs)
        {
            var proc = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true, 
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    FileName = "node.exe",
                    Arguments = "-i",
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();

            proc.StandardInput.Write(straightJs);
            proc.StandardInput.WriteLine("setTimeout(function() { process.exit();}, 10000).supressOut;");
            proc.OutputDataReceived += proc_OutputDataReceived;
            proc.WaitForExit();
        }

        static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}