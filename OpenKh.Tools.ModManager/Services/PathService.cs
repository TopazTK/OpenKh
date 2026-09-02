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
        public static string ResolveMod(Config input)
        {
            var _fetchConfigPath = input.Frontend.ModPath;
            var _fetchTargetGame = input.Frontend.TargetGame;

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, "mods", Config.GameShorthand[_fetchTargetGame]);

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);

            else
                return Path.Combine(_fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);
        }

        public static string ResolveBuild(Config input)
        {
            var _fetchConfigPath = input.Frontend.BuildPath;
            var _fetchTargetGame = input.Frontend.TargetGame;

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, "build", Config.GameShorthand[_fetchTargetGame]);

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);

            else
                return Path.Combine(_fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);
        }

        public static string? ResolveData(Config input)
        {
            var _fetchConfigPath = input.Frontend.DataPath;
            var _fetchTargetGame = input.Frontend.TargetGame;

            if (String.IsNullOrEmpty(_fetchConfigPath))
                return null;

            else if (!Path.IsPathFullyQualified(_fetchConfigPath))
                return Path.Combine(AppContext.BaseDirectory, _fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);

            else
                return Path.Combine(_fetchConfigPath, Config.GameShorthand[_fetchTargetGame]);
        }

        public static string? ResolveGame(Config input) => input.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? input.Frontend.GamePath[1] : input.Frontend.GamePath[0];
    }
}
