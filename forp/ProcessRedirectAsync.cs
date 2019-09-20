using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.ComponentModel;
using forp;
using System.Security;
using System.Runtime.ConstrainedExecution;

namespace Spi
{
    public class ProcessRedirectAsync
    {
        private static long g_PipeSerialNumber = -1;
        private static int  g_currProcId = 0;

        public delegate void OnProcessOutput(KINDOFOUTPUT kind, string line);
        public static void Init()
        {
            if (g_currProcId == 0)
            {
                g_currProcId = Process.GetCurrentProcess().Id;
            }
        }
        public static async Task Start(string commandline, OnProcessOutput onProcessOutput)
        {
            var pi = new PROCESS_INFORMATION();
            try
            {
                CreatePipeAsyncReadSyncWrite(out StreamReader reader, out PipeStream writer);
                using (reader)
                using (writer)
                {
                    var si = new STARTUPINFO(stdout: writer.SafePipeHandle, stderr: writer.SafePipeHandle);
                    
                    var procAttr = new IntPtr();
                    var threadAttr = new IntPtr();
                    if (!CreateProcess(
                        lpApplicationName: null,
                        lpCommandLine: commandline,
                        lpProcessAttributes: ref procAttr,
                        lpThreadAttributes: ref threadAttr,
                        bInheritHandles: true,
                        dwCreationFlags: 0,
                        lpEnvironment: IntPtr.Zero,
                        lpCurrentDirectory: null,
                        lpStartupInfo: ref si,
                        lpProcessInformation: out pi))
                    {
                        var wex = new Win32Exception();
                        throw wex;
                    }

                    /*
                    await Task
                            .WhenAll(
                                Misc.ReadLinesAsync(reader, (line) => onProcessOutput(KINDOFOUTPUT.STDOUT, line)),
                                Misc.ReadLinesAsync(reader, (line) => onProcessOutput(KINDOFOUTPUT.STDERR, line)))
                            .ConfigureAwait(false);
                            */
                    await Misc.ReadLinesAsync(reader, (line) => onProcessOutput(KINDOFOUTPUT.STDOUT, line)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if ( !pi.hThread.Equals(IntPtr.Zero))
                {
                    CloseHandle(pi.hThread);
                }
                if (!pi.hProcess.Equals(IntPtr.Zero))
                {
                    CloseHandle(pi.hProcess);
                }
            }
        }
        [Flags]
        public enum PipeModeFlags : uint
        {
            //One of the following type modes can be specified. The same type mode must be specified for each instance of the pipe.
            PIPE_TYPE_BYTE = 0x00000000,
            PIPE_TYPE_MESSAGE = 0x00000004,
            //One of the following read modes can be specified. Different instances of the same pipe can specify different read modes
            PIPE_READMODE_BYTE = 0x00000000,
            PIPE_READMODE_MESSAGE = 0x00000002,
            //One of the following wait modes can be specified. Different instances of the same pipe can specify different wait modes.
            PIPE_WAIT = 0x00000000,
            PIPE_NOWAIT = 0x00000001,
            //One of the following remote-client modes can be specified. Different instances of the same pipe can specify different remote-client modes.
            PIPE_ACCEPT_REMOTE_CLIENTS = 0x00000000,
            PIPE_REJECT_REMOTE_CLIENTS = 0x00000008
        }
        [Flags]
        public enum PipeOpenModeFlags : uint
        {
            PIPE_ACCESS_DUPLEX = 0x00000003,
            PIPE_ACCESS_INBOUND = 0x00000001,
            PIPE_ACCESS_OUTBOUND = 0x00000002,
            FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000,
            FILE_FLAG_WRITE_THROUGH = 0x80000000,
            FILE_FLAG_OVERLAPPED = 0x40000000,
            WRITE_DAC = 0x00040000,
            WRITE_OWNER = 0x00080000,
            ACCESS_SYSTEM_SECURITY = 0x01000000
        }
        private static void CreatePipeAsyncReadSyncWrite(
            out StreamReader lpReadPipe,
            out PipeStream lpWritePipe)
        {
            lpReadPipe = null;
            lpWritePipe = null;

            //string pipename = $"\\\\.\\Pipe\\forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";
            string pipename = $"forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";

            int nSize = 4096;
            var pipeRead = new System.IO.Pipes.NamedPipeServerStream(
                pipename,
                System.IO.Pipes.PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: nSize,
                outBufferSize: nSize,
                pipeSecurity: null,
                inheritability: HandleInheritability.Inheritable);
            
            var pipeWrite = new NamedPipeClientStream(
                serverName: ".", // The name of the remote computer to connect to, or "." to specify the local computer.
                pipeName: pipename,
                direction: PipeDirection.Out,
                options: PipeOptions.None,
                impersonationLevel: System.Security.Principal.TokenImpersonationLevel.None,
                inheritability: HandleInheritability.Inheritable);

            pipeWrite.Connect();

            lpReadPipe  = new StreamReader(pipeRead);
            lpWritePipe = pipeWrite;
        }
        #region DLLIMPORT
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcess(
           string lpApplicationName,
           string lpCommandLine,
           //ref SECURITY_ATTRIBUTES lpProcessAttributes,
           //ref SECURITY_ATTRIBUTES lpThreadAttributes,
           ref IntPtr lpProcessAttributes,
           ref IntPtr lpThreadAttributes,
           bool bInheritHandles,
           uint dwCreationFlags,
           IntPtr lpEnvironment,
           string lpCurrentDirectory,
           [In] ref STARTUPINFO lpStartupInfo,
           out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateNamedPipe(string lpName, uint dwOpenMode,
           uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize,
           uint nDefaultTimeOut, SECURITY_ATTRIBUTES lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
        #endregion
    }
    [StructLayout(LayoutKind.Sequential)]
    internal class STARTUPINFO : IDisposable
    {
        public const int STARTF_USESTDHANDLES = 0x00000100;

        public int cb;
        public IntPtr lpReserved = IntPtr.Zero;
        public IntPtr lpDesktop = IntPtr.Zero;
        public IntPtr lpTitle = IntPtr.Zero;
        public int dwX = 0;
        public int dwY = 0;
        public int dwXSize = 0;
        public int dwYSize = 0;
        public int dwXCountChars = 0;
        public int dwYCountChars = 0;
        public int dwFillAttribute = 0;
        public int dwFlags = 0;
        public short wShowWindow = 0;
        public short cbReserved2 = 0;
        public IntPtr lpReserved2 = IntPtr.Zero;
        public SafeHandle hStdInput = new SafeFileHandle(IntPtr.Zero, false);
        //public SafeFileHandle hStdOutput = new SafeFileHandle(IntPtr.Zero, false);
        //public SafeFileHandle hStdError = new SafeFileHandle(IntPtr.Zero, false);
        public SafeHandle hStdOutput;
        public SafeHandle hStdError;
        public STARTUPINFO(SafeHandle stdout, SafeHandle stderr)
        {
            cb = Marshal.SizeOf(this);
            hStdOutput = stdout;
            hStdError = stderr;
            this.dwFlags = STARTF_USESTDHANDLES;
        }
        public void Dispose()
        {
            // close the handles created for child process
            if (hStdInput != null && !hStdInput.IsInvalid)
            {
                hStdInput.Close();
                hStdInput = null;
            }

            if (hStdOutput != null && !hStdOutput.IsInvalid)
            {
                hStdOutput.Close();
                hStdOutput = null;
            }

            if (hStdError != null && !hStdError.IsInvalid)
            {
                hStdError.Close();
                hStdError = null;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }
}
