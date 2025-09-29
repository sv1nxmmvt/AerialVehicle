using System;
using System.Windows.Forms;

namespace Sender
{
    public class Logger
    {
        private readonly RichTextBox _logControl;
        private readonly Action<string> _uiUpdateAction;

        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Success,
            Debug
        }

        public Logger(RichTextBox logControl)
        {
            _logControl = logControl ?? throw new ArgumentNullException(nameof(logControl));
            _uiUpdateAction = AppendToLog;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (_logControl.InvokeRequired)
            {
                _logControl.Invoke(_uiUpdateAction, FormatMessage(message, level));
            }
            else
            {
                AppendToLog(FormatMessage(message, level));
            }
        }

        public void LogInfo(string message) => Log(message, LogLevel.Info);
        public void LogWarning(string message) => Log(message, LogLevel.Warning);
        public void LogError(string message) => Log(message, LogLevel.Error);
        public void LogSuccess(string message) => Log(message, LogLevel.Success);
        public void LogDebug(string message)
        {
            if (Config.ENABLE_DETAILED_LOGGING)
            {
                Log(message, LogLevel.Debug);
            }
        }

        private string FormatMessage(string message, LogLevel level)
        {
            string prefix = level switch
            {
                LogLevel.Info => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                LogLevel.Success => "[OK]",
                LogLevel.Debug => "[DEBUG]",
                _ => "[LOG]"
            };

            return $"[{DateTime.Now:HH:mm:ss.fff}] {prefix} {message}";
        }

        private void AppendToLog(string message)
        {
            try
            {
                _logControl.AppendText(message + Environment.NewLine);
                _logControl.ScrollToCaret();

                if (_logControl.Lines.Length > Config.MAX_LOG_LINES)
                {
                    var lines = _logControl.Lines;
                    var keepLines = Config.MAX_LOG_LINES / 2;
                    var newLines = new string[keepLines];
                    Array.Copy(lines, lines.Length - keepLines, newLines, 0, keepLines);
                    _logControl.Lines = newLines;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger error: {ex.Message}");
            }
        }

        public void Clear()
        {
            if (_logControl.InvokeRequired)
            {
                _logControl.Invoke(new Action(_logControl.Clear));
            }
            else
            {
                _logControl.Clear();
            }
        }
    }
}