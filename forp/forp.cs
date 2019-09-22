using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Spi;

namespace forp
{
    static class forp
    {
        static Log log = Log.GetLogger();
        public static void Run(IEnumerable<string> ProcessesToStart, int maxParallel)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (TextWriter writer         = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt",      append: false, encoding: Encoding.UTF8)))
            using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                log.dbg($"starting with maxParallel: {maxParallel}");
                var cancel = cts.Token;
                Task.Run(() => HandleKeys(cts, writer));
                var procs = new MaxTasks();
                var procsTask = procs.Start(
                    tasks: ProcessesToStart.Select(
                        async (procCommandLine) =>
                            {
                                int rc = await RunOneProcess(procCommandLine, writer, cancel).ConfigureAwait(false);
                                exitcodeWriter.WriteLine($"{rc}\t{procCommandLine}");
                            }),
                    MaxParallel: maxParallel,
                    cancel: cts.Token);

                var status = new StatusLineWriter();
                var currProcess = Process.GetCurrentProcess();
                DoUntilTaskFinished(procsTask, TimeSpan.FromSeconds(1), () => WriteStatusLine(status, procs, currProcess));
            }
        }
        static async Task<int> RunOneProcess(string commandline, TextWriter writer, CancellationToken cancel)
        {
            log.dbg("starting: [{0}]", commandline);

            await ProcessRedirectAsync.Start(commandline, onProcessOutput: (kind, line) =>
            {
                log.dbg("out: {0}", line);
                writer.WriteLine(line);
            });

            log.dbg("proc ended");
            return 99;
        }
        static void DoUntilTaskFinished(Task task, TimeSpan timeout, Action doEvery)
        {
            while (!task.Wait(timeout))
            {
                doEvery.Invoke();
            }
            doEvery.Invoke();
        }
        static void HandleKeys(CancellationTokenSource cancelSource, TextWriter outWriter)
        {
            Console.Error.WriteLine("press 'q' to quit. 'f' to flush output file");
            while (!cancelSource.IsCancellationRequested)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.KeyChar)
                {
                    case 'q': cancelSource.Cancel(); break;
                    case 'f': outWriter.Flush(); break;
                }
            }
        }
        static void WriteStatusLine(StatusLineWriter statusLine, MaxTasks processes, Process currProcess)
        {
            currProcess.Refresh();

            statusLine.Write($"running/done\t{processes.Running}/{processes.Done}"
                + $"\tthreads: {currProcess.Threads.Count}"
                + $"\tprivMem: {Misc.StrFormatByteSize(currProcess.PrivateMemorySize64)}");
        }
    }
}
