using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            if (!Opts.ParseOpts(args, out Opts opts, out List<string> commandTemplate))
            {
                return 1;
            }

            if (opts.debug)
            {
                Log.SetLevel(Log.LEVEL.DEBUG);
            }
            Log log = Log.GetLogger();

            ThreadPool.GetMaxThreads(out int maxwork, out int maxio);
            ThreadPool.GetMinThreads(out int minwork, out int minio);
            log.dbg("minWork: {0}, minIO: {1}, maxWork: {2}, maxIO: {3}", minwork, minio, maxwork, maxio);
            if ( ThreadPool.SetMaxThreads(minwork, minio))
            {
                log.dbg("successfully set MaxThreads to {0}/{1}", minwork, minio);
            }
            //
            // must be placed here. afterwards commandtemplate get's expanded
            //
            bool appendAllInputTokens = commandTemplate.Count == 1;

            ExpandCommand(opts, commandTemplate);
            log.dbgKeyVal("CommandTemplate", String.Join(" ", commandTemplate));

            TextReader inputstream;
            Func<long> jobCount;
            if (String.IsNullOrEmpty(opts.inputfilename))
            {
                jobCount = null;
                inputstream = Console.In;
                log.dbg("reading from stdin");
            }
            else
            {
                inputstream = new StreamReader(opts.inputfilename);
                jobCount = () =>
                {
                    using (var linereader = new StreamReader(new FileStream(opts.inputfilename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan)))
                    {
                        return Misc.ReadLines(linereader).LongCount();
                    }
                };
            }

            using (inputstream)
            {
                
                IEnumerable<ProcCtx> commandlines2Exec = ConstructCommandline(opts.printPrefix, commandTemplate, inputstream, appendAllInputTokens);

                if (opts.firstOnly)
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
                    long start = DateTime.Now.Ticks;
                    Stats stats = forp.Run(commandlines2Exec, opts.maxParallel, opts.skipEmptyLines, opts.printStatusLine, jobCount);
                    TimeSpan forpDuration = new TimeSpan(DateTime.Now.Ticks - start);
                    log.inf($"forp duration:        {forpDuration}");
                    log.inf($"total time processes: {new TimeSpan(ticks: stats.TotalTicksProcesses)}");
                }
            }

            return 0;
        }

        private static void ExpandCommand(Opts opts, List<string> commandTemplate)
        {
            if (opts.runCmdExe
                            || commandTemplate[0].EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                            || commandTemplate[0].EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                commandTemplate.InsertRange(0, new string[] { Environment.GetEnvironmentVariable("ComSpec"), "/c" });
            }
            else if (commandTemplate[0].EndsWith(".vbs", StringComparison.OrdinalIgnoreCase))
            {
                commandTemplate.InsertRange(0, new string[] {
                    Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"),"system32","cscript.exe"),
                    "//NOLOGO" });
            }
        }

        private static IEnumerable<ProcCtx> ConstructCommandline(bool printPrefix, List<string> commandTemplate, TextReader inputstream, bool appendAllInputTokens)
        {
            return Misc.ReadLines(inputstream)
                .Select(inputlines => Native.CommandLineToArgv(inputlines))
                .Select(inputArgs =>
                {
                    IEnumerable<string> cmdLineTokens;
                    if (appendAllInputTokens)
                    {
                        cmdLineTokens = commandTemplate.Concat(inputArgs);
                    }
                    else
                    {
                        cmdLineTokens = SubstitutePercentVariables(commandTemplate, inputArgs);
                    }

                    return new ProcCtx
                    {
                        prefix = printPrefix ? inputArgs[0] : null,
                        commandline = String.Join(" ", cmdLineTokens.Select(arg => QuoteIfNeeded(arg)))
                    };
                });
        }

        static List<string> SubstitutePercentVariables(List<string> commandTemplate, string[] substitutes)
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
        static string QuoteIfNeeded(string token)
        {
            if (token.Contains(' '))
            {
                return "\"" + token + "\"";
            }
            else
            {
                return token;
            }
        }
        
    }
}
