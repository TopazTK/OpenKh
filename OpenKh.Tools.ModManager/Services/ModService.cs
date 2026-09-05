using Avalonia.Controls;
using Avalonia.Threading;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Bbs;
using OpenKh.Common;
using OpenKh.Common.Archives;
using OpenKh.Egs;
using OpenKh.Kh2;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Services
{
    public static class ModService
    {
        // We need a cancellation token to interrupt what we are doing should the user not want to do that anymore.
        public static CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        public static CancellationToken CancelToken = CancelTokenSource.Token;

        public static string ResolveMD5(ModModel currentMod, Config currentConfig)
        {
            var _fetchRelativePath = Path.GetRelativePath(PathService.ResolveMod(currentConfig), currentMod.ModPath);
            var _fetchBytes = Encoding.ASCII.GetBytes(_fetchRelativePath);
            var _fetchHash = MD5.HashData(_fetchBytes);

            return Convert.ToHexString(_fetchHash);
        }

        /// <summary>
        /// Installs a mod from any Git repository.
        /// </summary>
        /// <param name="modPath">The path for the mod folder.</param>
        /// <param name="repoName">The input string of the repository. The format is [AUTHOR]/[NAME]:BRANCH@HOST</param>
        /// <param name="reportProgress">Optional, the progress handler for Git requests.</param>
        /// <returns>Error code. 0x00 is success, 0x01 is invlaid mod, 0x03 is cancellation.</returns>
        public static async Task<byte> InstallGit(string modPath, string repoName, TransferProgressHandler? reportProgress = null)
        {
            // Reset the cancel token if it was called prior.

            if (CancelToken.IsCancellationRequested)
            {
                CancelTokenSource.Dispose();

                CancelTokenSource = new CancellationTokenSource();
                CancelToken = CancelTokenSource.Token;
            }

            // Fetch the platfrom from the given string.
            var _fetchPlatform = repoName.Contains('@') ? repoName.Split('@').Last() : null;
            repoName = _fetchPlatform != null ? repoName.Replace("@" + _fetchPlatform, "") : repoName;

            // Fetch the branch from the given string.
            var _fetchBranch = repoName.Contains(':') ? repoName.Split(':').Last() : null;
            repoName = _fetchBranch != null ? repoName.Replace(":" + _fetchBranch, "") : repoName;

            // Fetch the author and the name from the given string.
            var _fetchAuthor = repoName.Split('/').First();
            var _fetchName = repoName.Split('/').Last();

            // P.S. - I know my string handling is SHIT and can break. If anyone knows how to do this in C# with regex I highly advise you to fix this.

            // Construct the mod and git directories.
            var _fetchCurrentModDir = Path.Combine(modPath, _fetchName);
            var _fetchCurrentGitPath = Path.Combine(_fetchCurrentModDir, ".git");

            // Create the Uri to be used with Git.
            var _fetchBaseUri = new Uri("https://" + (_fetchPlatform != null ? _fetchPlatform : "github.com"));
            var _fetchRelativeUri = new Uri(_fetchBaseUri, $"{repoName}");

            // Construct the clone options.

            var _cloneOptions = new CloneOptions
            {
                Checkout = true,
                BranchName = _fetchBranch,
            };

            // Do the least amount of fetching humanly possible.

            _cloneOptions.FetchOptions.Depth = 1;
            _cloneOptions.FetchOptions.Prune = true;

            // If we have a progress handler, pass it on in fetch options.

            if (reportProgress != null)
                _cloneOptions.FetchOptions.OnTransferProgress = reportProgress;

            // Otherwise, create one of our own. This is needed to be able to cancel Git transactions at will.

            else
            {
                _cloneOptions.FetchOptions.OnTransferProgress = new TransferProgressHandler((progress) =>
                {
                    if (CancelToken.IsCancellationRequested)
                        return false;

                    return true;
                });
            }

            // If the directory does not exist, create it. 
            // TODO: If it does, ask for an overwrite.
            if (!Directory.Exists(_fetchCurrentModDir))
                Directory.CreateDirectory(_fetchCurrentModDir);

            // This is being done with a try-catch because if the git doesn't exist this throws an exception.
            // If it does, consider this is not a valid mod and abort.

            IEnumerable<Reference>? _fetchRemotes = null;

            try { _fetchRemotes = Repository.ListRemoteReferences(_fetchRelativeUri.ToString()); }
            catch (LibGit2SharpException)
            {
                Directory.Delete(_fetchCurrentModDir, true);
                return 0x01;
            }

            // If there is not a platform given, meaning it is a GitHub mod. Or if it is LITERALLY GitHub:
            if (_fetchPlatform == null || _fetchPlatform == "github.com")
            {
                // Fetch the branch string. If branch is not given, default to whatever the HEAD branch is.
                // This took far too long for me to admit.
                var _branchString = _fetchBranch != null ? _fetchBranch : _fetchRemotes.First().TargetIdentifier;

                // Make an HTTP client and use GitHub's RAW API to see if the mod.yml exists.
                // This is the fastest way to handle this, otherwise I sadly have to fetch the mod FIRST and then check it.
                using var _makeClient = new HttpClient();
                using var _fetchResponse = await _makeClient.GetAsync($"https://raw.githubusercontent.com/{repoName}/{_branchString}/mod.yml", HttpCompletionOption.ResponseHeadersRead);

                // If the file doesn't exist, abort.
                if (_fetchResponse.StatusCode != HttpStatusCode.OK)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x01;
                }

                // Otherwise, clone the mod.
                // This is being done on an awaited task because otherwise even though THIS is a task it will still block UI execution.
                // Also there is a try-catch here, LibGit2Sharp will throw an exception if the user cancels an operation.
                await Task.Run(() =>
                {
                    try { Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions); }
                    catch (LibGit2SharpException) { }
                });

                // After the task finishes/aborts, if we requested cancellation, abort.
                if (CancelToken.IsCancellationRequested)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x03;
                }

                // Fetch the Git directory and fix all permissions before moving on.
                // We have to do this on each case because the second case requires deletion if the YAML isn't found AFTER it initializes a Git.
                // While it may TECHNICALLY may not be required HERE, I ain't risking it.

                var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                    _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
            }

            // If a platform was specified:
            else
            {
                // First, clone the mod.
                // We gotta do this because every Git platform has a different REST API and I want to support ALL of them.
                await Task.Run(() =>
                {
                    try
                    { Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions); }
                    catch (LibGit2SharpException) { }
                });

                // Fetch the Git directory and fix all permissions before moving on.
                // Again, may not be necessary here. Again, not risking it.

                var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                    _fetchFile.Attributes &= ~FileAttributes.ReadOnly;

                // After the task finishes/aborts, if we requested cancellation, abort.
                if (CancelToken.IsCancellationRequested)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x03;
                }

                // Init the repository we just cloned.
                var _fetchGit = new Repository(_fetchCurrentModDir);

                // Fetch the absolute latest commit and see if it has the YAML in it.
                var _fetchCommit = _fetchGit.Head.Tip;
                var _doesModFileExist = _fetchCommit["mod.yml"] != null;

                // Dispose the repository.
                _fetchGit.Dispose();

                // Fix permissions before moving on.
                // It IS necessary here.

                foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                    _fetchFile.Attributes &= ~FileAttributes.ReadOnly;

                // If the YAML does no exist, delete the "mod" and abort.
                if (!_doesModFileExist)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x01;
                }
            }

            // All is well, return success.
            return 0x00;
        }

        /// <summary>
        /// Installs a mod from a local file.
        /// </summary>
        /// <param name="modPath">The path for the mod folder.</param>
        /// <param name="fileName">The targeted file.</param>
        /// <param name="reportProgress">Optional, the progress handler for file progress.</param>
        /// <returns>Error code. 0x00 is success, 0x01 is invlaid mod, 0x03 is cancellation.</returns>
        public static async Task<byte> InstallLocal(string modPath, string fileName, Func<int, int, bool>? reportProgress = null)
        {
            // Reset the cancel token if it was called prior.

            if (CancelToken.IsCancellationRequested)
            {
                CancelTokenSource.Dispose();

                CancelTokenSource = new CancellationTokenSource();
                CancelToken = CancelTokenSource.Token;
            }

            // Get the extension to check and the mod path to process.
            var _fetchExtension = Path.GetExtension(fileName).ToLower();
            var _fetchCurrentModDir = Path.Combine(modPath, Path.GetFileNameWithoutExtension(fileName));

            // If the file is a ZIP Archive:
            if (_fetchExtension == ".zip")
            {
                // I hate that I have to use streams to use a file class.
                // Just have the stream internally, kappa.
                using (var _fileStream = new FileStream(fileName, FileMode.Open))
                {
                    // Fetch the archive on read-only to prevent... issues.
                    var _fetchArchive = new ZipArchive(_fileStream, ZipArchiveMode.Read);

                    // If the archive does not contain a YAML, abort.
                    if (_fetchArchive.Entries.FirstOrDefault(x => x.Name == "mod.yml") == null)
                        return 0x01;

                    // If the directory don't exist, create it.
                    // TODO: If it exists, ask for an overwrite.
                    if (!Directory.Exists(_fetchCurrentModDir))
                        Directory.CreateDirectory(_fetchCurrentModDir);

                    // Now we gettin' to the nitty gritty.
                    await Task.Run(() =>
                    {
                        // Why am I iterating every file and handling it separately instead of using ExtractAll?
                        // Because Microsoft is a piece of shit compant that did not implement progress feedback to that function :^)
                        for (int i = 0; i < _fetchArchive.Entries.Count; i++)
                        {
                            // Fetch the current entry.
                            var _fetchEntry = _fetchArchive.Entries[i];

                            // If the entry is a directory (yes, really): Move on to the next one.
                            if (_fetchEntry.FullName.EndsWith('/'))
                                continue;

                            // Construct the target paths for the file and the directory.
                            var _fetchFileTarget = Path.Combine(_fetchCurrentModDir, _fetchEntry.FullName);
                            var _fetchDirectory = Path.Combine(_fetchCurrentModDir, Path.GetDirectoryName(_fetchEntry.FullName));

                            // Should the target directory not exist, we make it exist.
                            if (!Directory.Exists(_fetchDirectory))
                                Directory.CreateDirectory(_fetchDirectory);

                            // Extract the file.
                            _fetchEntry.ExtractToFile(_fetchFileTarget, true);

                            // If the progress feedback exists:
                            if (reportProgress != null)
                            {
                                // Feedback to the progress and see the result.
                                var _fetchProgress = reportProgress(i, _fetchArchive.Entries.Count);

                                // If the result is false, meaning cancellation requested, break out immediately.
                                if (!_fetchProgress)
                                    break;
                            }

                            // Otherwise manually check for cancellation and break out if it's requested.
                            else if (CancelToken.IsCancellationRequested)
                                break;
                        }
                    });

                    // If the task ended/aborted and cancellation was requested, abort.
                    if (CancelToken.IsCancellationRequested)
                    {
                        Directory.Delete(_fetchCurrentModDir, true);
                        return 0x03;
                    }
                }
            }

            // If the file is a LUA Script, handle it.
            // This is code that was YANKED from the old mod manager, cleaned up and brought up to standard.
            // I am not commenting this yet.
            else if (_fetchExtension == ".lua")
            {
                var _fetchCurrentLuaName = Path.Combine(_fetchCurrentModDir, Path.GetFileName(fileName));

                if (!Directory.Exists(_fetchCurrentModDir))
                    Directory.CreateDirectory(_fetchCurrentModDir);

                File.Copy(fileName, _fetchCurrentLuaName);

                var _createMetadata = new Metadata
                {
                    Title = Path.GetFileNameWithoutExtension(fileName) + " (Lua)",
                    Description = "This Metadata has been automatically generated for this Lua Modification.",
                    Assets = new List<AssetFile>()
                };

                var _createSource = new AssetFile() { Name = Path.GetFileName(fileName) };
                var _createFile = new AssetFile()
                {
                    Name = "scripts/" + Path.GetFileName(fileName),
                    Method = "copy",
                    Source = new List<AssetFile>() { _createSource },
                };

                _createMetadata.Assets.Add(_createFile);

                using (var _strReader = new StreamReader(_fetchCurrentLuaName))
                {
                    while (!_strReader.EndOfStream)
                    {
                        string _fetchLine = _strReader.ReadLine();

                        if (_fetchLine.Contains("LUAGUI"))
                        {
                            string _lineGib = "";
                            string _lineLead = "";

                            _lineGib = _fetchLine.Substring(_fetchLine.IndexOf("=") + 1).Replace("\"", "").Replace("\'", "").Trim();
                            _lineLead = string.Concat(_fetchLine.Take(11));

                            switch (_lineLead)
                            {
                                case "LUAGUI_NAME":
                                    _createMetadata.Title = "\"" + _lineGib + "\"";
                                    break;
                                case "LUAGUI_AUTH":
                                    _createMetadata.OriginalAuthor = "\"" + _lineGib + "\"";
                                    break;
                                case "LUAGUI_DESC":
                                    _createMetadata.Description = "\"" + _lineGib + "\"";
                                    break;
                            }
                        }
                    }
                }

                var _yamlPath = Path.Combine(_fetchCurrentModDir, "mod.yml");
                File.WriteAllText(_yamlPath, _createMetadata.ToString());
            }

            // If the file is ANY PCPatch Package, handle it.
            // This is code that was YANKED from the old mod manager, cleaned up and brought up to standard.
            // I am not commenting this yet.
            else if (_fetchExtension.Contains("pcpatch"))
            {
                using (var _fileStream = new FileStream(fileName, FileMode.Open))
                {
                    var _fetchArchive = new ZipArchive(_fileStream, ZipArchiveMode.Read);

                    if (!Directory.Exists(_fetchCurrentModDir))
                        Directory.CreateDirectory(_fetchCurrentModDir);

                    await Task.Run(() =>
                    {
                        for (int i = 0; i < _fetchArchive.Entries.Count; i++)
                        {
                            var _fetchEntry = _fetchArchive.Entries[i];

                            if (_fetchEntry.FullName.EndsWith('/'))
                                continue;

                            var _pathSplit = _fetchEntry.FullName.Split(_fetchEntry.FullName.IndexOf('/') > -1 ? "/" : "\\");
                            var _pathPackage = _pathSplit[0];

                            var _accommodatePath = _fetchEntry.FullName.Replace("original/", "").Replace(_pathPackage + "/", "");

                            var _fetchFileTarget = Path.Combine(_fetchCurrentModDir, _accommodatePath);
                            var _fetchDirectory = Path.Combine(_fetchCurrentModDir, Path.GetDirectoryName(_accommodatePath));

                            if (!Directory.Exists(_fetchDirectory))
                                Directory.CreateDirectory(_fetchDirectory);

                            _fetchEntry.ExtractToFile(_fetchFileTarget, true);

                            var _fetchProgress = reportProgress(i, _fetchArchive.Entries.Count);

                            if (!_fetchProgress)
                                break;
                        }
                    });

                    if (CancelToken.IsCancellationRequested)
                    {
                        Directory.Delete(_fetchCurrentModDir, true);
                        return 0x03;
                    }

                    var _fetchMetadata = new Metadata();

                    _fetchMetadata.Title = Path.GetFileNameWithoutExtension(fileName) + $" ({_fetchExtension.ToUpper()})";
                    _fetchMetadata.Description = $"This Metadata has been automatically generated for this {_fetchExtension.ToUpper()} Modification.";
                    _fetchMetadata.Assets = new List<AssetFile>();

                    await Task.Run(() =>
                    {
                        foreach (var _fetchEntry in _fetchArchive.Entries.Where(x => !string.IsNullOrEmpty(x.Name)))
                        {
                            var _pathSplit = _fetchEntry.FullName.Split(_fetchEntry.FullName.IndexOf('/') > -1 ? "/" : "\\");
                            var _pathPackage = _pathSplit[0];

                            var _accommodatePath = _fetchEntry.FullName.Replace("original/", "").Replace(_pathPackage + "/", "");

                            var _createSource = new AssetFile() { Name = _accommodatePath };

                            var _createFile = new AssetFile()
                            {
                                Name = _accommodatePath,
                                Method = "copy",
                                Source = new List<AssetFile>() { _createSource },
                                Platform = "pc",
                                Package = _pathPackage
                            };

                            _fetchMetadata.Assets.Add(_createFile);

                            if (CancelToken.IsCancellationRequested)
                                break;
                        }
                    });

                    if (CancelToken.IsCancellationRequested)
                    {
                        Directory.Delete(_fetchCurrentModDir, true);
                        return 0x03;
                    }

                    var _yamlPath = Path.Combine(_fetchCurrentModDir, "mod.yml");
                    File.WriteAllText(_yamlPath, _fetchMetadata.ToString());
                }
            }

            // All is good, return success.
            return 0x00;
        }

        /// <summary>
        /// Builds the mods given according to the config given and reports back progress.
        /// </summary>
        /// <param name="modsList">The mods to build.</param>
        /// <param name="currentConfig">The config to base the build on.</param>
        /// <param name="reportModProgress">[Optional] Callback to the mod build progress.</param>
        /// <param name="reportAssetProgress">[Optional] Callback to the asset build progress per mod.</param>
        /// <returns>Error code. 0x00 is success, 0x01 is failure, 0x03 is cancellation.</returns>
        public static async Task<byte> Build(IEnumerable<ModModel> modsList, Config currentConfig, Func<string, int, int, bool>? reportModProgress = null, Func<int, int, bool>? reportAssetProgress = null)
        {
            // Reset the cancel token if it was called prior.

            if (CancelToken.IsCancellationRequested)
            {
                CancelTokenSource.Dispose();

                CancelTokenSource = new CancellationTokenSource();
                CancelToken = CancelTokenSource.Token;
            }

            var _fetchPatcher = new PatcherProcessor();
            var _fetchPackageMap = new ConcurrentDictionary<string, string>();

            var _fetchGameName = currentConfig.Frontend.TargetGame.ToString().ToLower();
            var _fetchGameId = PatcherProcessor.GameShorthand[(int)currentConfig.Frontend.TargetGame];

            var _fetchDataPath = PathService.ResolveData(currentConfig);
            var _fetchGamePath = PathService.ResolveGame(currentConfig);
            var _fetchBuildPath = PathService.ResolveBuild(currentConfig);

            var _fetchPKGMapPath = Path.Combine(_fetchBuildPath, "patch-package-map.txt");

            if (!Directory.Exists(_fetchBuildPath))
                Directory.CreateDirectory(_fetchBuildPath);

            else
            {
                Directory.Delete(_fetchBuildPath, true);
                Directory.CreateDirectory(_fetchBuildPath);
            }

            var _currentModName = "";
            var _currentModIndex = 0;
            var _currentModCount = modsList.Where(x => x.ModValid && x.ModActive).Count();

            if (reportModProgress != null)
            {
                Task.Run(async () =>
                {
                    while (!CancelToken.IsCancellationRequested)
                    {
                        var _fetchProgress = reportModProgress(_currentModName, _currentModIndex, _currentModCount);

                        if (!_fetchProgress)
                            break;

                        await Task.Delay(TimeSpan.FromMilliseconds(10), CancelToken);
                    }
                });
            }

            await Task.Run(async () =>
            {
                for (int _modIdx = modsList.Count() - 1; _modIdx >= 0; _modIdx--)
                {
                    var _fetchMod = modsList.ElementAt(_modIdx);

                    _currentModIndex++;

                    if (!_fetchMod.ModValid || !_fetchMod.ModActive)
                        continue;

                    _currentModName = _fetchMod.ModTitle;

                    var _fetchYamlPath = Path.Combine(_fetchMod.ModPath, "mod.yml");
                    var _fetchMetadata = Metadata.Read(_fetchYamlPath);

                    await Task.Run(() =>
                    {
                        _fetchPatcher.Patch
                        (
                            _fetchDataPath,
                            _fetchBuildPath,
                            _fetchMetadata,
                            _fetchMod.ModPath,
                            _fetchGamePath,
                            (int)currentConfig.Frontend.TargetPlatform,
                            (int)currentConfig.Frontend.TargetGame,
                            _fetchPackageMap,
                            reportProgress: reportAssetProgress
                        );
                    });

                    if (CancelToken.IsCancellationRequested)
                        break;
                }
            });

            if (CancelToken.IsCancellationRequested)
            {
                Directory.Delete(_fetchBuildPath, true);
                return 0x03;
            }

            using (var _writePackageMap = new StreamWriter(_fetchPKGMapPath))
                foreach (var _mapEntry in _fetchPackageMap)
                    _writePackageMap.WriteLine(_mapEntry.Key + " $$$$ " + _mapEntry.Value);

            return 0x00;
        }
    
        /// <summary>
        /// Runs the game. Requires to be called from a View because of Avalonia's cross-platform launch service.
        /// </summary>
        /// <param name="currentConfig">The config to base the launch on.</param>
        /// <param name="topLevelObject">The top-level fetched from the calling View.</param>
        /// <returns>Only true for now.</returns>
        public static async Task<bool> Run(Config currentConfig, TopLevel topLevelObject)
        {
            var _fetchTargetGame = currentConfig.Frontend.TargetGame;
            var _fetchTargetPlatform = currentConfig.Frontend.TargetPlatform;

            var _fetchGamePath = PathService.ResolveGame(currentConfig);

            var _fetchAPIFilePath = Path.Combine(_fetchGamePath, "steam_appid.txt");

            var _fetchLauncherPath = Path.Combine(AppContext.BaseDirectory, "resources/OpenKh.Command.Launcher.exe");
            var _fetchTargetGamePath = Path.Combine(_fetchGamePath, Config.GameExecutable[_fetchTargetGame]);

            var _fetchAPIExists = _fetchTargetPlatform == Platform.STEAM ? File.Exists(_fetchAPIFilePath) : false;

            var _fetchSteamId = currentConfig.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? 2552440 : 2552430;
            var _fetchReMIXFilePath = currentConfig.Frontend.TargetGame == Game.DREAM_DROP_DISTANCE ? Path.Combine(_fetchGamePath, "KINGDOM HEARTS HD 2.8 Final Chapter Prologue.exe") : Path.Combine(_fetchGamePath, "KINGDOM HEARTS HD 1.5+2.5 ReMIX.exe");

            var _fetchArguments = currentConfig.Frontend.LaunchArguments;

            if (_fetchAPIExists)
                _fetchArguments = $"{Config.GameShorthand[_fetchTargetGame]} {_fetchArguments}";

            Uri _fetchTargetUri = null;
            FileInfo _fetchTargetFile = null;

            if (!OperatingSystem.IsWindows() || !_fetchAPIExists)
            {
                switch (_fetchTargetPlatform)
                {
                    case Platform.STEAM:
                        _fetchTargetUri = new Uri($"steam://rungameid/{_fetchSteamId}" + (!String.IsNullOrEmpty(_fetchArguments) ? $"//{ Uri.EscapeDataString(_fetchArguments) }" : ""));
                        break;
                    case Platform.EPIC_GAMES_STORE:
                        _fetchTargetUri = new Uri("com.epicgames.launcher://apps/4158b699dd70447a981fee752d970a3e%3A5aac304f0e8948268ddfd404334dbdc7%3A68c214c58f694ae88c2dab6f209b43e4?action=launch");
                        break;
                }
            }

            if (topLevelObject.Launcher != null && _fetchTargetUri != null)
            {
                if (_fetchAPIExists)
                {
                    var _fetchReMIXBackup = Path.ChangeExtension(_fetchReMIXFilePath, ".bak");

                    if (!File.Exists(_fetchReMIXBackup))
                    {
                        File.Move(_fetchReMIXFilePath, _fetchReMIXBackup);
                        File.Copy(_fetchLauncherPath, _fetchReMIXFilePath, true);
                    }
                }

                await topLevelObject.Launcher.LaunchUriAsync(_fetchTargetUri);
            }

            else
            {
                var _fetchProcessInfo = new ProcessStartInfo
                {
                    FileName = _fetchTargetGamePath,
                    WorkingDirectory = _fetchGamePath,
                    UseShellExecute = true
                };

                Process.Start(_fetchProcessInfo);
            }

            return true;
        }
    
        public static async Task<byte> Extract(List<bool> extractGames, Config currentConfig, bool isPlatformPC, Func<int, int, bool>? reportProgress = null)
        {
            if (isPlatformPC)
            {
                var _fetchExtractList = new List<string>()
                {
                    extractGames[0] ? "kh1" : "",
                    extractGames[1] ? "kh2" : "",
                    extractGames[2] ? "Recom" : "",
                    extractGames[3] ? "bbs" : "",
                    extractGames[4] ? "kh3d" : ""
                }.Where(x => !String.IsNullOrEmpty(x));

                var _fetchShortDictionary = new Dictionary<string, string>
                {
                    { "kh1", "kh1" },
                    { "kh2", "kh2" },
                    { "Recom", "com" },
                    { "bbs", "bbs" },
                    { "kh3d", "ddd" },
                };

                var _fetchFilesCurrent = 0;
                var _fetchFilesTotal = 0;

                await Task.Run(async () =>
                {
                    var _fetchDataPath = PathService.ResolveData(currentConfig, true);

                    if (String.IsNullOrEmpty(_fetchDataPath))
                    {
                        currentConfig.Frontend.DataPath = Path.Combine(AppContext.BaseDirectory, "extract");
                        _fetchDataPath = PathService.ResolveData(currentConfig, true);
                    }

                    foreach (var _fetchExtractGame in _fetchExtractList)
                    {
                        var _fetchGamePath = _fetchExtractGame == "kh3d" ? PathService.ResolvePath28(currentConfig) : PathService.ResolvePath1525(currentConfig);

                        var _fetchPackagePath = Path.Combine(_fetchGamePath, "Image", currentConfig.Frontend.TargetPlatform == Platform.STEAM ? "dt" : "en");
                        var _fetchHeaderFiles = Directory.GetFiles(_fetchPackagePath).Where(x => x.Contains(_fetchExtractGame) && x.EndsWith(".hed"));

                        foreach (var _fetchHeader in _fetchHeaderFiles)
                        {
                            using (var _fetchHedStream = new FileStream(_fetchHeader, FileMode.Open))
                            {
                                var _fetchFiles = Hed.Read(_fetchHedStream);
                                _fetchFilesTotal += _fetchFiles.Count();
                            }
                        }
                    }

                    Parallel.ForEach(_fetchExtractList.AsParallel(), (_fetchExtractGame, _fetchStateGame) =>
                    {
                        var _fetchDataPath = PathService.ResolveData(currentConfig, true);

                        if (String.IsNullOrEmpty(_fetchDataPath))
                        {
                            currentConfig.Frontend.DataPath = Path.Combine(AppContext.BaseDirectory, "extract");
                            _fetchDataPath = PathService.ResolveData(currentConfig, true);
                        }

                        var _fetchGamePath = _fetchExtractGame == "kh3d" ? PathService.ResolvePath28(currentConfig) : PathService.ResolvePath1525(currentConfig);

                        var _fetchPackagePath = Path.Combine(_fetchGamePath, "Image", currentConfig.Frontend.TargetPlatform == Platform.STEAM ? "dt" : "en");
                        var _fetchHeaderFiles = Directory.GetFiles(_fetchPackagePath).Where(x => x.Contains(_fetchExtractGame) && x.EndsWith(".hed"));

                        Parallel.ForEach(_fetchHeaderFiles.AsParallel(), (_fetchHeader, _fetchStateHeader) =>
                        {
                            var _fetchPackage = Path.ChangeExtension(_fetchHeader, ".pkg");

                            using (var _fetchHedStream = new FileStream(_fetchHeader, FileMode.Open))
                            {
                                var _fetchFiles = Hed.Read(_fetchHedStream);

                                Parallel.ForEach(_fetchFiles.AsParallel(), (_fetchFile, _fetchStateFile) =>
                                {
                                    using (var _fetchPkgStream = new FileStream(_fetchPackage, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        _fetchFilesCurrent++;

                                        var _fetchHashText = Convert.ToHexString(_fetchFile.MD5);
                                        var _fetchNameValue = EgsTools.Names.FirstOrDefault(x => x.Key == _fetchHashText).Value;

                                        var _fetchFileName = String.IsNullOrEmpty(_fetchNameValue) ? $"{_fetchHashText}.dat" : _fetchNameValue;

                                        var _fetchFilePath = Path.Combine(_fetchDataPath, _fetchShortDictionary[_fetchExtractGame], _fetchFileName);
                                        var _fetchFileDir = Path.GetDirectoryName(_fetchFilePath);

                                        _fetchPkgStream.SetPosition(_fetchFile.Offset);
                                        var _fetchData = new EgsHdAsset(_fetchPkgStream);

                                        if (!Directory.Exists(_fetchFileDir))
                                            Directory.CreateDirectory(_fetchFileDir);

                                        File.Create(_fetchFilePath).Using(_fetchStr => _fetchStr.Write(_fetchData.OriginalData));

                                        if (_fetchData.Assets.Count() != 0x00)
                                        {
                                            var _fetchRemasterPath = Path.Combine(_fetchDataPath, _fetchShortDictionary[_fetchExtractGame], "remastered", _fetchFileName);

                                            Parallel.ForEach(_fetchData.Assets.AsParallel(), (_fetchAsset, _fetchStateAsset) =>
                                            {
                                                var _fetchAssetPath = Path.Combine(_fetchRemasterPath, _fetchAsset);
                                                var _fetchAssetDir = Path.GetDirectoryName(_fetchAssetPath);

                                                if (!Directory.Exists(_fetchAssetDir))
                                                    Directory.CreateDirectory(_fetchAssetDir);

                                                var _fetchAssetData = _fetchData.RemasteredAssetsDecompressedData[_fetchAsset];
                                                File.Create(_fetchAssetPath).Using(_fetchStr => _fetchStr.Write(_fetchAssetData));
                                            });
                                        }

                                        var _fetchProgress = reportProgress(_fetchFilesCurrent, _fetchFilesTotal);

                                        if (!_fetchProgress || CancelToken.IsCancellationRequested)
                                            _fetchStateFile.Stop();
                                    }
                                });
                            }

                            if (CancelToken.IsCancellationRequested)
                                _fetchStateHeader.Stop();
                        });

                        if (CancelToken.IsCancellationRequested)
                            _fetchStateGame.Stop();
                    });
                });

                if (CancelToken.IsCancellationRequested)
                    return 0x03;
            }

            return 0x00;
        }
    }
}
