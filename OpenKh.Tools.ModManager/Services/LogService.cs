using System;

using Avalonia.Threading;
using Avalonia.Controls.Documents;

using OpenKh.Tools.ModManager.Views;

namespace OpenKh.Tools.ModManager.Services
{
    public static class LogService
    {
        static LogView? _logView = new LogView();
        static readonly object _lockObj = new object();

        public static void Show()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _logView.Loggers.Inlines.Clear();
                _logView.Show();
            });
        }

        public static void Dismiss()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _logView.Loggers.Inlines.Clear();
                _logView.Hide();
            });
        }

        public static void Log(string message, byte type)
        {
            lock (_lockObj)
            {
                var _stringFormat = "[{0}] - {1}\n";
                var _fetchDate = DateTime.Now.ToString("HH:mm:ss");

                var _fetchLog = String.Format(_stringFormat, _fetchDate, message);
                var _colorHex = type == 0x00 ? "#4CCCCC" : (type == 0x01 ? "#CCCC4C" : "#CC4C4C");

                Dispatcher.UIThread.Post(() =>
                {
                    Run _fetchInline = new Run
                    {
                        Text = _fetchLog,
                        Foreground = Avalonia.Media.Brush.Parse(_colorHex)
                    };

                    _logView.Loggers.Inlines.Add(_fetchInline);
                    _logView.Scroller.ScrollToEnd();
                });
            }
        }
    }
}
