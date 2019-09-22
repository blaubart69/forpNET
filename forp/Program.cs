using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Spi;
using static forp.forp;

namespace forp
{
    class ProcCtx
    {
        public string prefix;
        public string commandline;
    }
    class Program
    {
        static int Main(string[] args)
        {
            if ( ! Opts.ParseOpts(args, out Opts opts, out List<string> commandTemplate) )
            {
                return 1;
            }

            if ( opts.debug )
            {
                Log.SetLevel(Log.LEVEL.DEBUG);
            }
            Log log = Log.GetLogger();

            if (opts.runCmdExe 
                || commandTemplate[0].EndsWith(".bat", StringComparison.OrdinalIgnoreCase) 
                || commandTemplate[0].EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                commandTemplate.InsertRange(0, new string[] { Environment.GetEnvironmentVariable("ComSpec"), "/c" });
            }
            log.dbgKeyVal("CommandTemplate", String.Join(" ", commandTemplate));

            TextReader inputstream;
            if (String.IsNullOrEmpty(opts.inputfilename))
            {
                inputstream = Console.In;
                log.dbg("reading from stdin");
            }
            else
            {
                inputstream = new StreamReader(opts.inputfilename);
            }
            
            using (inputstream)
            {
                var commandlines2Exec =
                    ReadLines(inputstream)
                    .Select(inputlines => Native.CommandLineToArgv(inputlines))
                    .Select(substitutes =>
                        new ProcCtx
                        {
                            prefix = opts.printPrefix ? substitutes[0] : null,
                            commandline = String.Join(" ", 
                                SubstitutePercent(commandTemplate, substitutes)
                                .Select(arg => arg.Contains(' ') ? "\"" + arg + "\"" : arg))
                        });

                if ( opts.firstOnly )
                {
                    commandlines2Exec = commandlines2Exec.Take(1);
                }

                if (opts.dryrun)
                {
                    foreach (var p in commandlines2Exec)
                    {
                        log.inf($"{p.commandline}");
                    }
                }
                else
                {
                    forp.Run(commandlines2Exec, opts.maxParallel);
                }
            }

            return 0;
        }
        static List<string> SubstitutePercent(List<string> commandTemplate, string[] substitutes)
        {
            List<string> result = new List<string>(commandTemplate);

            for (int i = 0; i < substitutes.Length; ++i)
            {
                for (int j = 0; j < result.Count; ++j)
                {
                    string toReplace = "%" + (i + 1).ToString();
                    result[j] = result[j].Replace(toReplace, substitutes[i]);
                }
            }

            return result;
        }
        static IEnumerable<string> ReadLines(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
}
