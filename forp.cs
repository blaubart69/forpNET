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
        public struct ProcToExec
        {
            public string Exe;
            public string Args;
        }
        public static void Run(IEnumerable<ProcToExec> commandline, int maxParallel)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (TextWriter writer         = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt",      append: false, encoding: Encoding.UTF8)))
            using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                log.dbg($"starting with maxParallel: {maxParallel}");
                var cancel = cts.Token;
                Task.Run(() => HandleQuitPressed(cts));
                var procs = new MaxTasks();
                var procsTask = procs.Start(
                    tasks: commandline.Select(cl =>
                            {
                                return
                                    RunOneProcess(cl.Exe, cl.Args, writer, cancel)
                                    .ContinueWith((rc) =>
                                    {
                                        exitcodeWriter.WriteLine($"{rc.Result}\t{cl.Exe} {cl.Args}");
                                    }, scheduler: TaskScheduler.Default);
                            }),
                    MaxParallel: maxParallel,
                    cancel: cts.Token);

                var status = new StatusLineWriter();
                DoUntilTaskFinished(procsTask, TimeSpan.FromSeconds(1), () =>
                {
                    Process currProc = null;
                    try
                    {
                        currProc = System.Diagnostics.Process.GetCurrentProcess();
                    }
                    catch { }

                    string threadcount  = currProc == null ? "n/a" : currProc.Threads.Count.ToString();
                    status.Write($"running/done/error\t{procs.Running}/{procs.Done}/{procs.Error}"
                        + $"\tthreads: {threadcount}");
                });
            }
        }
        static Task<int> RunOneProcess(string exe, string args, TextWriter writer, CancellationToken cancel)
        {
            log.dbg("starting: [{0}] [{1}]", exe, args);

            var rc = ProcessRedirect.StartAsync(
                new System.Diagnostics.ProcessStartInfo(exe, args),
                OnOutput: (kind, line) =>
                {
                    writer.WriteLine(line);
                },
                cancel: cancel);

            if (rc.IsCompleted)
            {
                log.dbg("proc ended with rc={0}", rc);
            }
            return rc;
        }
        static void DoUntilTaskFinished(Task task, TimeSpan timeout, Action doEvery)
        {
            while (!task.Wait(timeout))
            {
                doEvery.Invoke();
            }
            doEvery.Invoke();
        }
        static void HandleQuitPressed(CancellationTokenSource cancelSource)
        {
            while (!cancelSource.IsCancellationRequested)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.KeyChar)
                {
                    case 'q': cancelSource.Cancel(); break;
                }
            }
        }
    }
}
