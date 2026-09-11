using HarfBuzzSharp;
using OpenKh.Tools.ModManager.Classes;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static bool? ResolveRegionJP(Config input)
        {
            var _fetchGamePath = ResolveGame(input);

            if (input.Frontend.TargetPlatform == Platform.STEAM)
            {
                var _targetStr = input.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? "KH2.8_system_WW.png" : "KH1.5+2.5_system_WW.png";
                var _targetExec = Path.Combine(_fetchGamePath, input.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? "KINGDOM HEARTS HD 2.8 Launcher.exe" : "KINGDOM HEARTS HD 1.5+2.5 Launcher.exe");

                var _fetchFileRAW = File.ReadAllBytes(_targetExec);

                var _fetchFileSpan = _fetchFileRAW.AsSpan(0, _fetchFileRAW.Length);
                var _fetchStrBytes = Encoding.UTF8.GetBytes(_targetStr);

                if (_fetchFileSpan.IndexOf(_fetchStrBytes) >= 0x00)
                    return false;

                else
                    return true;
            }

            else if (input.Frontend.TargetPlatform == Platform.EPIC_GAMES_STORE)
            {
                var _checkPathJP = Path.Combine(_fetchGamePath, "Image", "jp");
                var _checkPathEN = Path.Combine(_fetchGamePath, "Image", "en");

                if (Directory.Exists(_checkPathJP) && !Directory.Exists(_checkPathEN))
                    return true;

                else
                    return false;
            }

            else
                return null;
        }
    }
}
