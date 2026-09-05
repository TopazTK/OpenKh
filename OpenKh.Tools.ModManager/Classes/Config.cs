using OpenKh.Tools.ModManager.Services;
using SharpYaml;
using SharpYaml.Model;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenKh.Tools.ModManager.Classes
{
    public enum Platform : int
    {
        PCSX2,
        STEAM,
        EPIC_GAMES_STORE
    };

    public enum BuildType : int
    {
        PATCH,
        PANACEA
    };

    public enum Game : int
    {
        KINGDOM_HEARTS,
        KINGDOM_HEARTS_II,
        CHAIN_OF_MEMORIES,
        BIRTH_BY_SLEEP,
        DREAM_DROP_DISTANCE
    };

    // Frontend Configuration
    public class Frontend
    {
        public Platform TargetPlatform { get; set; }

        public Game TargetGame { get; set; }

        public BuildType ModBuildType { get; set; }

        public string? LaunchArguments { get; set; }

        public string? ModPath { get; set; }

        public string? BuildPath { get; set; }

        public bool UpdateMods { get; set; }

        public string? DataPath { get; set; }

        public string[]? GamePath { 
            get; 
            set; }
    }

    // Panacea Configuration
    public class Panacea
    {
        public bool IsConsole { get; set; }

        public bool IsDebug { get; set; }

        public bool IsSoundDebug { get; set; }

        public bool IsCacheActive { get; set; }
    }

    // PCSX2 Configuration
    public class Emulator
    {
        public string[]? EmuPath { get; set; }

        public string[]? RomPath { get; set; }
    }

    public class Config
    {
        public static Dictionary<Game, string> GameExecutable = new Dictionary<Game, string>()
        {
            { Game.KINGDOM_HEARTS, "KINGDOM HEARTS FINAL MIX.exe" },
            { Game.KINGDOM_HEARTS_II, "KINGDOM HEARTS II FINAL MIX.exe" },
            { Game.CHAIN_OF_MEMORIES, "KINGDOM HEARTS Re_Chain of Memories.exe" },
            { Game.BIRTH_BY_SLEEP, "KINGDOM HEARTS Birth by Sleep FINAL MIX.exe" },
            { Game.DREAM_DROP_DISTANCE, "KINGDOM HEARTS Dream Drop Distance.exe" },
        };

        public static Dictionary<Game, string> GameShorthand = new Dictionary<Game, string>()
        {
            { Game.KINGDOM_HEARTS, "kh1" },
            { Game.KINGDOM_HEARTS_II, "kh2" },
            { Game.CHAIN_OF_MEMORIES, "com" },
            { Game.BIRTH_BY_SLEEP, "bbs" },
            { Game.DREAM_DROP_DISTANCE, "ddd" },
        };

        public Frontend Frontend { get; set; }
        public Panacea Panacea { get; set; }
        public Emulator Emulator { get; set; }

        public static Config Load()
        {
            var _fetchConfigFile = System.IO.Path.Combine(AppContext.BaseDirectory, "config.yml");
            var _fetchConfigRAW = File.ReadAllText(_fetchConfigFile);

            return YamlSerializer.Deserialize<Config>(_fetchConfigRAW);
        }

        public bool IsValid()
        {
            if (Frontend.TargetPlatform != Platform.PCSX2)
            {
                var _fetchTargetGame = Frontend.TargetGame;

                // Check if the game is Dream Drop Distance and the second Game Path has been declared.
                // Otherwise, check if the game is NOT Dream Drop Distance.
                // If neither of these are true, the config isn't valid.

                var _isDDDConfigValid = _fetchTargetGame != Game.DREAM_DROP_DISTANCE || (_fetchTargetGame == Game.DREAM_DROP_DISTANCE && Frontend.GamePath.Length < 2);

                if (!_isDDDConfigValid)
                    return false;

                else
                {
                    // Fetch the second Game Path if the game is Dream Drop Distance, fetch the first one otherwise.
                    var _fetchGamePath = _fetchTargetGame == Game.DREAM_DROP_DISTANCE ? PathService.ResolvePath28(this) : PathService.ResolvePath1525(this);

                    if (String.IsNullOrEmpty(_fetchGamePath))
                        return false;

                    else
                    {
                        // Construct the paths for the game executable and Panacea.
                        var _fetchExePath = System.IO.Path.Combine(_fetchGamePath, Config.GameExecutable[Frontend.TargetGame]);
                        var _fetchSettingsPath = System.IO.Path.Combine(_fetchGamePath, "panacea_settings.txt");
                        var _fetchPanaceaPath = System.IO.Path.Combine(_fetchGamePath, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

                        // Verify the game executable and directory exists as configured. If the build type is PANACEA, also verify Panacea's existence.
                        var _isGameConfigValid = Directory.Exists(_fetchGamePath) && File.Exists(_fetchExePath);
                        var _isPanaceaConfigValid = (Frontend.ModBuildType == BuildType.PANACEA && File.Exists(_fetchPanaceaPath)) || Frontend.ModBuildType == BuildType.PATCH;

                        if (_isPanaceaConfigValid && Frontend.ModBuildType == BuildType.PANACEA)
                        {
                            var _regexModPath = new Regex("mod_path=(.*)");

                            if (File.Exists(_fetchPanaceaPath) && File.Exists(_fetchSettingsPath))
                            {
                                var _fetchSettingsRAW = File.ReadAllLines(_fetchSettingsPath);
                                var _fetchConfigPath = _fetchSettingsRAW.FirstOrDefault(x => _regexModPath.IsMatch(x));

                                if (_fetchConfigPath != null)
                                {
                                    var _fetchMatch = _regexModPath.Match(_fetchConfigPath);
                                    var _fetchValue = _fetchMatch.Groups[1].Value.Replace("\"", "");

                                    var _fetchConfigValue = System.IO.Path.GetFullPath(_fetchValue);

                                    var _fetchManagerPath = PathService.ResolveBuild(this, true);
                                    var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                                    if (!String.Equals(_fetchConfigValue, _fetchManagerPath, _comparisonRules))
                                        _isPanaceaConfigValid = false;
                                }
                            }
                        }

                        // If either are not valid, mark the config as faulty.
                        if (!_isGameConfigValid || !_isPanaceaConfigValid)
                            return false;

                        else
                            return true;
                    }
                }
            }

            // TODO: EMULATOR Configuration Check.

            else
                return false;
        }

        public void Commit()
        {
            var _fetchConfigFile = System.IO.Path.Combine(AppContext.BaseDirectory, "config.yml");
            var _fetchSerialize = YamlSerializer.Serialize(this);

            File.WriteAllText(_fetchConfigFile, _fetchSerialize);
        }
    }
}
