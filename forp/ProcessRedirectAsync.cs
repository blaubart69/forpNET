﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.ComponentModel;
using System.Security;

using forp;

namespace Spi
{
    public enum KINDOFOUTPUT
    {
        STDOUT,
        STDERR
    }
    public class ProcessStats
    {
        public uint ExitCode;
        public TimeSpan KernelTime;
        public TimeSpan UserTime;
        public TimeSpan TotalTime;
    }
    public class ProcessRedirectAsync
    {
        static Log log = Log.GetLogger();

        private static long g_PipeSerialNumber = -1;
        private static int  g_currProcId = 0;

        public delegate void OnProcessOutput(KINDOFOUTPUT kind, string line);
        public delegate void OnProcessCreated(uint procId);
       
        public static void Init()
        {
            if (g_currProcId == 0)
            {
                g_currProcId = Process.GetCurrentProcess().Id;
            }
        }
        public static async Task<ProcessStats> Run(string commandline, OnProcessOutput onProcessOutput, OnProcessCreated onProcessCreated)
        {
            var pi = new PROCESS_INFORMATION();
            try
            {
                CreatePipeAsyncReadSyncWrite(out NamedPipeServerStream readerOut, out NamedPipeClientStream writerOut);
                CreatePipeAsyncReadSyncWrite(out NamedPipeServerStream readerErr, out NamedPipeClientStream writerErr);

                using (readerOut)
                using (writerOut)
                using (readerErr)
                using (writerErr)
                {
                    pi = CreateProcess(commandline, writerOut, writerErr);
                    onProcessCreated?.Invoke(pi.dwProcessId);
                    //
                    // 2019-09-22 Spindi himself
                    //  this Close() is very importante.
                    //  When we do not close the writer handle here, the read from out/err will hang.
                    //
                    writerOut.Close();
                    writerErr.Close();

                    Task stdout = Misc.ReadLinesAsync(new StreamReader(readerOut), (line) => onProcessOutput(KINDOFOUTPUT.STDOUT, line));
                    Task stderr = Misc.ReadLinesAsync(new StreamReader(readerErr), (line) => onProcessOutput(KINDOFOUTPUT.STDERR, line));

                    await Task.WhenAll(stdout, stderr);
                    uint exitCode = GetExitCode(pi);
                    return CreateProcessStats(pi, exitCode);
                }
            }
            finally
            {
                if (!pi.hProcess.Equals(IntPtr.Zero))
                {
                    CloseHandle(pi.hProcess);
                }
            }
        }

        private static uint GetExitCode(PROCESS_INFORMATION pi)
        {
            uint exitCode;
            if (!GetExitCodeProcess(pi.hProcess, out exitCode))
            {
                const UInt32 WAIT_TIMEOUT = 0x00000102;
                UInt32 rc;
                while ((rc = WaitForSingleObject(pi.hProcess, 1000)) == WAIT_TIMEOUT)
                {
                    log.err($"waiting for process {pi.dwProcessId} to become signaled. WaitForSingleObject()");
                }
                if (!GetExitCodeProcess(pi.hProcess, out exitCode))
                {
                    var lasterr = new Win32Exception();
                    throw new Exception($"still no ExitCode after WaitForSingleObject() on process handle. GetExitCodeProcess() returned: {lasterr.NativeErrorCode}");
                }
            }

            return exitCode;
        }

        private static ProcessStats CreateProcessStats(PROCESS_INFORMATION pi, uint exitCode)
        {
            System.Runtime.InteropServices.ComTypes.FILETIME creationTime;
            System.Runtime.InteropServices.ComTypes.FILETIME exitTime;
            System.Runtime.InteropServices.ComTypes.FILETIME userTime;
            System.Runtime.InteropServices.ComTypes.FILETIME kernelTime;
            if (!GetProcessTimes(pi.hProcess, out creationTime, out exitTime, out kernelTime, out userTime))
            {
                log.win32err(new Win32Exception(), "GetProcessTimes");
            }

            return new ProcessStats()
            {
                ExitCode = exitCode,
                KernelTime = Misc.FiletimeToTimeSpan(kernelTime),
                UserTime = Misc.FiletimeToTimeSpan(userTime),
                TotalTime = Misc.FiletimeToTimeSpan(exitTime).Subtract(Misc.FiletimeToTimeSpan(creationTime))
            };
        }

        private static PROCESS_INFORMATION CreateProcess(string commandline, NamedPipeClientStream writerOut, NamedPipeClientStream writerErr)
        {
            PROCESS_INFORMATION pi;
            const int STARTF_USESTDHANDLES = 0x00000100;
            const int STD_INPUT_HANDLE = -10;
            //const int STD_ERROR_HANDLE = -12;
            const uint IDLE_PRIORITY_CLASS = 0x00000040;

            var si = new STARTUPINFO();
            //si.cb = (uint)Marshal.SizeOf<STARTUPINFO>(); // 4.6 vs 45 fix
            si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
            si.dwFlags = STARTF_USESTDHANDLES;
            si.hStdOutput = writerOut.SafePipeHandle.DangerousGetHandle();
            si.hStdError = writerErr.SafePipeHandle.DangerousGetHandle();
            si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

            if (!CreateProcessW(
                lpApplicationName: null,
                lpCommandLine: new StringBuilder(commandline),
                lpProcessAttributes: IntPtr.Zero,
                lpThreadAttributes: IntPtr.Zero,
                bInheritHandles: true,
                dwCreationFlags: IDLE_PRIORITY_CLASS,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: null,
                lpStartupInfo: ref si,
                lpProcessInformation: out pi))
            {
                var wex = new Win32Exception();
                log.err($"CreateProcessW() lastErr={wex.NativeErrorCode}");
                throw wex;
            }
            if (!pi.hThread.Equals(IntPtr.Zero))
            {
                CloseHandle(pi.hThread);
            }

            return pi;
        }

        private static void CreatePipeAsyncReadSyncWrite(
            out NamedPipeServerStream ReadPipe,
            out NamedPipeClientStream WritePipe)
        {
            
            //string pipename = $"\\\\.\\Pipe\\forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";
            string pipename = $"forpNet.{g_currProcId}.{Interlocked.Increment(ref g_PipeSerialNumber)}";
            //log.dbg("creating named pipe [{0}]", pipename);

            int nSize = 4096;
            ReadPipe = new NamedPipeServerStream(
                pipename,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: nSize,
                outBufferSize: nSize,
                pipeSecurity: null,
                inheritability: HandleInheritability.None);
            
            WritePipe = new NamedPipeClientStream(
                serverName:         ".", // The name of the remote computer to connect to, or "." to specify the local computer.
                pipeName:           pipename,
                direction:          PipeDirection.Out,
                options:            PipeOptions.None,
                impersonationLevel: System.Security.Principal.TokenImpersonationLevel.None,
                inheritability:     HandleInheritability.Inheritable);

            WritePipe.Connect();            // basically a CreateFile() to the pipename
            ReadPipe.WaitForConnection();   // otherwise we get: "Pipe hasn't been connected yet."
        }
        #region DLLIMPORT
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcessW(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int whichHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
    
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetProcessTimes(IntPtr hProcess, 
            out System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime, 
            out System.Runtime.InteropServices.ComTypes.FILETIME lpExitTime, 
            out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
            out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);
        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFO
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
