using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DMarket;

public static class AppDiagnostics
{
    private const int MaxEntries = 200;
    private static readonly object SyncRoot = new();
    private static readonly List<DiagnosticEntry> Entries = new();

    private static bool _isEnabled;

    public static bool IsEnabled
    {
        get
        {
            lock (SyncRoot)
            {
                return _isEnabled;
            }
        }
        set
        {
            lock (SyncRoot)
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    Entries.Clear();
                }
            }
        }
    }

    public static void LogInfo(string source, string message)
    {
        Add("INFO", source, message, null);
    }

    public static void LogWarning(string source, string message)
    {
        Add("WARN", source, message, null);
    }

    public static void LogError(string source, Exception exception)
    {
        Add("ERROR", source, exception.Message, exception.ToString());
    }

    public static void LogError(string source, string message)
    {
        Add("ERROR", source, message, null);
    }

    public static void LogHttpFailure(string source, string url, int statusCode, string reasonPhrase, string responsePreview)
    {
        var message = $"HTTP {(statusCode <= 0 ? "?" : statusCode.ToString())} {reasonPhrase} | {url}";
        var detail = string.IsNullOrWhiteSpace(responsePreview)
            ? message
            : message + Environment.NewLine + responsePreview;
        Add("ERROR", source, message, detail);
    }

    public static IReadOnlyList<DiagnosticEntry> GetEntries()
    {
        lock (SyncRoot)
        {
            return Entries.Select(x => x.Clone()).ToList();
        }
    }

    public static string BuildText()
    {
        var entries = GetEntries();
        if (entries.Count == 0)
        {
            return "エラーは保持されていません。";
        }

        var builder = new StringBuilder();
        foreach (var entry in entries.OrderByDescending(x => x.Timestamp))
        {
            builder.AppendLine($"{entry.Timestamp:yyyy/MM/dd HH:mm:ss} [{entry.Level}] {entry.Source}");
            builder.AppendLine(entry.Message);
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                builder.AppendLine(entry.Detail);
            }
            builder.AppendLine(new string('-', 80));
        }

        return builder.ToString();
    }

    public static void Clear()
    {
        lock (SyncRoot)
        {
            Entries.Clear();
        }
    }

    private static void Add(string level, string source, string message, string? detail)
    {
        lock (SyncRoot)
        {
            if (!_isEnabled)
            {
                return;
            }

            Entries.Add(new DiagnosticEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Source = source,
                Message = message,
                Detail = detail ?? string.Empty
            });

            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
        }
    }
}

public class DiagnosticEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string Detail { get; set; } = "";

    public DiagnosticEntry Clone()
    {
        return new DiagnosticEntry
        {
            Timestamp = Timestamp,
            Level = Level,
            Source = Source,
            Message = Message,
            Detail = Detail
        };
    }
}
