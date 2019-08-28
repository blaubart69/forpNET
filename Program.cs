﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Spi;

namespace forp
{
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

            if (opts.runCmdExe)
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
            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (inputstream)
            {
                IEnumerable<string[]> substitutes = ReadLines(inputstream).Select(l => Native.CommandLineToArgv(l));
                forp.Run(commandTemplate, substitutes, opts, cts.Token);
            }

            return 0;
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
