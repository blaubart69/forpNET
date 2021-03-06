﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Spi;

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
            PrintOptions(opts);
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
            bool appendAllInputTokens = 
                    commandTemplate.Count == 0 
                || (commandTemplate.Count == 1 && !Misc.isPercentTokenNumber(commandTemplate[0]));

            ExpandCommand(opts, ref commandTemplate);
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
                        Console.Out.WriteLine(p.commandline);
                    }
                }
                else
                {
                    GetOutErrStreams(opts.filenameStdout, opts.filenameStderr, out TextWriter outStream, out TextWriter errStream);
                    long start = DateTime.Now.Ticks;
                    Stats stats = null;
                    using (outStream)
                    using (errStream)
                    {
                        
                        stats = forp.Run(commandlines2Exec, outStream, errStream, opts.maxParallel, opts.skipEmptyLines, opts.quiet, opts.writeStderr, jobCount);
                    }
                    if (!opts.quiet)
                    {
                        TimeSpan forpDuration = new TimeSpan(DateTime.Now.Ticks - start);
                        Console.Error.WriteLine(
                                 "executed processes:"
                            + $"\n  TotalTime:   {new TimeSpan(stats.procTotalTime)}"
                            + $"\n  KernelTime:  {new TimeSpan(stats.procKernelTime)}"
                            + $"\n  UserTime:    {new TimeSpan(stats.procUserTime)}"
                            + $"\nforp duration: {forpDuration}");
                    }
                }
            }

            return 0;
        }

        private static void GetOutErrStreams(string filenameStdout, string filenameStderr, out TextWriter outStream, out TextWriter errStream)
        {
            if ( String.IsNullOrEmpty(filenameStdout) && String.IsNullOrEmpty(filenameStderr))
            {
                outStream = Console.Out;
                errStream = outStream;
            }
            else
            {
                outStream = TextWriter.Synchronized(new StreamWriter(filenameStdout, append: false, encoding: Encoding.UTF8));
                if ( String.IsNullOrEmpty(filenameStderr))
                {
                    errStream = outStream;
                }
                else
                {
                    errStream = TextWriter.Synchronized(new StreamWriter(filenameStderr, append: false, encoding: Encoding.UTF8));
                }
            }
        }

        private static void ExpandCommand(Opts opts, ref List<string> commandTemplate)
        {
            if (commandTemplate == null || commandTemplate?.Count == 0)
            {
                return;
            }

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
        static void PrintOptions(Opts opts)
        {
            Log log = Log.GetLogger();
            log.dbgKeyVal("filename input",  opts.inputfilename);
            log.dbgKeyVal("filename stdout", opts.filenameStdout);
            log.dbgKeyVal("filename stderr", opts.filenameStderr);
        }
    }
}
