using LibGit2Sharp;

namespace abremir.Git.Manager
{
    internal static class RepositoryLister
    {
        internal static List<Repository> ListRepos(string baseFolder)
        {
            var repoList = new List<Repository>();

            if (Repository.IsValid(baseFolder))
            {
                var repository = new Repository(baseFolder);

                if (repository.Info.IsBare
                    || repository.Info.IsHeadUnborn
                    || repository.Info.IsHeadDetached
                    || repository.Head.IsRemote
                    || !repository.Head.IsTracking)
                {
                    return repoList;
                }

                repoList.Add(repository);

                return repoList;
            }

            var subdirectories = Directory.GetDirectories(baseFolder, "*", new EnumerationOptions { IgnoreInaccessible = true })
                .Where(directory => !Path.GetFileName(directory)!.StartsWith('.'));
            foreach (var subdirectory in subdirectories ?? [])
            {
                repoList.AddRange(ListRepos(subdirectory));
            }

            return repoList;
        }
    }
}
