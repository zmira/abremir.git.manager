using System.CommandLine;
using System.Diagnostics;
using abremir.Git.Manager;
using Kurukuru;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

if (!Debugger.IsAttached)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}

var pathOption = new Option<string>("--path", ["-p"])
{
    Description = "Path to base folder"
};

var rootCommand = new RootCommand("Manage your git repositories, in bulk")
{
    pathOption
};

rootCommand.SetAction(async parseResult =>
{
    var path = parseResult.GetValue(pathOption) ?? Environment.CurrentDirectory;

    RepositoryManager.UiInitialized += (source, _) => Spinner.Start($"Searching for git repositories in {path}", spinner =>
    {
        ((RepositoryManager)source!).LoadRepositories(path, false);
        spinner.Text = string.Empty;
    }, Patterns.Dots);

    ConfigurationManager.Enable(ConfigLocations.All);
    ThemeManager.Theme = "Anders";
    using IApplication app = Application.Create().Init();
    app.Run<RepositoryManager>();
});

await rootCommand.Parse(args).InvokeAsync();

Console.Clear();
