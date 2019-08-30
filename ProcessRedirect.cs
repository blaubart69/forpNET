using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spi
{
    public enum KINDOFOUTPUT
    {
        STDOUT,
        STDERR
    }
    public static class ProcessRedirect
    {
        static Log log = Log.GetLogger();

        public delegate void OutputHandler(KINDOFOUTPUT output, string line);
        public static async Task<int> StartAsync(ProcessStartInfo pi, OutputHandler OnOutput, CancellationToken cancel)
        {
            Log logger = Log.GetLogger();

            using (Process _proc = new Process() { StartInfo = pi, EnableRaisingEvents = false })
            {
                _proc.StartInfo.UseShellExecute = false;
                _proc.StartInfo.RedirectStandardOutput = true;
                _proc.StartInfo.RedirectStandardError = true;
                try
                {
                    _proc.Start();
                }
                catch (Win32Exception wex)
                {
                    log.win32err(wex, "Process.Start()");
                    return -1;
                }

                using (cancel.Register(() =>
                {
                    try
                    {
                        _proc.Kill();
                        logger.dbg("killed procid {0}", _proc.Id);
                    }
                    catch (Win32Exception wex)
                    {
                        log.win32err(wex, "Process.Kill()");
                    }
                }))
                {
                    await Task
                        .WhenAll(
                            ReadLinesAsync(_proc.StandardOutput, (line) => OnOutput(KINDOFOUTPUT.STDOUT, line)),
                            ReadLinesAsync(_proc.StandardError,  (line) => OnOutput(KINDOFOUTPUT.STDERR, line)))
                        .ConfigureAwait(false);
                }

                if (!_proc.HasExited)
                {
                    log.dbg("_proc has not exited yet. waiting...");
                    _proc.WaitForExit();
                }

                return _proc.ExitCode;
            }
        }
        static async Task ReadLinesAsync(TextReader input, Action<string> onLine)
        {
            string line;
            while ((line = await input.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                onLine(line);
            }
        }
    }
}
