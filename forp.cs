using System;
using System.Collections.Generic;
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
                                    });
                            }),
                    MaxParallel: maxParallel,
                    cancel: cts.Token);

                var status = new StatusLineWriter();
                DoUntilTaskFinished(procsTask, TimeSpan.FromSeconds(2), () =>
                {
                    status.Write($"running/done/error\t{procs.Running}/{procs.Done}/{procs.Error}");
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

            log.dbg("proc ended with rc={0}",rc);
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
        static async void HandleQuitPressed(CancellationTokenSource cancelSource)
        {
            char[] buffer = new char[1];
            while (!cancelSource.IsCancellationRequested)
            {
                int read = await Console.In.ReadAsync(buffer, 0, 1);
                if ( read == 0 )
                {
                    break;
                }
                else if (read == 1)
                {
                    switch (buffer[0])
                    {
                        case 'q': cancelSource.Cancel(); break;
                    }
                }
            }
        }
    }
}
