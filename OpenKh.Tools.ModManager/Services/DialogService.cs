#pragma warning disable CS4014 

using Avalonia.Controls;
using Avalonia.Threading;
using Octokit;
using OpenKh.Tools.ModManager.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Services
{
    public static class DialogService
    {
        public async static Task<bool> ShowProgress(Window? owner, string title, string message, CancellationTokenSource cancelToken, IntPtr firstProgCurr, IntPtr firstProgMax, IntPtr? firstProgText = null, IntPtr? secondProgCurr = null, IntPtr? secondProgMax = null, IntPtr? secondProgText = null)
        {
            var _fetchDialog = new ProgressDialog 
            { 
                Title = title, 
                Message = message
            };

            Task.Run(async () =>
            {
                while (true)
                {
                    var _fetchCurrent = Marshal.ReadInt32(firstProgCurr);
                    var _fetchMaximum = Marshal.ReadInt32(firstProgMax);

                    Dispatcher.UIThread.Post(() =>
                    {
                        _fetchDialog.MainProgress.Maximum = _fetchMaximum;
                        _fetchDialog.MainProgress.Value = _fetchCurrent;

                        if (firstProgText != null)
                        {
                            var _fetchValue = new byte[0x100];
                            Marshal.Copy(firstProgText.Value, _fetchValue, 0, 0x100);

                            _fetchDialog.MainProgress.ShowProgressText = true;
                            _fetchDialog.MainProgress.ProgressTextFormat = Encoding.Default.GetString(_fetchValue, 0x00, _fetchValue.IndexOf<byte>(0x00));
                        }

                        else
                            _fetchDialog.MainProgress.ShowProgressText = false;

                        if (secondProgCurr != null && secondProgMax != null)
                        {
                            var _fetchSecondCurrent = Marshal.ReadInt32(secondProgCurr.Value);
                            var _fetchSecondMaximum = Marshal.ReadInt32(secondProgMax.Value);

                            _fetchDialog.MiscProgress.Maximum = _fetchSecondMaximum;
                            _fetchDialog.MiscProgress.Value = _fetchSecondCurrent;

                            _fetchDialog.MiscProgress.IsVisible = true;
                        }

                        else
                            _fetchDialog.MiscProgress.IsVisible = false;

                        if (secondProgText != null)
                        {
                            var _fetchValue = new byte[0x100];
                            Marshal.Copy(secondProgText.Value, _fetchValue, 0, 0x100);

                            _fetchDialog.MiscProgress.ShowProgressText = true;
                            _fetchDialog.MiscProgress.ProgressTextFormat = Encoding.Default.GetString(_fetchValue, 0x00, _fetchValue.IndexOf<byte>(0x00));
                        }
                    });

                    if ((_fetchCurrent == _fetchMaximum && _fetchMaximum != 0) || cancelToken.IsCancellationRequested)
                    {
                        if ((secondProgCurr != null && secondProgMax != null) || cancelToken.IsCancellationRequested)
                        {
                            var _fetchSecondCurrent = Marshal.ReadInt32(secondProgCurr.Value);
                            var _fetchSecondMaximum = Marshal.ReadInt32(secondProgMax.Value);

                            if (_fetchSecondCurrent == _fetchSecondMaximum && _fetchSecondMaximum != 0)
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    _fetchDialog.Close(_fetchDialog.Result = true);
                                });

                                break;
                            }
                        }

                        else
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                _fetchDialog.Close(_fetchDialog.Result = true);
                            });

                            break;
                        }
                    }

                    await Task.Delay(5);
                }
            });

            if (owner != null)
            {
                var _fetchResult = await _fetchDialog.ShowDialog<bool>(owner);

                if (!_fetchResult)
                    cancelToken.Cancel();

                return _fetchResult;
            }

            else
            {
                _fetchDialog.Show();

                await Task.Run(() =>
                {
                    while (_fetchDialog.Result == null)
                    { }
                });

                if (!_fetchDialog.Result.Value)
                    cancelToken.Cancel();

                return _fetchDialog.Result.Value;
            }
        }

        public static async Task<bool> ShowQuestion(Window? owner, string title, string message)
        {
            var _fetchDialog = new QuestionDialog { Title = title };

            _fetchDialog.MainText.Text = message;

            if (owner != null)
                return await _fetchDialog.ShowDialog<bool>(owner);

            else
            {
                _fetchDialog.Show();

                await Task.Run(() =>
                {
                    while (_fetchDialog.Result == null)
                    { }
                });

                return _fetchDialog.Result.Value;
            }
        }

        public static async Task ShowMessage(Window? owner, string title, string message, MessageType type)
        {
            var _fetchDialog = new MessageDialog { Title = title, Type = type };

            _fetchDialog.MainText.Text = message;

            if (owner != null)
                await _fetchDialog.ShowDialog(owner);

            else
                _fetchDialog.Show();
        }

        public static async Task<string> ShowInput(Window? owner, string title, string message, string mainButtonText, string placeholderText, string? miscButtonMessage = null, Func<Window?, Task<string?>>? miscButtonCallback = null)
        {
            var _fetchDialog = new InputDialog { Title = title, MiscButtonCallback = miscButtonCallback };

            _fetchDialog.MainText.Text = message;
            _fetchDialog.AcceptButton.Content = mainButtonText;
            _fetchDialog.InputText.PlaceholderText = placeholderText;

            if (miscButtonMessage != null)
                _fetchDialog.MiscButton.Content = miscButtonMessage;

            if (owner != null)
                return await _fetchDialog.ShowDialog<string>(owner);

            else
            {
                _fetchDialog.Show();

                await Task.Run(() =>
                {
                    while (_fetchDialog.Result == null)
                    { }
                });

                return _fetchDialog.Result;
            }
        }
    }
}

#pragma warning restore CS4014
