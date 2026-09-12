using Avalonia.Controls;
using OpenKh.Tools.ModManager.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Services
{
    public static class DialogService
    {
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
                return _fetchDialog.Result;
            }
        }
    }
}
