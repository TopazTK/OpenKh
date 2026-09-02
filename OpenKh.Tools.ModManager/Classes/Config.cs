using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpYaml;
using SharpYaml.Model;
using SharpYaml.Serialization;

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
        public string? ModPath { get; set; }
        public string? BuildPath { get; set; }
        public bool UpdateMods { get; set; }
        public string? DataPath { get; set; }
        public string[]? GamePath { 
            get; 
            set; }
    }

    public class Panacea
    {
        // Panacea Configuration
        public bool IsConsole { get; set; }
        public bool IsDebug { get; set; }
        public bool IsSoundDebug { get; set; }
        public bool IsCacheActive { get; set; }
    }

    public class Emulator
    {
        // PCSX2 Configuration
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
    }
}
