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

        public static void Run(List<string> commandTemplate, IEnumerable<string[]> substitutes, Opts opts, CancellationToken cancel)
        {
            using (TextWriter writer         = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt",      append: false, encoding: Encoding.UTF8)))
            using (TextWriter exitcodeWriter = TextWriter.Synchronized(new StreamWriter(@".\forp.ExitCode.txt", append: false, encoding: Encoding.UTF8)))
            {
                var procs = new MaxTasks();
                var procsTask = procs.Start(
                    tasks: substitutes
                            .Select(sub =>
                            {
                                List<string> commandline = SubstitutePercent(commandTemplate, sub);
                                string exe = commandline[0];
                                string args = String.Join(" ", commandline.Skip(1));
                                if (opts.dryrun)
                                {
                                    log.inf("[{0}] [{1}]", exe, args);
                                    return Task.CompletedTask;
                                }
                                else
                                {
                                    return
                                        RunOneProcess(exe, args, writer, cancel)
                                        .ContinueWith((rc) =>
                                        {
                                            exitcodeWriter.WriteLine($"{rc.Result}\t{exe} {args}");
                                        });
                                }
                            }),
                    MaxParallel: 2);
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
        static List<string> SubstitutePercent(List<string> commandTemplate, string[] substitutes)
        {
            //log.dbg("SubstitutePercent(): template [{0}], subs [{1}]", commandTemplate, String.Join(",", substitutes));

            List<string> result = new List<string>(commandTemplate);

            for (int i = 0; i < substitutes.Length; ++i)
            {
                for (int j = 0; j < result.Count; ++j)
                {
                    string toReplace = "%" + (i + 1).ToString();
                    result[j] = result[j].Replace(toReplace, substitutes[i]);
                }
            }

            log.dbg("SubstitutePercent(): result [{0}]", String.Join(" ",result));

            return result;
        }
        static void DoUntilTaskFinished(Task task, int milliSeconds, Action doEvery)
        {
            while ( ! task.Wait(millisecondsTimeout: milliSeconds) )
            {
                doEvery.Invoke();
            }
            doEvery.Invoke();
        }
    }
}
