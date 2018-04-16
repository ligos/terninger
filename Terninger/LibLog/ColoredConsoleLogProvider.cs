namespace MurrayGrant.Terninger.LibLog
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public class ColoredConsoleLogProvider : ILogProvider
    {
        private readonly bool _LogToTraceOut;
        private readonly LogLevel _MinLogLevel;

        public ColoredConsoleLogProvider() : this(LogLevel.Trace, false) { }
        public ColoredConsoleLogProvider(bool logToTrace) : this(LogLevel.Trace, logToTrace) { }
        public ColoredConsoleLogProvider(LogLevel minLogLevel) : this(minLogLevel, false) { }
        public ColoredConsoleLogProvider(LogLevel minLogLevel, bool logToTrace)
        {
            _MinLogLevel = minLogLevel;
            _LogToTraceOut = logToTrace;
        }

        // This relies on the int value of LogLevel.
        private static readonly ConsoleColor[] Colors = new []
            {
                ConsoleColor.DarkGray,  // Trace
                ConsoleColor.Gray,      // Debug
                ConsoleColor.White,     // Info
                ConsoleColor.Magenta,   // Warn
                ConsoleColor.Yellow,    // Error
                ConsoleColor.Red,       // Fatal
            };

        public Logger GetLogger(string name)
        {
            return (logLevel, messageFunc, exception, formatParameters) =>
            {
                if (logLevel < _MinLogLevel)
                    return false;   // Log level disabled.
                if (messageFunc == null)
                    return true;    // All log levels are enabled

                try
                {
                    ConsoleColor consoleColor = Colors[(int)logLevel];
                    var originalForground = Console.ForegroundColor;
                    try
                    {
                        Console.ForegroundColor = consoleColor;
                        WriteMessage(logLevel, name, messageFunc, formatParameters, exception);
                    }
                    finally
                    {
                        Console.ForegroundColor = originalForground;
                    }
                } catch (IndexOutOfRangeException) { 
                    WriteMessage(logLevel, name, messageFunc, formatParameters, exception);
                }

                return true;
            };
        }

        private void WriteMessage(
            LogLevel logLevel,
            string name,
            Func<string> messageFunc,
            object[] formatParameters,
            Exception exception)
        {
            var message = messageFunc();
            if (formatParameters != null && formatParameters.Length > 0)
                message = string.Format(CultureInfo.InvariantCulture, message, formatParameters);
            if (exception != null)
            {
                message = message + "|" + exception;
            }
            var line = String.Format("{0:HH:mm:ss} | {1} | {2} | {3}", DateTime.Now, logLevel.ToString().ToUpper(), name, message);
            Console.WriteLine(line);
            if (_LogToTraceOut)
                System.Diagnostics.Trace.WriteLine(line);
        }

        public IDisposable OpenNestedContext(string message)
        {
            return NullDisposable.Instance;
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            return NullDisposable.Instance;
        }
        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return NullDisposable.Instance;
        }

        private class NullDisposable : IDisposable
        {
            internal static readonly IDisposable Instance = new NullDisposable();

            public void Dispose()
            { }
        }
    }
}