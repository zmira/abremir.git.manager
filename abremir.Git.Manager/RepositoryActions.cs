using System.Collections.Concurrent;
using System.Diagnostics;
using abremir.Git.Manager.Models;
using LibGit2Sharp;

namespace abremir.Git.Manager
{
    internal static class RepositoryActions
    {
        private static readonly ConcurrentDictionary<string, (string? Username, string? Password)> _credentials = new();

        internal static void Fetch(Repository repository)
        {
            var remote = repository.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

            Commands.Fetch(repository, remote.Name, refSpecs, GetFetchOptions(), null);
        }

        internal static void Pull(Repository repository)
        {
            Commands.Pull(repository, repository.Config.BuildSignature(DateTimeOffset.Now), new PullOptions { FetchOptions = GetFetchOptions() });
        }

        internal static RepositoryStatus RetrieveStatus(Repository repository)
        {
            return repository.RetrieveStatus();
        }

        internal static void Reset(Repository repository, Commit commit)
        {
            repository.Reset(ResetMode.Hard, commit);
        }

        internal static List<ChangedItem> GetChanges(Repository repository)
        {
            var treeChanges = repository.Diff.Compare<TreeChanges>(repository.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);
            var repositoryPatch = repository.Diff.Compare<Patch>(repository.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);

            var changedItems = new List<ChangedItem>();

            foreach (var treeChange in treeChanges)
            {
                changedItems.Add(new(treeChange.Path, treeChange.Status, repositoryPatch.FirstOrDefault(patch => patch.Path == treeChange.Path)?.Patch ?? string.Empty));
            }

            return changedItems;
        }

        private static FetchOptions GetFetchOptions()
        {
            static Credentials credentialsProvider(string url, string _, SupportedCredentialTypes __)
            {
                var (Username, Password) = GetCredentials(url);

                return new UsernamePasswordCredentials()
                {
                    Username = Username,
                    Password = Password
                };
            }

            return new FetchOptions { CredentialsProvider = credentialsProvider, Prune = true, TagFetchMode = TagFetchMode.Auto };
        }

        private static (string? Username, string? Password) GetCredentials(string url)
        {
            var uri = new Uri(url);

            if (!_credentials.ContainsKey(uri.Host))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "credential fill",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = new Process
                {
                    StartInfo = startInfo
                };

                process.Start();

                // Write query to stdin.
                // For stdin to work we need to send \n instead of WriteLine
                // We need to send empty line at the end
                process.StandardInput.NewLine = "\n";
                process.StandardInput.WriteLine($"protocol={uri.Scheme}");
                process.StandardInput.WriteLine($"host={uri.Host}");
                process.StandardInput.WriteLine($"path={uri.AbsolutePath}");
                process.StandardInput.WriteLine();

                // Get user/pass from stdout
                string? username = null;
                string? password = null;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    if (line.StartsWith("username", StringComparison.OrdinalIgnoreCase))
                    {
                        username = line[(line.IndexOf('=') + 1)..];
                    }
                    else if (line.StartsWith("password", StringComparison.OrdinalIgnoreCase))
                    {
                        password = line[(line.IndexOf('=') + 1)..];
                    }
                }

                _credentials.TryAdd(uri.Host, new(username, password));
            }

            return _credentials[uri.Host];
        }
    }
}
