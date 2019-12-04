using Spi;
using System;
using System.Collections.Generic;
using System.IO;

namespace forp
{
    class Opts
    {
        public string inputfilename;
        public string filenameStdout = null;
        public string filenameStderr = null;
        public bool allToStdout = false;
        public bool runCmdExe;
        public int maxParallel = 16;
        public bool firstOnly;
        public bool printPrefix = true;
        public bool skipEmptyLines = false;
        public bool quiet = false;
        public bool writeStderr = true;
        public bool debug;
        public bool dryrun;

        private Opts()
        {
        }

        public static bool ParseOpts(string[] args, out Opts opts, out List<string> commandlineTemplate)
        {
            opts = null;
            bool showhelp = false;

            string defaultOutFilename = @".\forp.out.txt";

            Opts tmpOpts = new Opts() { };
            var cmdOpts = new BeeOptsBuilder()
                .Add('f',  "file",      OPTTYPE.VALUE,  "input file", o => tmpOpts.inputfilename = o)
                .Add('c',  "cmd",       OPTTYPE.BOOL,   "execute with [%ComSpec% /C]", o => tmpOpts.runCmdExe = true)
                .Add('o',  "out",       OPTTYPE.VALUE, $"filename for stdout. default: {defaultOutFilename}", o => tmpOpts.filenameStdout = o)
                .Add('e',  "err",       OPTTYPE.VALUE, $"filename for stderr. default: {defaultOutFilename}", o => tmpOpts.filenameStderr = o)
                .Add('1',  "first",     OPTTYPE.BOOL,   "run only for first line in inputfile", o => tmpOpts.firstOnly = true )
                .Add('n',  "dryrun",    OPTTYPE.BOOL,   "dry run", o => tmpOpts.dryrun = true)
                .Add('s',  "skipempty", OPTTYPE.BOOL,   "do not write empty lines to output. String.IsNullOrWhiteSpace()", o => tmpOpts.skipEmptyLines = true)
                .Add('q',  "quiet",     OPTTYPE.BOOL,   "don't print anything", o => tmpOpts.quiet = true)
                .Add('x',  "parallel",  OPTTYPE.VALUE, $"run max parallel processes (default: {tmpOpts.maxParallel})", o => tmpOpts.maxParallel = Convert.ToInt32(o))
                .Add('d',  "debug",     OPTTYPE.BOOL,   "debug output", o => tmpOpts.debug = true)
                .Add('h',  "help",      OPTTYPE.BOOL,   "show help", o => showhelp = true)
                .Add(null, "stdout",    OPTTYPE.BOOL,   "write stdout/stderr of processes to stdout", o => tmpOpts.allToStdout = true)
                .Add(null, "noprefix",  OPTTYPE.BOOL,   "do not prefix every output line with %1", o => tmpOpts.printPrefix = false)
                .Add(null, "noerr",     OPTTYPE.BOOL,   "do not capture stderr", o => tmpOpts.writeStderr = false)
                .GetOpts();

            commandlineTemplate = Spi.BeeOpts.Parse(args, cmdOpts, (string unknownOpt) => Console.Error.WriteLine($"unknow option [{unknownOpt}]"));

            if (showhelp)
            {
                Console.WriteLine(
                      "\nusage: forp.exe [OPTIONS] -- [commandline to execute. use %1, %2, ... as tokens from your input]"
                    + "\n\nOptions:");
                Spi.BeeOpts.PrintOptions(cmdOpts);
                Console.WriteLine(
                  "\n  + if no inputfile is given. read input from stdin"
                + "\n  + each line from the input is parsed with CommandLineToArgv() to produce %1, %2, ..."
                + "\n\n  generated files:"
                + "\n    + forp.out.txt ........ stdout and stderr from all executed processes. each line is prepended with %1"
                + "\n    + forp.ExitCode.txt ... exitcode of each executed process. {rc}TAB{commandline}"
                + "\n\n  defaults:"
                + "\n    1, if just one program (exe/...) is passed as commandline, all the tokens form your input will be appended to it"
                + "\n    2, if no commandline is given the lines of your input are treated as a commandline to execute"
                );
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

            if (tmpOpts.allToStdout)
            {
                if (!(String.IsNullOrEmpty(tmpOpts.filenameStdout) && String.IsNullOrEmpty(tmpOpts.filenameStderr)))
                {
                    Console.Error.WriteLine($"you have specified to write everything to stdout, but also give a filename for out/err.");
                    return false;
                }
            }
            else
            {
                if (tmpOpts.writeStderr)
                {
                }
                else
                {
                    if (!String.IsNullOrEmpty(tmpOpts.filenameStderr))
                    {
                        Console.Error.WriteLine($"you have specified NOT TO WRITE stderr but a filename of [{tmpOpts.filenameStderr}] was given.");
                        return false;
                    }
                }

                if (String.IsNullOrEmpty(tmpOpts.filenameStdout))
                {
                    tmpOpts.filenameStdout = defaultOutFilename;
                }

            }
            opts = tmpOpts;


            return true;
        }
    }
}
