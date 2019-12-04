using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Spi;

namespace forp
{
    class Stats
    {
        public long? AllItems;
        public long procTotalTime = 0;
        public long procUserTime = 0;
        public long procKernelTime = 0;
    }
    static class forp
    {
        static Log log = Log.GetLogger();
        public static Stats Run(IEnumerable<ProcCtx> ProcessesToStart, TextWriter outStream, TextWriter errStream, int maxParallel, bool skipEmptyLines, bool quiet, bool writeStderr, Func<long> numberJobs)
        {
            ProcessRedirectAsync.Init();
            Stats stats = new Stats();
            if (numberJobs != null)
            {
                Task.Run(() => stats.AllItems = numberJobs());
            }
            ICollection<uint> runningProcIDs = new HashSet<uint>();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                log.dbg($"starting with maxParallel: {maxParallel}");
                var cancel = cts.Token;
                if (!quiet)
                {
                    Console.Error.WriteLine("press 'q' to quit. 'h' for more keys.");
                }
                Task.Run(() => HandleKeys(cts, outStream, errStream, runningProcIDs));
                
                var procs = new MaxTasks();
                var procsTask = procs.Start(
                    tasks: ProcessesToStart.Select(
                        async (procToRun) =>
                            {
                                ProcessStats procStats = await RunOneProcess(procToRun.commandline, procToRun.prefix, outStream, errStream, cancel, skipEmptyLines, writeStderr, runningProcIDs).ConfigureAwait(false);
                                exitcodeWriter.WriteLine($"{procStats.ExitCode}\t{procToRun.commandline}");
                                Interlocked.Add(ref stats.procTotalTime,  procStats.TotalTime.Ticks);
                                Interlocked.Add(ref stats.procKernelTime, procStats.KernelTime.Ticks);
                                Interlocked.Add(ref stats.procUserTime,   procStats.UserTime.Ticks);
                            }),
                    MaxParallel: maxParallel,
                    cancel: cts.Token);

                if (!quiet)
                {
                    var status = new StatusLineWriter();
                    var currProcess = Process.GetCurrentProcess();
                    Misc.DoUntilTaskFinished(procsTask, TimeSpan.FromSeconds(2), () => WriteStatusLine(status, procs, currProcess, stats));
                    Console.Error.WriteLine();
                }
                else
                {
                    procsTask.Wait();
                }
            }
            return stats;
        }
        static async Task<ProcessStats> RunOneProcess(string commandline, string prefix, TextWriter outWriter, TextWriter errWriter,  CancellationToken cancel, bool skipEmptyLines, bool writeStderr, ICollection<uint> procIDs)
        {
            log.dbg("starting: [{0}]", commandline);

            uint? currProcID = null;

            ProcessStats procStats = await ProcessRedirectAsync.Run(
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
                    if ( kind == KINDOFOUTPUT.STDERR && !writeStderr)
                    {
                        return;
                    }

                    if ( skipEmptyLines && String.IsNullOrWhiteSpace(line) )
                    {
                        return;
                    }

                    TextWriter writer = kind == KINDOFOUTPUT.STDOUT ? outWriter : errWriter;

                    if (String.IsNullOrEmpty(prefix))
                    {
                        writer.WriteLine(line);
                    }
                    else
                    {
                        writer.WriteLine(prefix + "\t" + line);
                    }
                });

            if (currProcID.HasValue)
            {
                lock (procIDs)
                {
                    procIDs.Remove(currProcID.Value);
                }
                log.dbg("process ended: ID: {0}, ExitCode: {1}", currProcID.Value, procStats.ExitCode);
            }
            else
            {
                log.err("proc has no ID set. komisch...?");
            }

            return procStats;
        }
        
        static void HandleKeys(CancellationTokenSource cancelSource, TextWriter outStream, TextWriter errStream, ICollection<uint> runningProcIDs)
        {
            
            while (!cancelSource.IsCancellationRequested)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.KeyChar)
                {
                    case 'q': 
                        cancelSource.Cancel(); 
                        break;
                    case 'f': 
                        outStream.Flush();
                        errStream.Flush();
                        break;
                    case 'p':
                        PrintProcessInfo(runningProcIDs);
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
                + "\n  'f' to flush out/err files"
                + "\n  'p' to show running procIDs");
        }
        private static void PrintProcessInfo(ICollection<uint> runningProcIDs)
        {
            try
            {
                uint[] tmpProcIDs;
                lock (runningProcIDs)
                {
                    tmpProcIDs = runningProcIDs.ToArray();
                }

                string clause = String.Join(" OR ", tmpProcIDs.Select(id => $"ProcessId={id}"));

                var now = DateTime.Now;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessId,CommandLine,CreationDate FROM Win32_Process WHERE " + clause))
                using (ManagementObjectCollection objects = searcher.Get())
                {
                    foreach (var obj in objects)
                    {
                        var o = obj as ManagementBaseObject;
                        if (o != null)
                        {
                            string id = o["ProcessId"]?.ToString();
                            string commandline = o["CommandLine"]?.ToString();
                            DateTime started = ManagementDateTimeConverter.ToDateTime(o["CreationDate"].ToString());
                            TimeSpan duration = new TimeSpan(now.Ticks - started.Ticks);
                            Console.Error.WriteLine($"{id}\t{duration}\t[{commandline}]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.exception(ex);
            }
        }
        static void WriteStatusLine(StatusLineWriter statusLine, MaxTasks processes, Process currProcess, Stats stats)
        {
            currProcess.Refresh();

            string statsThreads = String.Join(" | ",
                currProcess.Threads.Cast<ProcessThread>()
                    .GroupBy(keySelector: t => t.ThreadState)
                    .Select(grp => $"{grp.Key.ToString()} ({(grp.Key == System.Diagnostics.ThreadState.Running ? grp.Count()-1 : grp.Count())})"));

            string all;
            string finished;
            if ( stats.AllItems.HasValue )
            {
                all = stats.AllItems.ToString();
                float ffinished = (float)processes.Done / (float)stats.AllItems.Value;
                finished = ffinished.ToString("P");
            }
            else
            {
                all = "?";
                finished = "?";
            }

            statusLine.Write($"running/done/all/finished\t{processes.Running}/{processes.Done}/{all}/{finished}"
                //+ $"\tthreads: {currProcess.Threads.Count}"
                + $"\t(forp thread states: {statsThreads})");
        }
    }
}
