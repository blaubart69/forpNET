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
        public static void Run(IEnumerable<ProcCtx> ProcessesToStart, int maxParallel, bool skipEmptyLines)
        {
            ProcessRedirectAsync.Init();

            ICollection<uint> runningProcIDs = new HashSet<uint>();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (TextWriter writer         = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt",      append: false, encoding: Encoding.UTF8)))
            //using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                log.dbg($"starting with maxParallel: {maxParallel}");
                var cancel = cts.Token;
                Task.Run(() => HandleKeys(cts, writer, runningProcIDs));
                var procs = new MaxTasks();
                var procsTask = procs.Start(
                    tasks: ProcessesToStart.Select(
                        async (procToRun) =>
                            {
                                await RunOneProcess(procToRun.commandline, procToRun.prefix, writer, cancel, skipEmptyLines, runningProcIDs)
                                            .ConfigureAwait(false);
                                //exitcodeWriter.WriteLine($"{rc}\t{procToRun}");
                            }),
                    MaxParallel: maxParallel,
                    cancel: cts.Token);

                var status = new StatusLineWriter();
                var currProcess = Process.GetCurrentProcess();
                DoUntilTaskFinished(procsTask, TimeSpan.FromSeconds(1), () => WriteStatusLine(status, procs, currProcess));
            }
        }
        static async Task RunOneProcess(string commandline, string prefix, TextWriter writer, CancellationToken cancel, bool skipEmptyLines, ICollection<uint> procIDs)
        {
            log.dbg("starting: [{0}]", commandline);

            uint? currProcID = null;

            uint? ExitCode = await ProcessRedirectAsync.Start(
                commandline, 
                onProcessCreated: (uint procId) => 
                {
                    currProcID = procId;
                    lock (procIDs)
                    {
                        procIDs.Add(procId);
                    }
                },
                onProcessOutput: (kind, line) =>
                {
                    if ( skipEmptyLines && String.IsNullOrWhiteSpace(line) )
                    {
                        return;
                    }

                    if (String.IsNullOrEmpty(prefix))
                    {
                        writer.WriteLine(line);
                    }
                    else
                    {
                        writer.WriteLine(prefix + "\t" + line);
                    }
                });

            if ( currProcID.HasValue)
            {
                lock (procIDs)
                {
                    procIDs.Remove(currProcID.Value);
                }
                log.dbg("process ended: ID: {0}, ExitCode: {1}", currProcID.Value, ExitCode);
            }
            else
            {
                log.dbg("proc has no ID set. komisch...?");
            }
            
        }
        static void DoUntilTaskFinished(Task task, TimeSpan timeout, Action doEvery)
        {
            while (!task.Wait(timeout))
            {
                doEvery.Invoke();
            }
            doEvery.Invoke();
        }
        static void HandleKeys(CancellationTokenSource cancelSource, TextWriter outWriter, ICollection<uint> runningProcIDs)
        {
            Console.WriteLine("press 'q' to quit. 'h' for more keys.");
            while (!cancelSource.IsCancellationRequested)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.KeyChar)
                {
                    case 'q': 
                        cancelSource.Cancel(); 
                        break;
                    case 'f': 
                        outWriter.Flush(); 
                        break;
                    case 'p':
                        PrintProcessIDs(runningProcIDs);
                        break;
                    case 'h':
                        ShowKeyHelp();
                        break;
                }
            }
        }
        static void ShowKeyHelp()
        {
            Console.Error.WriteLine(
                  "\npress ..."
                + "\n  'q' to quit"
                + "\n  'f' to flush forp.out.txt"
                + "\n  'p' to show running procIDs");
        }
        private static void PrintProcessIDs(ICollection<uint> runningProcIDs)
        {
            string procIds;
            int count;
            procIds = String.Join(",", runningProcIDs);
            count = runningProcIDs.Count;
            Console.Error.WriteLine($"\nprocess IDs: ({count}) {procIds}");
        }

        static void WriteStatusLine(StatusLineWriter statusLine, MaxTasks processes, Process currProcess)
        {
            currProcess.Refresh();

            string statsThreads = String.Join(" | ",
                currProcess.Threads.Cast<ProcessThread>()
                    .GroupBy(keySelector: t => t.ThreadState)
                    .Select(grp => $"{grp.Key.ToString()} ({(grp.Key == System.Diagnostics.ThreadState.Running ? grp.Count()-1 : grp.Count())})"));

            statusLine.Write($"running/done\t{processes.Running}/{processes.Done}"
                + $"\tthreads: {currProcess.Threads.Count}"
                + $"\tthreadStates: {statsThreads}");
        }
    }
}
