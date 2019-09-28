using Spi;
using System;
using System.Collections.Generic;
using System.IO;

namespace forp
{
    class Opts
    {
        public string inputfilename;
        public bool runCmdExe;
        public int maxParallel = 16;
        public bool firstOnly;
        public bool printPrefix = true;
        public bool skipEmptyLines = false;
        public bool printStatusLine = true;
        public bool debug;
        public bool dryrun;

        private Opts()
        {
        }

        public static bool ParseOpts(string[] args, out Opts opts, out List<string> commandlineTemplate)
        {
            opts = null;
            bool showhelp = false;

            Opts tmpOpts = new Opts() { };
            var cmdOpts = new BeeOptsBuilder()
                .Add('f',  "file", OPTTYPE.VALUE, "input file", o => tmpOpts.inputfilename = o)
                .Add('c',  "cmd", OPTTYPE.BOOL, "execute with [%ComSpec% /C]", o => tmpOpts.runCmdExe = true)
                .Add(null, "noprefix", OPTTYPE.BOOL, "do not prefix every output line with %1", o => tmpOpts.printPrefix = false)
                .Add('p',  "parallel", OPTTYPE.VALUE, $"run max parallel processes (default: {tmpOpts.maxParallel})", o => tmpOpts.maxParallel = Convert.ToInt32(o))
                .Add('1',  "first", OPTTYPE.BOOL, "run only for first line in inputfile", o => tmpOpts.firstOnly = true )
                .Add('n',  "dryrun", OPTTYPE.BOOL, "dry run", o => tmpOpts.dryrun = true)
                .Add('e',  "skipempty", OPTTYPE.BOOL, "do not write empty lines to output. String.IsNullOrWhiteSpace()", o => tmpOpts.skipEmptyLines = true)
                .Add(null, "nostatus", OPTTYPE.BOOL, "do not print the status line", o => tmpOpts.printStatusLine = false)
                .Add('d',  "debug", OPTTYPE.BOOL, "debug output", o => tmpOpts.debug = true)
                .Add('h',  "help", OPTTYPE.BOOL, "show help", o => showhelp = true)
                .GetOpts();

            commandlineTemplate = Spi.BeeOpts.Parse(args, cmdOpts, (string unknownOpt) => Console.Error.WriteLine($"unknow option [{unknownOpt}]"));

            if (showhelp)
            {
                Console.WriteLine(
                      "\nusage: forp.exe [OPTIONS] -- {exe} [options mixed with %1, %2, ...]"
                    + "\n  - each line from the input is parsed with CommandLineToArgv() to produce %1, %2, ..."
                    + "\n  - stdout and stderr from all executed processes is collected in the file: forp.out.txt"
                    + "\n\nOptions:");
                Spi.BeeOpts.PrintOptions(cmdOpts);
                return false;
            }

            if ( commandlineTemplate.Count == 0 )
            {
                Console.Error.WriteLine("no command given");
                return false;
            }

            if (!String.IsNullOrEmpty(tmpOpts.inputfilename))
            {
                if (!File.Exists(tmpOpts.inputfilename))
                {
                    Console.Error.WriteLine($"cannot find inputfile given [{tmpOpts.inputfilename}]");
                    return false;
                }
            }

            opts = tmpOpts;

            return true;
        }
    }
}
