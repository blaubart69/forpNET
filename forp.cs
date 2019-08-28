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
            using (TextWriter writer = TextWriter.Synchronized(new StreamWriter(@".\forp.out.txt", append: false, encoding: Encoding.UTF8)))
            {
                new MaxTasks().Start(
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
                                    return RunOneProcess(exe, args, writer, cancel);
                                }
                            }),
                    MaxParallel: 2)
                .Wait();
            }
        }
        static Task RunOneProcess(string exe, string args, TextWriter writer, CancellationToken cancel)
        {
            log.dbg("starting: [{0}] [{1}]", exe, args);

            return
                ProcessRedirect.Start(
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
    }
}
