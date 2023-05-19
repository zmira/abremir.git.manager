using System.CommandLine;
using System.Diagnostics;
using abremir.Git.Manager;
using Kurukuru;
using Terminal.Gui;

if (!Debugger.IsAttached)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}

var pathOption = new Option<string>(new[] { "-p", "--path" }, description: "Path to base folder");

var rootCommand = new RootCommand
{
    pathOption
};

rootCommand.Description = "Manage your git repositories, in bulk";

rootCommand.SetHandler((string path) =>
{
    path ??= Environment.CurrentDirectory;

    RepositoryManager.UiInitialized += (_, __) =>
    {
        Spinner.Start($"Searching for git repositories in {path}", () => RepositoryManager.LoadRepositories(path), Patterns.Dots);
    };

    Application.Run<RepositoryManager>();
}, pathOption);

return rootCommand.Invoke(args);
