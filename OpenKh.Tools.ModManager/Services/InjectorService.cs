using LibGit2Sharp;
using OpenKh.Common;
using OpenKh.Tools.Common;
using OpenKh.Tools.ModManager.Classes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xe.BinaryMapper;
using static Antlr4.Runtime.Atn.SemanticContext;
using static OpenKh.Kh2.Ard.AreaDataScript;
using static OpenKh.Kh2.Constants;

namespace OpenKh.Tools.ModManager.Services
{
    public class InjectorService
    {
        public static class Hooks
        {
            public static readonly uint[] LoadFileHook =
            [
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.SW(MIPS.A0, MIPS.T6, -0x08),
                MIPS.SW(MIPS.A1, MIPS.T6, -0x0C),
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V0, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.V0, MIPS.Zero, 0x02),
                MIPS.NOP(),
                MIPS.ADDIU(MIPS.RA, MIPS.RA, 4),
                MIPS.ADDIU(MIPS.SP, MIPS.SP, -0x10),
                MIPS.SD(MIPS.T4, MIPS.SP, 0x08),
                MIPS.SD(MIPS.S0, MIPS.SP, 0x00),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] GetFileSizeHook =
            [
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.SW(MIPS.A0, MIPS.T6, -0x08),
                MIPS.SW(MIPS.A1, MIPS.T6, -0x0C),
                MIPS.SW(MIPS.A2, MIPS.T6, -0x10),
                MIPS.SW(MIPS.A3, MIPS.T6, -0x14),
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V0, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.V0, MIPS.Zero, 2),
                MIPS.NOP(),
                MIPS.JR(MIPS.T4),
                MIPS.NOP(),
                MIPS.ADDIU(MIPS.SP, MIPS.SP, -0x10),
                MIPS.SD(MIPS.T4, MIPS.SP, 0x08),
                MIPS.SD(MIPS.S0, MIPS.SP, 0x00),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] LoadFileTaskHook =
            [
                // Input:
                // S0 DstPtr
                // S1 Filename
                // T4 return program counter
                // T5 Operation
                // V0 IdxFilePtr
                //
                // Work:
                // MIPS.T6 Hook stack
                // V0 Return value
                // 
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.SW(MIPS.S1, MIPS.T6, -0x08),
                // Filename
                MIPS.SW(MIPS.S0, MIPS.T6, -0x0C),
                // DstPtr
                MIPS.SW(MIPS.V0, MIPS.T6, -0x10),
                // LoadFileTask
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V1, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.V1, MIPS.Zero, 3),
                MIPS.MOVE(MIPS.S2, MIPS.V0),
                MIPS.BEQ(MIPS.Zero, MIPS.Zero, 2),
                MIPS.ADDIU(MIPS.RA, MIPS.RA, 0x98),
                // skip the remainder of the function
                MIPS.LI(MIPS.V0, -1),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] LoadFileTaskHookVanilla =
            [
                // Input:
                // S0 DstPtr
                // S1 Filename
                // T4 return program counter
                // T5 Operation
                // V0 IdxFilePtr
                //
                // Work:
                // MIPS.T6 Hook stack
                // V0 Return value
                // 
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.SW(MIPS.S1, MIPS.T6, -0x08),
                // Filename
                MIPS.SW(MIPS.S0, MIPS.T6, -0x0C),
                // DstPtr
                MIPS.SW(MIPS.V0, MIPS.T6, -0x10),
                // LoadFileTask
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V1, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.V1, MIPS.Zero, 3),
                MIPS.MOVE(MIPS.S2, MIPS.V0),
                MIPS.BEQ(MIPS.Zero, MIPS.Zero, 2),
                MIPS.ADDIU(MIPS.RA, MIPS.RA, 0x64),
                // skip the remainder of the function
                MIPS.LI(MIPS.V0, -1),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] LoadFileAsyncHook =
            [
                // Input:
                //
                // Work:
                //
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.LW(MIPS.T2, MIPS.V0, -0x2A10),
                MIPS.LW(MIPS.T3, MIPS.V0, -0x2A24),
                MIPS.ADDI(MIPS.T4, MIPS.V0, -0x29F8),
                MIPS.LW(MIPS.T0, MIPS.T4, 0),
                MIPS.ADDIU(MIPS.T1, MIPS.T3, 8),
                MIPS.LW(MIPS.T4, MIPS.T3, 0x38),
                MIPS.LW(MIPS.T4, MIPS.T4, 0),
                MIPS.SW(MIPS.T0, MIPS.T6, -0x08),
                // FileDirID
                MIPS.BEQ(MIPS.T2, MIPS.T4, 9),
                MIPS.SW(MIPS.T1, MIPS.T6, -0x0C),
                // FileNamePtr
                MIPS.ADDIU(MIPS.T4, MIPS.Zero, 0x03),
                MIPS.SW(MIPS.T4, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T4, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T4, 0x00, -2),
                MIPS.LW(MIPS.T4, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.T4, MIPS.Zero, 7),
                MIPS.NOP(),
                MIPS.BEQ(MIPS.Zero, MIPS.Zero, 8),
                MIPS.SW(MIPS.T2, MIPS.T6, -0x10),
                // MemDstPtr
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.T4, MIPS.T6, -0x08),
                MIPS.LUI(MIPS.V1, 0x5B),
                // For Fallback
                MIPS.BEQ(MIPS.T4, MIPS.Zero, 5),
                MIPS.LW(MIPS.A2, MIPS.V0, -0x29F4),
                MIPS.ADD(MIPS.T2, MIPS.T2, MIPS.T4),
                MIPS.SW(MIPS.T2, MIPS.V0, -0x2A10),
                MIPS.ADDIU(MIPS.V0, MIPS.Zero, 1),
                MIPS.ADDIU(MIPS.RA, MIPS.RA, 0x64),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] GetFileSizeRecomHook =
            [
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.LUI(MIPS.V1, 0x5C),
                MIPS.ADDI(MIPS.T4, MIPS.V1, -0x29F8),
                MIPS.LW(MIPS.T4, MIPS.T4, 0),
                MIPS.SW(MIPS.T4, MIPS.T6, -0x08),
                // FileDirID
                MIPS.SW(MIPS.S2, MIPS.T6, -0x0C),
                // FileNamePtr
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V1, MIPS.T6, -0x08),

                // For Fallback
                MIPS.BNE(MIPS.V1, MIPS.Zero, 2),
                MIPS.NOP(),
                MIPS.LW(MIPS.V1, MIPS.S2, 0x18),
                MIPS.JR(MIPS.RA),
                MIPS.SW(MIPS.V1, MIPS.S1, 0x28),
            ];

            public static readonly uint[] LoadFileAsyncHookJp =
            [
                // Input:
                //
                // Work:
                //
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.LW(MIPS.T2, MIPS.V0, 0x18B0),
                MIPS.LW(MIPS.T3, MIPS.V0, 0x189C),
                MIPS.ADDI(MIPS.T4, MIPS.V0, 0x18C8),
                MIPS.LW(MIPS.T0, MIPS.T4, 0),
                MIPS.ADDIU(MIPS.T1, MIPS.T3, 8),
                MIPS.LW(MIPS.T4, MIPS.T3, 0x38),
                MIPS.LW(MIPS.T4, MIPS.T4, 0),
                MIPS.SW(MIPS.T0, MIPS.T6, -0x08),
                // FileDirID
                MIPS.BEQ(MIPS.T2, MIPS.T4, 9),
                MIPS.SW(MIPS.T1, MIPS.T6, -0x0C),
                // FileNamePtr
                MIPS.ADDIU(MIPS.T4, MIPS.Zero, 0x03),
                MIPS.SW(MIPS.T4, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T4, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T4, 0x00, -2),
                MIPS.LW(MIPS.T4, MIPS.T6, -0x08),
                MIPS.BEQ(MIPS.T4, MIPS.Zero, 7),
                MIPS.NOP(),
                MIPS.BEQ(MIPS.Zero, MIPS.Zero, 8),
                MIPS.SW(MIPS.T2, MIPS.T6, -0x10),
                // MemDstPtr
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.T4, MIPS.T6, -0x08),
                MIPS.LUI(MIPS.V1, 0x5B),
                // For Fallback
                MIPS.BEQ(MIPS.T4, MIPS.Zero, 5),
                MIPS.LW(MIPS.A2, MIPS.V0, 0x18CC),
                MIPS.ADD(MIPS.T2, MIPS.T2, MIPS.T4),
                MIPS.SW(MIPS.T2, MIPS.V0, 0x18B0),
                MIPS.ADDIU(MIPS.V0, MIPS.Zero, 1),
                MIPS.ADDIU(MIPS.RA, MIPS.RA, 0x64),
                MIPS.JR(MIPS.RA),
                MIPS.NOP(),
            ];

            public static readonly uint[] GetFileSizeRecomHookJp =
            [
                MIPS.LUI(MIPS.T6, 0x0F),
                MIPS.LUI(MIPS.V1, 0x5C),
                MIPS.ADDI(MIPS.T4, MIPS.V1, 0x18C8),
                MIPS.LW(MIPS.T4, MIPS.T4, 0),
                MIPS.SW(MIPS.T4, MIPS.T6, -0x08),
                // FileDirID
                MIPS.SW(MIPS.S2, MIPS.T6, -0x0C),
                // FileNamePtr
                MIPS.SW(MIPS.T5, MIPS.T6, -0x04),
                // Operation
                MIPS.LW(MIPS.T5, MIPS.T6, -0x04),
                MIPS.BNE(MIPS.T5, 0x00, -2),
                MIPS.LW(MIPS.V1, MIPS.T6, -0x08),

                // For Fallback
                MIPS.BNE(MIPS.V1, MIPS.Zero, 2),
                MIPS.NOP(),
                MIPS.LW(MIPS.V1, MIPS.S2, 0x18),
                MIPS.JR(MIPS.RA),
                MIPS.SW(MIPS.V1, MIPS.S1, 0x28),
            ];
        }

        public static Dictionary<Game, string> ISO_NAMES = new Dictionary<Game, string>
        {
            { Game.KINGDOM_HEARTS, "KHFM.iso"},
            { Game.KINGDOM_HEARTS_II, "KHIIFM.iso"},
        };

        private static readonly HashSet<string> DENY_FILES = new HashSet<string>
        {
            "dkmovie.x",
            "dktitle.x",
            "gb.x",
            "wm.x",
            "xl_limit.x",
            "xs_bambi.x",
            "xs_dh_break.x",
            "xs_dumbo.x",
            "xs_genie.x",
            "xs_mushu.x",
            "xs_simba.x",
            "xs_tink.x",

            "ovl_title.x",
            "ovl_shop.x",
            "ovl_movie.x",
            "ovl_gumibattle.x",
            "ovl_gumimenu.x",
            "ovl_gumimenu.x",
        };

        Process _targetProcess;

        uint _hookPtr;
        uint _nextHookPtr = 0xFFF00;

        byte[] _loadFuncArr = Hooks.LoadFileHook.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] _getSizeFuncArr = Hooks.GetFileSizeHook.SelectMany(BitConverter.GetBytes).ToArray();

        CancellationTokenSource _cancelSource;
        CancellationToken _cancelToken;

        uint _loadFileFunc = 0x1682b8;
        uint _getFileSizeFunc = 0x1AE1B0;
        uint _subFileSizeFunc = 0x1AE460;

        uint _bufferPtr = 0x350E88;
        uint _regionPtr = 0x33CAF0;
        uint _languagePtr = 0x33CAF4;

        public Config TargetConfig { get; set; }

        bool ResolvePath(string input, out string? finalPath)
        {
            Console.WriteLine($"Resolving file: {input}...");

            var _fetchBuildPath = PathService.ResolveBuild(TargetConfig);
            var _fetchDataPath = PathService.ResolveData(TargetConfig);

            var _fetchRegionList = Kh2.Constants.Regions;
            var _fetchFileRegion = _fetchRegionList.FirstOrDefault(x => input.Contains($"/{x}/") || input.EndsWith($".a.{x}"));

            var _fetchRegionInput = input.Replace($"/{_fetchFileRegion}/", $"/fm/")
                                         .Replace($".a.{_fetchFileRegion}", $".a.fm")
                                         .Replace($".apdx", $".a.fm");


            if (DENY_FILES.Contains(input))
            {
                Console.WriteLine($"Cannot resolve file \"{input}\" as it is on the forbidden list! Skipping...");

                finalPath = null;
                return false;
            }

            var _fetchBuildFile = Path.Combine(_fetchBuildPath, input);
            var _fetchBuildFileRegion = Path.Combine(_fetchBuildPath, _fetchRegionInput);

            if (File.Exists(_fetchBuildFile))
            {
                finalPath = _fetchBuildFile;
                return true;
            }

            else if (File.Exists(_fetchBuildFileRegion))
            {
                finalPath = _fetchBuildFileRegion;
                return true;
            }

            var _fetchDataFile = Path.Combine(_fetchDataPath, input);
            var _fetchDataFileRegion = Path.Combine(_fetchDataPath, _fetchRegionInput);

            if (File.Exists(_fetchDataFile))
            {
                finalPath = _fetchDataFile;
                return true;
            }

            else if (File.Exists(_fetchDataFileRegion))
            {
                finalPath = _fetchDataFileRegion;
                return true;
            }

            Console.WriteLine($"Cannot resolve file \"{input}\" as it does not exist! Skipping...");

            finalPath = null;
            return false;
        }

        public int ResolveSize(string input)
        {
            if (ResolvePath(input, out var _filePath))
            {
                var _fetchInfo = new FileInfo(_filePath);
                return (int)_fetchInfo.Length;
            }

            return -1;
        }

        int WriteFile(Stream targetStream, string inputName)
        {
            var _couldResolve = ResolvePath(inputName, out var _filePath);

            if (_couldResolve)
            {
                Console.WriteLine($"Loading file from path: {_filePath}");

                return File.OpenRead(_filePath).Using(x =>
                {
                    x.CopyTo(targetStream, 512 * 1024);
                    return (int)x.Length;
                });
            }

            return 0x00;
        }

        public InjectorService(Config _targetConfig)
        {
            TargetConfig = _targetConfig;

            _cancelSource = new CancellationTokenSource();
            _cancelToken = _cancelSource.Token;

            Stream? _fetchStream = null;
            var _fetchPathEmu = PathService.ResolveEmulator(TargetConfig);

            if (OperatingSystem.IsWindows())
            {
                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                static extern bool AllocConsole();

                AllocConsole();
            }

            Console.WriteLine("=== OpenKH - Mod Manager | Dynamic Loader ===");
            Console.WriteLine("");

            _targetProcess = new Process
            {
                StartInfo = new ProcessStartInfo(_fetchPathEmu)
                {
                    Arguments = Path.Combine(TargetConfig.Emulator.RomPath[0], ISO_NAMES[TargetConfig.Frontend.TargetGame]),
                    WorkingDirectory = Path.GetDirectoryName(_fetchPathEmu),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            if (_targetProcess.Start())
            {
                if (!_fetchPathEmu.Contains("qt"))
                {
                    Console.WriteLine("Detected PCSX2 Build: Before v1.7.0 - Using Legacy Method.");

                    _fetchStream = new ProcessStream(_targetProcess, 0x20000000, 0x20000000);

                    Console.WriteLine("Latching to legacy build on 0x20000000...");
                }

                else
                {
                    Console.WriteLine("Detected PCSX2 Build: After 1.7.0 - Using Current Method.");

                    var _fetchProcessID = _targetProcess.Id;
                    var _fetchMemoryName = $"pcsx2_{_fetchProcessID}";

                    Console.WriteLine($"Searching for MemoryMap: {_fetchMemoryName}.");

                    Thread.Sleep(1000);

                    var _fetchMemoryMap = MemoryMappedFile.OpenExisting(_fetchMemoryName);

                    if (_fetchMemoryMap == null)
                    {
                        Console.WriteLine("MemoryMap not detected! Operation cannot continue! Aborting...");

                        _targetProcess.Kill();
                        return;
                    }

                    Console.WriteLine("MemoryMap latched! Creating stream...");
                    _fetchStream = _fetchMemoryMap.CreateViewStream();
                }

                if (_fetchStream == null)
                {
                    Console.WriteLine("Couldn't create stream! Operation cannot continue! Aborting...");

                    _targetProcess.Kill();
                    return;
                }

                Console.WriteLine("Stream created... Executing main loop...");
                Console.WriteLine("");

                Task.Run(async () =>
                {
                    while (!_cancelToken.IsCancellationRequested)
                    {
                        _fetchStream.SetPosition(_loadFileFunc);

                        if (_fetchStream.ReadUInt32() == 0x00)
                            continue;

                        _nextHookPtr = 0xFFF00;

                        if (_loadFileFunc > 0)
                        {
                            _hookPtr = _nextHookPtr;

                            if (_hookPtr != 0)
                            {
                                _fetchStream.SetPosition(_hookPtr);

                                if (_fetchStream.ReadUInt32() == 0x00)
                                {
                                    _fetchStream.SetPosition(_hookPtr);
                                    _fetchStream.Write(_loadFuncArr);
                                }

                                _nextHookPtr += (uint)_loadFuncArr.Length;
                            }

                            _fetchStream.SetPosition(_loadFileFunc);

                            uint[] _fetchFunction =
                            [
                                MIPS.ADDIU(MIPS.T4, MIPS.RA, 0),
                                MIPS.JAL(_hookPtr),
                                MIPS.ADDIU(MIPS.T5, MIPS.Zero, 0x01)
                            ];

                            foreach (var _fetchInst in _fetchFunction)
                                _fetchStream.Write(_fetchInst);
                        }

                        if (_getFileSizeFunc > 0)
                        {
                            _hookPtr = _nextHookPtr;

                            if (_hookPtr != 0)
                            {
                                _fetchStream.SetPosition(_hookPtr);

                                if (_fetchStream.ReadUInt32() == 0x00)
                                {
                                    _fetchStream.SetPosition(_hookPtr);
                                    _fetchStream.Write(_getSizeFuncArr);
                                }

                                _nextHookPtr += (uint)_getSizeFuncArr.Length;
                            }

                            _fetchStream.SetPosition(_getFileSizeFunc);

                            uint[] _fetchFunction =
                            [
                                MIPS.ADDIU(MIPS.T4, MIPS.RA, 0),
                            MIPS.JAL(_hookPtr),
                            MIPS.ADDIU(MIPS.T5, MIPS.Zero, 0x02),
                            MIPS.JAL(_subFileSizeFunc),
                            MIPS.NOP(),
                            MIPS.BEQ(MIPS.V0, MIPS.Zero, 2),
                            MIPS.NOP(),
                            MIPS.LW(MIPS.V0, MIPS.V0, 0x0C),
                            MIPS.LD(MIPS.RA, MIPS.SP, 0x08),
                            MIPS.JR(MIPS.RA),
                            MIPS.ADDIU(MIPS.SP, MIPS.SP, 0x10),
                            ];

                            foreach (var _fetchInst in _fetchFunction)
                                _fetchStream.Write(_fetchInst);
                        }

                        _fetchStream.Flush();

                        var _fetchOpcodeAddr = (0x0F << 0x10) - 0x04;
                        var _fetchOpcode = _fetchStream.SetPosition(_fetchOpcodeAddr).ReadInt32();

                        if (_fetchStream.Position == _fetchOpcode || _targetProcess.HasExited)
                            break;

                        switch (_fetchOpcode)
                        {
                            case 0x01:
                            {
                                _fetchStream.SetPosition(_fetchOpcodeAddr - 0x08);

                                var _destinationPtr = _fetchStream.ReadInt32();
                                var _fileNamePtr = _fetchStream.ReadInt32();

                                _fetchStream.SetPosition(_fileNamePtr);
                                var _fetchName = Encoding.UTF8.GetString(_fetchStream.ReadBytes(0x30));

                                _fetchName = _fetchName.Substring(0x00, _fetchName.IndexOf('\x00'));

                                if (_fetchName.Length < 2 || !char.IsLetterOrDigit(_fetchName[0]) || !char.IsLetterOrDigit(_fetchName[1]))
                                    continue;

                                _fetchStream.SetPosition(_destinationPtr);
                                var _writeFile = WriteFile(_fetchStream, _fetchName);

                                _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);
                                _fetchStream.Write(_writeFile);

                            }
                            break;

                            case 0x02:
                            {
                                _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);

                                var _fileNamePtr = _fetchStream.ReadInt32();

                                _fetchStream.SetPosition(_fileNamePtr);
                                var _fetchName = Encoding.UTF8.GetString(_fetchStream.ReadBytes(0x30));

                                _fetchName = _fetchName.Substring(0x00, _fetchName.IndexOf('\x00'));

                                if (_fetchName.Length < 2 || !char.IsLetterOrDigit(_fetchName[0]) || !char.IsLetterOrDigit(_fetchName[1]))
                                    continue;

                                var _fetchFileSize = ResolveSize(_fetchName);

                                _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);
                                _fetchStream.Write(_fetchFileSize);
                            }
                            break;

                            case 0x00:
                                Thread.Sleep(1);
                                continue;
                        }

                        _fetchStream.SetPosition((0x0F << 0x10) - 0x04).Write(0x00);
                    }
                }, _cancelToken);
            }
        }
    }
}
