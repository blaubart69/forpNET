using Spi;
using System;
using System.Collections.Generic;

namespace forp
{
    class Opts
    {
        public string inputfilename;
        public bool runCmdExe;
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
                .Add('f', "file",  OPTTYPE.VALUE, "input file", o => tmpOpts.inputfilename = o)
                .Add('c', "cmd", OPTTYPE.BOOL, "execute with cmd.exe", o => tmpOpts.runCmdExe = true)
                .Add('d', "dryrun", OPTTYPE.BOOL, "dry run", o => tmpOpts.dryrun = true)
                .Add('v', "verbose", OPTTYPE.BOOL, "verbose output", o => tmpOpts.debug = true)
                .Add('h', "help",  OPTTYPE.BOOL, "show help", o => showhelp = true)
                .GetOpts();

            commandlineTemplate = Spi.BeeOpts.Parse(args, cmdOpts, (string unknownOpt) => Console.Error.WriteLine($"unknow option [{unknownOpt}]"));

            if (showhelp)
            {
                Spi.BeeOpts.PrintOptions(cmdOpts);
                Console.WriteLine(
                        "\nSample:"
                    + "forpNET.exe {executable with %1...}"
                    );
                return false;
            }

            opts = tmpOpts;

            return true;
        }
    }
}
