using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Bbs;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Services
{
    public static class GitService
    {
        public static CancellationTokenSource CancelTokenSource = new CancellationTokenSource();
        public static CancellationToken CancelToken = CancelTokenSource.Token;

        public static async Task<byte> InstallGit(string modPath, string repoName, TransferProgressHandler reportProgress)
        {
            var _fetchPlatform = repoName.Contains('@') ? repoName.Split('@').Last() : null;
            repoName = _fetchPlatform != null ? repoName.Replace("@" + _fetchPlatform, "") : repoName;

            var _fetchBranch = repoName.Contains(':') ? repoName.Split(':').Last() : null;
            repoName = _fetchBranch != null ? repoName.Replace(":" + _fetchBranch, "") : repoName;

            var _fetchAuthor = repoName.Split('/').First();
            var _fetchName = repoName.Split('/').Last();

            var _fetchCurrentModDir = Path.Combine(modPath, _fetchName);
            var _fetchCurrentGitPath = Path.Combine(_fetchCurrentModDir, ".git");

            var _fetchBaseUri = new Uri("https://" + (_fetchPlatform != null ? _fetchPlatform : "github.com"));
            var _fetchRelativeUri = new Uri(_fetchBaseUri, $"{repoName}");

            var _cloneOptions = new CloneOptions
            {
                Checkout = true,
                BranchName = _fetchBranch,
            };

            _cloneOptions.FetchOptions.Depth = 1;
            _cloneOptions.FetchOptions.Prune = true;

            _cloneOptions.FetchOptions.OnTransferProgress = reportProgress;

            if (!Directory.Exists(_fetchCurrentModDir))
                Directory.CreateDirectory(_fetchCurrentModDir);

            IEnumerable<Reference>? _fetchRemotes = null;

            try { _fetchRemotes = Repository.ListRemoteReferences(_fetchRelativeUri.ToString()); }
            catch (LibGit2SharpException)
            {
                Directory.Delete(_fetchCurrentModDir, true);
                return 0x01;
            }

            if (_fetchPlatform == null)
            {
                using var _makeClient = new HttpClient();
                using var _fetchResponse = await _makeClient.GetAsync($"https://raw.githubusercontent.com/{repoName}/" + (_fetchBranch != null ? _fetchBranch : _fetchRemotes.First().TargetIdentifier) + "/mod.yml", HttpCompletionOption.ResponseHeadersRead);

                if (_fetchResponse.StatusCode != HttpStatusCode.OK)
                    return 0x01;

                await Task.Run(() =>
                {
                    try { Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions); }
                    catch (LibGit2SharpException) { }
                });

                if (CancelToken.IsCancellationRequested)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x03;
                }

                var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                    _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
            }

            else
            {
                await Task.Run(() =>
                {
                    try
                    { Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions); }
                    catch (LibGit2SharpException) { }
                });

                if (CancelToken.IsCancellationRequested)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x03;
                }

                var _fetchGit = new Repository(_fetchCurrentModDir);

                var _fetchCommit = _fetchGit.Head.Tip;
                var _doesModFileExist = _fetchCommit["mod.yml"] != null;

                _fetchGit.Dispose();

                var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                    _fetchFile.Attributes &= ~FileAttributes.ReadOnly;

                if (!_doesModFileExist)
                {
                    Directory.Delete(_fetchCurrentModDir, true);
                    return 0x01;
                }
            }

            return 0x00;
        }

        public static async Task<byte> InstallLocal(string modPath, string fileName, Func<int, int, bool> reportProgress)
        {
            var _fetchExtension = Path.GetExtension(fileName).ToLower();
            var _fetchCurrentModDir = Path.Combine(modPath, Path.GetFileNameWithoutExtension(fileName));

            if (_fetchExtension == ".zip")
            {
                using (var _fileStream = new FileStream(fileName, FileMode.Open))
                {
                    var _fetchArchive = new ZipArchive(_fileStream, ZipArchiveMode.Read);

                    if (_fetchArchive.Entries.FirstOrDefault(x => x.Name == "mod.yml") == null)
                        return 0x01;

                    if (!Directory.Exists(_fetchCurrentModDir))
                        Directory.CreateDirectory(_fetchCurrentModDir);

                    await Task.Run(() =>
                    {
                        for (int i = 0; i < _fetchArchive.Entries.Count; i++)
                        {
                            var _fetchEntry = _fetchArchive.Entries[i];

                            if (_fetchEntry.FullName.EndsWith('/'))
                                continue;

                            var _fetchFileTarget = Path.Combine(_fetchCurrentModDir, _fetchEntry.FullName);
                            var _fetchDirectory = Path.Combine(_fetchCurrentModDir, Path.GetDirectoryName(_fetchEntry.FullName));

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
                }
            }

            else if (_fetchExtension == ".lua")
            {
                var _fetchCurrentLuaName = Path.Combine(_fetchCurrentModDir, Path.GetFileName(fileName));

                if (!Directory.Exists(_fetchCurrentModDir))
                    Directory.CreateDirectory(_fetchCurrentModDir);

                File.Copy(fileName, _fetchCurrentLuaName);

                var _fetchMetadata = new Metadata();

                _fetchMetadata.Title = Path.GetFileNameWithoutExtension(fileName) + " (Lua)";
                _fetchMetadata.Description = "This Metadata has been automatically generated for this Lua Modification.";
                _fetchMetadata.Assets = new List<AssetFile>();

                var _createSource = new AssetFile() { Name = Path.GetFileName(fileName) };
                var _createFile = new AssetFile()
                {
                    Name = "scripts/" + Path.GetFileName(fileName),
                    Method = "copy",
                    Source = new List<AssetFile>() { _createSource },
                };

                _fetchMetadata.Assets.Add(_createFile);

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
                                    _fetchMetadata.Title = "\"" + _lineGib + "\"";
                                    break;
                                case "LUAGUI_AUTH":
                                    _fetchMetadata.OriginalAuthor = "\"" + _lineGib + "\"";
                                    break;
                                case "LUAGUI_DESC":
                                    _fetchMetadata.Description = "\"" + _lineGib + "\"";
                                    break;
                            }
                        }
                    }
                }

                var _yamlPath = Path.Combine(_fetchCurrentModDir, "mod.yml");
                File.WriteAllText(_yamlPath, _fetchMetadata.ToString());
            }

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

            return 0x00;
        }
    }
}
