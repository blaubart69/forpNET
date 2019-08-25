using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spi
{
    public enum KINDOFOUTPUT
    {
        STDOUT,
        STDERR
    }
    public class ProcessRedirect
    {
        public delegate void OutputHandler(KINDOFOUTPUT output, string line);

        public static Task<int> Start(ProcessStartInfo pi, OutputHandler OnOutput, CancellationToken cancel)
        {
            Log logger = Log.GetLogger();

            Process _proc = new Process()
            {
                StartInfo = pi,
                EnableRaisingEvents = false
            };
            _proc.StartInfo.UseShellExecute = false;
            _proc.StartInfo.RedirectStandardOutput = true;
            _proc.StartInfo.RedirectStandardError = true;

            _proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) { OnOutput(KINDOFOUTPUT.STDOUT, e.Data); } };
            _proc.ErrorDataReceived  += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) { OnOutput(KINDOFOUTPUT.STDERR, e.Data); } };

            /*
            Return Value
            Type: System.Boolean
            true if a process resource is started; 
            false if no new process resource is started (for example, if an existing process is reused).
            */

            bool started = _proc.Start();
            if (started == false)
            {
                logger.dbg("_proc.Start() returned false. no new process resource is started (for example, if an existing process is reused)");
            }

            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            return Task.Run(() =>
            {
                try
                {
                    _proc.WaitForExit();
                    int RetCode = _proc.ExitCode;
                    return RetCode;
                }
                finally
                {
                    _proc.Dispose();
                }
            });
        }
    }
}
