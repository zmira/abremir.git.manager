namespace abremir.Git.Manager.Models
{
    internal record LogItem(DateTimeOffset Timestamp, LogType Type, string Message);
}
