namespace MurrayGrant.Terninger.LibLog
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public class ColoredConsoleLogProvider : ILogProvider
    {
        private readonly bool _LogToTraceOut;
        public ColoredConsoleLogProvider() : this(false) { }
        public ColoredConsoleLogProvider(bool logToTrace)
        {
            _LogToTraceOut = logToTrace;
        }

        private static readonly Dictionary<LogLevel, ConsoleColor> Colors = new Dictionary<LogLevel, ConsoleColor>
            {
                {LogLevel.Fatal, ConsoleColor.Red},
                {LogLevel.Error, ConsoleColor.Yellow},
                {LogLevel.Warn, ConsoleColor.Magenta},
                {LogLevel.Info, ConsoleColor.White},
                {LogLevel.Debug, ConsoleColor.Gray},
                {LogLevel.Trace, ConsoleColor.DarkGray},
            };

        public Logger GetLogger(string name)
        {
            return (logLevel, messageFunc, exception, formatParameters) =>
            {
                if (messageFunc == null)
                {
                    return true; // All log levels are enabled
                }

                ConsoleColor consoleColor;
                if (Colors.TryGetValue(logLevel, out consoleColor))
                {
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
                }
                else
                {
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
            var message = string.Format(CultureInfo.InvariantCulture, messageFunc(), formatParameters);
            if (exception != null)
            {
                message = message + "|" + exception;
            }
            var line = String.Format("{0} | {1} | {2} | {3}", DateTime.UtcNow, logLevel, name, message);
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