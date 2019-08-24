using Spi;
using System;

namespace forp
{
    class Opts
    {
        public string inputfilename;
        public bool debug;

        private Opts()
        {
        }

        public static bool ParseOpts(string[] args, out Opts opts, out string CommandlineTemplate)
        {
            opts = null;
            bool showhelp = false;

            Opts tmpOpts = new Opts() { };
            var cmdOpts = new BeeOptsBuilder()
                .Add('f', "file",  OPTTYPE.VALUE, "input file", o => tmpOpts.inputfilename = o)
                .Add('d', "debug", OPTTYPE.BOOL, "verbose output", o => tmpOpts.debug = true)
                .Add('h', "help",  OPTTYPE.BOOL, "show help", o => showhelp = true)
                .GetOpts();

            var templateArgs = Spi.BeeOpts.Parse(args, cmdOpts, (string unknownOpt) => Console.Error.WriteLine($"unknow option [{unknownOpt}]"));
            CommandlineTemplate = String.Join(" ", templateArgs);

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
