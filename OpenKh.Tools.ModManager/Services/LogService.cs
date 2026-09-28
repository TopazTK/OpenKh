using Avalonia.Controls.Documents;
using Avalonia.Threading;
using OpenKh.Tools.ModManager.Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

                var _fetchLog = "";
                var _colorHex = "";

                switch (type)
                {
                    case 0x00:
                        _fetchLog = String.Format(_stringFormat, DateTime.Now, message);
                        _colorHex = "#4CFCFC";
                        break;
                    case 0x01:
                        _fetchLog = String.Format(_stringFormat, DateTime.Now, message);
                        _colorHex = "#F0F040";
                        break;
                    case 0x02:
                        _fetchLog = String.Format(_stringFormat, DateTime.Now, message);
                        _colorHex = "#F04040";
                        break;
                }

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
