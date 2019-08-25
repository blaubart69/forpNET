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
    class forp
    {
        public static void Run(string commandTemplate, IEnumerable<string[]> substitutes, CancellationToken cancel)
        {
            TextWriter writer = TextWriter.Synchronized(new StreamWriter(".\forp.out.txt", append: false, encoding: Encoding.UTF8));

            new MaxTasks().Start(
                tasks: substitutes
                        .Select(sub =>
                        {
                            string commandline = SubstitutePercent(commandTemplate, sub);
                            return RunOneProcess(commandline, writer, cancel);
                        }),
                MaxParallel: 2)
            .Wait();
        }
        static Task RunOneProcess(string commandline, TextWriter writer, CancellationToken cancel)
        {
            return
                ProcessRedirect.Start(
                    new System.Diagnostics.ProcessStartInfo(commandline),
                    OnOutput: (kind, line) =>
                    {
                        writer.WriteLine(line);
                    }, 
                    cancel: cancel);
        }
        static string SubstitutePercent(string commandTemplate, string[] substitutes)
        {
            string result = commandTemplate;

            for ( int i=0; i<substitutes.Length; ++i)
            {
                string toReplace = "%" + (i+1).ToString();
                result = result.Replace(toReplace, substitutes[i]);
            }

            return result;
        }
    }
}
