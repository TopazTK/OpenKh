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

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, "mods", _fetchGameTarget);

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

            else
                return Path.Combine(_fetchConfigPath, _fetchGameTarget);
        }

        public static string ResolveBuild(Config input, bool barePath = false)
        {
            var _fetchConfigPath = input.Frontend.BuildPath;
            var _fetchTargetGame = input.Frontend.TargetGame;
            var _fetchGameTarget = barePath ? "" : Config.GameShorthand[_fetchTargetGame];

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, "build", _fetchGameTarget);

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

            else
                return Path.Combine(_fetchConfigPath, _fetchGameTarget);
        }

        public static string? ResolveData(Config input, bool barePath = false)
        {
            var _fetchConfigPath = input.Frontend.DataPath;
            var _fetchTargetGame = input.Frontend.TargetGame;
            var _fetchGameTarget = barePath ? "" : Config.GameShorthand[_fetchTargetGame];

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return null;

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, _fetchGameTarget);

            else
                return Path.Combine(_fetchConfigPath, _fetchGameTarget);
        }

        public static string? ResolveGame(Config input) => input.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? input.Frontend.GamePath[1] : input.Frontend.GamePath[0];
    }
}
