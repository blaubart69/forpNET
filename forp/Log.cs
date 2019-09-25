using System;
using System.ComponentModel;
using System.IO;

namespace Spi
{
    public class Log
    {
        public enum LEVEL
        {
            ERROR = 0,
            WARNING = 1,
            INFO = 2,
            DEBUG = 3
        }

        private static readonly char[] LogPrefix = new char[] { 'E', 'W', 'I', 'D' };
        private static readonly object ConsoleWriteLock = new object();

        private LEVEL       _currentLevel;
        //private TextWriter  _LogFileWriter = null;

        #region Constructor
        private Log(LEVEL level)
        {
            _currentLevel = level;
            //_LogFileWriter = LogWriter;
        }
        #endregion
        public void SetLogLevel(LEVEL level)
        {
            this._currentLevel = level;
        }
        #region Logging
        public void dbgKeyVal(string key, object value)
        {
            if (SkipLog(LEVEL.DEBUG)) return;
            this.dbg("{0}:\t[{1}]", key, value);
        }
        public void dbg(string text)
        {
            if (SkipLog(LEVEL.DEBUG)) return;
            write(LEVEL.DEBUG, 'D', text);
        }
        public void dbg(string format, object arg1) 
        {
            if (SkipLog(LEVEL.DEBUG)) return;
            write(LEVEL.DEBUG, 'D', format, arg1); 
        }
        public void dbg(string format, object arg1, object arg2)
        {
            if (SkipLog(LEVEL.DEBUG)) return;
            write(LEVEL.DEBUG, 'D', format, arg1, arg2);
        }
        public void inf(string format, params object[] args) { write(LEVEL.INFO, 'I', format, args); }
        public void wrn(string format, params object[] args) { write(LEVEL.WARNING, 'W', format, args); }
        public void err(string format, params object[] args) { write(LEVEL.ERROR, 'E', format, args); }
        public void win32err(Win32Exception wex, string context)
        {
            err($"Win32Exeption: native rc: {wex.NativeErrorCode}, HResult: {wex.HResult}, {wex.Message}");
        }
        public void log(LEVEL level, string format, params object[] args)
        {
            char prefix;
            if ((int)level < LogPrefix.GetLowerBound(0) || (int)level > LogPrefix.GetUpperBound(0))
            {
                prefix = '-';
            }
            else
            {
                prefix = LogPrefix[(int)level];
            }

            write(level, prefix, format, args);
        }
        public void exception(Exception ex)
        {
            string exLines =

              BuildLine('X', ">>> exception")
            + '\n' + BuildLine('X', "MESSAGE: [{0}]", ex.Message)
            + '\n' + BuildLine('X', "TYPE:    [{0}]", ex.GetType().ToString())
            + '\n' + BuildLine('X', "STACKTRACE:\n{0}", ex.StackTrace);

            if (ex.InnerException != null && ex.InnerException.Message != null)
            {
                exLines +=
                  '\n' + BuildLine('X', "\t>>> inner exception ")
                + '\n' + BuildLine('X', "\tMESSAGE: [{0}]", ex.InnerException.Message)
                + '\n' + BuildLine('X', "\tTYPE:    [{0}]", ex.InnerException.GetType().ToString())
                + '\n' + BuildLine('X', "\tSTACKTRACE:\n{0}", ex.InnerException.StackTrace)
                + '\n' + BuildLine('X', "\t<<< inner exception ");
            }
            exLines +=
            '\n' + BuildLine('X', "<<< exception");

            WriteConsoleColored(LEVEL.ERROR, exLines);
        }
        public void aggroException(AggregateException ex)
        {
            var newex = ex.Flatten();
            exception(newex);
        }

        private void write(LEVEL level, char prefix, string format, params object[] args)
        {
            if (level > this._currentLevel)
            {
                return;
            }

            string line = BuildLine(prefix, format, args);
            WriteConsoleColored(level, line);
        }
        #endregion
        private static string BuildLine(char prefix, string format, params object[] args)
        {
            string line = String.Format("{0:yyyy-MM-dd HH:mm:ss} {1} ", DateTime.Now, prefix);
            if (args == null)
            {
                line += format;
            }
            else
            {
                line += String.Format(format, args);
            }

            return line;
        }
        private static void WriteConsoleColored(LEVEL level, string line)
        {
            lock (ConsoleWriteLock)
            {
                var ColorBefore = Console.ForegroundColor;

                try
                {
                    TextWriter ConsoleWriter;

                    switch (level)
                    {
                        default:
                            ConsoleWriter = Console.Out;
                            break;
                        case LEVEL.WARNING:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            ConsoleWriter = Console.Out;
                            break;
                        case LEVEL.ERROR:
                            Console.ForegroundColor = ConsoleColor.Red;
                            ConsoleWriter = Console.Error;
                            break;
                    }

                    ConsoleWriter.WriteLine(line);

                }
                catch (Exception Lex)
                {
                    Console.Error.WriteLine("Exception occured writing a logline");
                    Console.Error.WriteLine(Lex.Message);
                    Console.Error.WriteLine(Lex.StackTrace);
                }
                finally
                {
                    Console.ForegroundColor = ColorBefore;
                }
            }
        }
        private bool SkipLog(LEVEL level)
        {
            return level > this._currentLevel;
        }
        #region STATIC
        private static readonly object InitLock = new object();
        private static readonly object GetLock = new object();
        private static Log Logger = null;

        public static Log GetLogger()
        {
            lock (GetLock)
            {
                if (Logger == null)
                {
                    SetLevel(level: LEVEL.INFO);
                }
            }
            return Logger;
        }
        public static void SetLevel(LEVEL level)
        {
            lock (InitLock)
            {
                if (Logger == null)
                {
                    Logger = new Log(level);
                }
                else
                {
                    //Logger._LogFileWriter = LogWriter;
                    Logger._currentLevel = level;
                }
            }
        }
        #endregion
    }
}