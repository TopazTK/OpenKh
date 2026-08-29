using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Bbs;
using System;
using System.Collections.Generic;
using System.IO;
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

            try {  Repository.ListRemoteReferences(_fetchRelativeUri.ToString()); }
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
                    catch (LibGit2SharpException) {  }
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
    }
}
