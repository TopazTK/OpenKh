using OpenKh.Common;
using OpenKh.Engine.Input;
using OpenKh.Tools.Common;
using OpenKh.Tools.ModManager.Classes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Services
{
    public class InjectorService
    {
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

        CancellationTokenSource _cancelSource;
        CancellationToken _cancelToken;

        long _memoryOffset = 0x00;

        uint _loadFileFunc = 0x1682b8;
        uint _getFileSizeFunc = 0x1AE1B0;
        uint _subFileSizeFunc = 0x1AE460;

        uint _bufferPtr = 0x350E88;
        uint _regionPtr = 0x33CAF0;
        uint _languagePtr = 0x33CAF4;

        uint _hookPtrLoad = 0xFFF00;
        uint _hookPtrSize = 0xFFF00;

        bool _functionsWritten = false;

        public Config TargetConfig { get; set; }

        byte ResolvePath(string input, out string? finalPath)
        {
            var _fetchBuildPath = PathService.ResolveBuild(TargetConfig);
            var _fetchDataPath = PathService.ResolveData(TargetConfig);

            var _fetchRegionList = Kh2.Constants.Regions;
            var _fetchFileRegion = _fetchRegionList.FirstOrDefault(x => input.Contains($"/{x}/") || input.EndsWith($".a.{x}"));

            var _fetchRegionInput = input.Replace($"/{_fetchFileRegion}/", $"/fm/")
                                         .Replace($".a.{_fetchFileRegion}", $".a.fm")
                                         .Replace($".apdx", $".a.fm");


            if (DENY_FILES.Contains(input))
            {
                finalPath = null;
                return 0x00;
            }

            var _fetchBuildFile = Path.Combine(_fetchBuildPath, input);
            var _fetchBuildFileRegion = Path.Combine(_fetchBuildPath, _fetchRegionInput);

            if (File.Exists(_fetchBuildFile))
            {
                finalPath = _fetchBuildFile;
                return 0x01;
            }

            else if (File.Exists(_fetchBuildFileRegion))
            {
                finalPath = _fetchBuildFileRegion;
                return 0x01;
            }

            finalPath = null;
            return 0x03;
        }

        public InjectorService(Config _targetConfig)
        {
            TargetConfig = _targetConfig;

            _cancelSource = new CancellationTokenSource();
            _cancelToken = _cancelSource.Token;

            Stream? _fetchStream = null;
            var _fetchPathEmu = PathService.ResolveEmulator(TargetConfig);

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
                if (OperatingSystem.IsLinux())
                {
                    var _fetchProcessID = _targetProcess.Id;

                    Thread.Sleep(500);

                    _fetchStream = new PINEStream();
                }

                else
                {
                    try
                    {
                        var _fetchProcessID = _targetProcess.Id;
                        var _fetchMemoryName = $"pcsx2_{_fetchProcessID}";

                        Thread.Sleep(1000);

                        var _fetchMemoryMap = MemoryMappedFile.OpenExisting(_fetchMemoryName);

                        if (_fetchMemoryMap == null)
                        {
                            _targetProcess.Kill();
                            return;
                        }

                        _fetchStream = _fetchMemoryMap.CreateViewStream();
                    }

                    catch (FileNotFoundException) { _fetchStream = new ProcessStream(_targetProcess, 0x20000000, 0x20000000); }
                }

                if (_fetchStream == null)
                {
                    _targetProcess.Kill();
                    return;
                }

                Task.Run(() =>
                {
                LOOP_START:

                    if (_cancelToken.IsCancellationRequested || _targetProcess.HasExited)
                        return;

                    _fetchStream.SetPosition(_hookPtrLoad);

                    uint[] _fetchHookLoad =
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
                        MIPS.NOP()
                    ];

                    uint[] _fetchHookSize =
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
                        MIPS.BEQ(MIPS.V0, MIPS.Zero, 0x03),
                        MIPS.NOP(),
                        MIPS.JR(MIPS.T4),
                        MIPS.NOP(),
                        MIPS.ADDIU(MIPS.SP, MIPS.SP, -0x10),
                        MIPS.SD(MIPS.T4, MIPS.SP, 0x08),
                        MIPS.SD(MIPS.S0, MIPS.SP, 0x00),
                        MIPS.JR(MIPS.RA),
                        MIPS.NOP()
                    ];

                    if (_fetchStream.ReadUInt32() == 0x00)
                    {
                        byte[] _fetchHookArray = new byte[_fetchHookLoad.Length * sizeof(uint) + _fetchHookSize.Length * sizeof(uint)];

                        Buffer.BlockCopy(_fetchHookLoad, 0, _fetchHookArray, 0, _fetchHookLoad.Length * sizeof(uint));
                        Buffer.BlockCopy(_fetchHookSize, 0, _fetchHookArray, _fetchHookLoad.Length * sizeof(uint), _fetchHookSize.Length * sizeof(uint));

                        _fetchStream.SetPosition(_hookPtrLoad);
                        _fetchStream.Write(_fetchHookArray);

                        _hookPtrSize = _hookPtrLoad + (uint)_fetchHookLoad.Length * sizeof(uint);
                    }

                    if (_loadFileFunc > 0)
                    {
                        uint[] _fetchFunction =
                        [
                            MIPS.ADDIU(MIPS.T4, MIPS.RA, 0),
                            MIPS.JAL(_hookPtrLoad),
                            MIPS.ADDIU(MIPS.T5, MIPS.Zero, 0x01)
                        ];

                        byte[] _fetchFunctionArray = new byte[_fetchFunction.Length * sizeof(uint)];
                        Buffer.BlockCopy(_fetchFunction, 0, _fetchFunctionArray, 0, _fetchFunctionArray.Length);

                        _fetchStream.SetPosition(_loadFileFunc);
                        _fetchStream.Write(_fetchFunctionArray);
                    }

                    if (_getFileSizeFunc > 0)
                    {
                        uint[] _fetchFunction =
                        [
                            MIPS.ADDIU(MIPS.T4, MIPS.RA, 0),
                            MIPS.JAL(_hookPtrSize),
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

                        byte[] _fetchFunctionArray = new byte[_fetchFunction.Length * sizeof(uint)];
                        Buffer.BlockCopy(_fetchFunction, 0, _fetchFunctionArray, 0, _fetchFunctionArray.Length);

                        _fetchStream.SetPosition(_getFileSizeFunc);
                        _fetchStream.Write(_fetchFunctionArray);
                    }

                    _fetchStream.Flush();

                    /////////////////////////////////////////////////////////////////////

                    var _fetchOpcodeAddr = (0x0F << 0x10) - 0x04;

                    _fetchStream.SetPosition(_fetchOpcodeAddr);
                    var _fetchOpcode = _fetchStream.ReadInt32();

                    if (_fetchStream.Position == _fetchOpcode || _targetProcess.HasExited)
                        return;

                    switch (_fetchOpcode)
                    {
                        case 0x01:
                        {
                            _fetchStream.SetPosition(_fetchOpcodeAddr - 0x08);

                            var _destinationPtr = _fetchStream.ReadInt32();
                            var _fileNamePtr = _fetchStream.ReadInt32();

                            _fetchStream.SetPosition(_fileNamePtr);

                            var _fetchSize = 0x00;
                            var _fetchName = _fetchStream.ReadString(0x30, Encoding.ASCII);

                            if (String.IsNullOrEmpty(_fetchName))
                                goto LOOP_START;

                            Debug.WriteLine(_fetchName);

                            var _couldResolve = ResolvePath(_fetchName, out var _filePath);

                            if (_couldResolve == 0x01)
                            {
                                var _fetchData = File.ReadAllBytes(_filePath);
                                _fetchSize = _fetchData.Length;

                                _fetchStream.SetPosition(_destinationPtr);
                                _fetchStream.Write(_fetchData);
                            }

                            _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);
                            _fetchStream.Write(_fetchSize);

                            _fetchStream.Flush();
                        }
                        break;

                        case 0x02:
                        {
                            _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);

                            var _fileNamePtr = _fetchStream.ReadInt32();

                            _fetchStream.SetPosition(_fileNamePtr);
                            var _fetchName = _fetchStream.ReadString(0x30, Encoding.ASCII);

                            if (String.IsNullOrEmpty(_fetchName))
                                goto LOOP_START;

                            var _fetchSize = 0x00;
                            var _couldResolve = ResolvePath(_fetchName, out var _filePath);

                            if (_couldResolve == 0x01)
                            {
                                var _fetchInfo = new FileInfo(_filePath);
                                _fetchSize = (int)_fetchInfo.Length;
                            }

                            _fetchStream.SetPosition(_fetchOpcodeAddr - 0x04);
                            _fetchStream.Write(_fetchSize);

                            _fetchStream.Flush();
                        }
                        break;

                        case 0x00:
                            Thread.Sleep(5);
                            goto LOOP_START;
                    }

                    _fetchStream.SetPosition(_fetchOpcodeAddr);
                    _fetchStream.Write(0x00);

                    Thread.Sleep(5);

                    goto LOOP_START;
                }, _cancelToken);
            }
        }
    }
}
