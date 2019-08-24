using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi;

namespace forp
{
    class forp
    {
        public static void Run(string commandTemplate, IEnumerable<string[]> substitutes)
        {
            new MaxTasks().Start(
                tasks: substitutes
                        .Select(sub =>
                        {
                            string commandline = SubstitutePercent(commandTemplate, sub);
                            return RunOneProcess(commandline);
                        }),
                MaxParallel: 2)
            .Wait();
        }
        static Task RunOneProcess(string commandline)
        {
            return
            Task.Run(() =>
            {
                Log log = Log.GetLogger();
                log.dbgKeyVal("commandline to exec", commandline);
            });
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
