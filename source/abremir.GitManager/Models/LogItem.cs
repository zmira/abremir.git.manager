namespace abremir.GitManager.Models;

internal record struct LogItem(DateTimeOffset Timestamp, LogType Type, string Message);
