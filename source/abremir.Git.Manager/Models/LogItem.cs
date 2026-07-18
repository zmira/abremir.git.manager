namespace abremir.Git.Manager.Models;

internal record struct LogItem(DateTimeOffset Timestamp, LogType Type, string Message);
