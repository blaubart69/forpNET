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
                CreatePipeAsyncReadSyncWrite(out NamedPipeServerStream reader, out NamedPipeClientStream writer);
                using (reader)
                using (writer)
                {
                    const int STARTF_USESTDHANDLES = 0x00000100;
                    const int STD_INPUT_HANDLE = -10;

                    var si = new STARTUPINFO();
                    si.cb = (uint)Marshal.SizeOf<STARTUPINFO>();
                    si.dwFlags = STARTF_USESTDHANDLES;
                    si.hStdOutput = writer.SafePipeHandle.DangerousGetHandle();
                    si.hStdError  = writer.SafePipeHandle.DangerousGetHandle();
                    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

                    if (!CreateProcessW(
                        lpApplicationName:      null,
                        lpCommandLine:          new StringBuilder(commandline),
                        lpProcessAttributes:    IntPtr.Zero,
                        lpThreadAttributes:     IntPtr.Zero,
                        bInheritHandles:        true,
                        dwCreationFlags:        0,
                        lpEnvironment:          IntPtr.Zero,
                        lpCurrentDirectory:     null,
                        lpStartupInfo:          ref si,
                        lpProcessInformation:   out pi))
                    {
                        var wex = new Win32Exception();
                        Console.Error.WriteLine($"could not start process. lastErr={wex.NativeErrorCode}");
                        
                        throw wex;
                    }

                    Task stdout = Misc.ReadLinesAsync(new StreamReader(reader), (line) => onProcessOutput(KINDOFOUTPUT.STDOUT, line));
                    Task stderr = Misc.ReadLinesAsync(new StreamReader(reader), (line) => onProcessOutput(KINDOFOUTPUT.STDERR, line));

                    await Task.WhenAll(stdout, stderr);
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
            out NamedPipeServerStream ReadPipe,
            out NamedPipeClientStream WritePipe)
        {
            //string pipename = $"\\\\.\\Pipe\\forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";
            string pipename = $"forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";

            int nSize = 4096;
            ReadPipe = new System.IO.Pipes.NamedPipeServerStream(
                pipename,
                System.IO.Pipes.PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: nSize,
                outBufferSize: nSize,
                pipeSecurity: null,
                inheritability: HandleInheritability.Inheritable);
            
            WritePipe = new NamedPipeClientStream(
                serverName: ".", // The name of the remote computer to connect to, or "." to specify the local computer.
                pipeName: pipename,
                direction: PipeDirection.Out,
                options: PipeOptions.None,
                impersonationLevel: System.Security.Principal.TokenImpersonationLevel.None,
                inheritability: HandleInheritability.Inheritable);

            WritePipe.Connect();
            ReadPipe.WaitForConnection();
        }
        #region DLLIMPORT
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcessW(string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        /*
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateNamedPipe(string lpName, uint dwOpenMode,
           uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize,
           uint nDefaultTimeOut, SECURITY_ATTRIBUTES lpSecurityAttributes);
           */

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int whichHandle);

        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {

        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public uint cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }
}
