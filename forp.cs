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
        public static void Run(IEnumerable<ProcToExec> commandline, int maxParallel, CancellationToken cancel)
        {
            using (TextWriter writer         = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt",      append: false, encoding: Encoding.UTF8)))
            using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                log.dbg($"starting with maxParallel: {maxParallel}");
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
                    MaxParallel: maxParallel);

                var status = new StatusLineWriter();
                DoUntilTaskFinished(procsTask, 2000, () =>
                {
                    status.Write($"running/done/error\t{procs.Running}/{procs.Done}/{procs.Error}");
                });
            }
        }
        static Task<int> RunOneProcess(string exe, string args, TextWriter writer, CancellationToken cancel)
        {
            log.dbg("starting: [{0}] [{1}]", exe, args);

            return
                ProcessRedirect.StartAsync(
                    new System.Diagnostics.ProcessStartInfo(exe, args),
                    OnOutput: (kind, line) =>
                    {
                        writer.WriteLine(line);
                    },
                    cancel: cancel);
        }
        static void DoUntilTaskFinished(Task task, int milliSeconds, Action doEvery)
        {
            while (!task.Wait(millisecondsTimeout: milliSeconds))
            {
                doEvery.Invoke();
            }
            doEvery.Invoke();
        }
    }
}
