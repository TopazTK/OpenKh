using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenKh.Tools.ModManager.Classes;

namespace OpenKh.Tools.ModManager.Services
{
    public static class PathService
    {
        public static string ResolveMod(Config input, bool barePath = false)
        {
            var _fetchConfigPath = input.Frontend.ModPath;
            var _fetchTargetGame = input.Frontend.TargetGame;
            var _fetchGameTarget = barePath ? "" : Config.GameShorthand[_fetchTargetGame];

            var _fetchTargetPath = Path.Combine(AppContext.BaseDirectory, "mods", _fetchGameTarget);

            if (!String.IsNullOrEmpty(_fetchConfigPath))
            {
                if (!Path.IsPathFullyQualified(_fetchConfigPath))
                    _fetchTargetPath = Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

                else
                    _fetchTargetPath = Path.Combine(_fetchConfigPath, _fetchGameTarget);
            }

            if (!Directory.Exists(_fetchTargetPath))
                Directory.CreateDirectory(_fetchTargetPath);

            return _fetchTargetPath;
        }

        public static string ResolveBuild(Config input, bool barePath = false)
        {
            var _fetchConfigPath = input.Frontend.BuildPath;
            var _fetchTargetGame = input.Frontend.TargetGame;
            var _fetchGameTarget = barePath ? "" : Config.GameShorthand[_fetchTargetGame];

            var _fetchTargetPath = Path.Combine(AppContext.BaseDirectory, "build", _fetchGameTarget);

            if (!String.IsNullOrEmpty(_fetchConfigPath))
            {
                if (!Path.IsPathFullyQualified(_fetchConfigPath))
                    _fetchTargetPath = Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

                else
                    _fetchTargetPath = Path.Combine(_fetchConfigPath, _fetchGameTarget);
            }

            if (!Directory.Exists(_fetchTargetPath))
                Directory.CreateDirectory(_fetchTargetPath);

            return _fetchTargetPath;
        }

        public static string? ResolveData(Config input, bool barePath = false)
        {
            var _fetchConfigPath = input.Frontend.DataPath;
            var _fetchTargetGame = input.Frontend.TargetGame;
            var _fetchGameTarget = barePath ? "" : Config.GameShorthand[_fetchTargetGame];

            string? _fetchTargetPath = null;

            if (!String.IsNullOrEmpty(_fetchConfigPath))
            {
                if (!Path.IsPathFullyQualified(_fetchConfigPath))
                    _fetchTargetPath = Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

                else
                    _fetchTargetPath = Path.Combine(_fetchConfigPath, _fetchGameTarget);

                if (!Directory.Exists(_fetchTargetPath))
                    Directory.CreateDirectory(_fetchTargetPath);

                return _fetchTargetPath;
            }

            else
                return null;
        }

        public static string ResolvePath1525(Config input)
        {
            var _fetchGamePath = input.Frontend.GamePath;

            if (_fetchGamePath == null)
                return "";

            var _fetchTargetGame = _fetchGamePath[0];

            if (String.IsNullOrEmpty(_fetchTargetGame))
                return "";
            
            return Path.GetFullPath(_fetchTargetGame);
        }

        public static string ResolvePath28(Config input)
        {
            var _fetchGamePath = input.Frontend.GamePath;

            if (_fetchGamePath == null)
                return "";

            var _fetchTargetGame = _fetchGamePath[1];

            if (String.IsNullOrEmpty(_fetchTargetGame))
                return "";
            
            return Path.GetFullPath(_fetchTargetGame);
        }

        public static string? ResolveGame(Config input) => input.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? input.Frontend.GamePath[1] : input.Frontend.GamePath[0];
    }
}
