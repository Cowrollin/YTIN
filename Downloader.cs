using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace YTIN;

public class Downloader
{
    private Process? _process;
    public double ProgressPercent { get; private set; }
    public string DownloadSpeed { get; private set; } = string.Empty;
    public string TimeRemaining { get; private set; } = string.Empty;
    private string StatusLine { get;  set; } = string.Empty;
    
    public event Action? OnDownloadFinished;
    public event Action? OnErrorOccurred;
    public event Action? OnProgressUpdated;

    public async Task<MediaInfo> GetMediaInfo(string url)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"--dump-json --no-warnings \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        string? jsonOutput = null;
        
        process.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                jsonOutput += e.Data;
            }
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Console.WriteLine("YT-DLP error: " + e.Data);
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(jsonOutput))
        {
            return ParseMedia(jsonOutput);
        }
        MainWindow.Logs.Add($"[ERROR] Url is invalid: {url}");
        return null!;
    }
    private static MediaInfo ParseMedia(string? json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var info = new MediaInfo
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Title = root.TryGetProperty("title", out var t) ? t.GetString() : null,
        };

        var formats = new ObservableCollection<MediaFormat>();
        if (root.TryGetProperty("formats", out var formatsElement))
        {
            foreach (JsonElement element in formatsElement.EnumerateArray())
            {
                var mediaFormat = new MediaFormat
                {
                    format_id = GetJsonValueAsString(element, "format_id"),
                    ext = GetJsonValueAsString(element, "ext"),
                    resolution = GetJsonValueAsString(element, "resolution"),
                    fps = GetJsonValueAsString(element, "fps")
                };
                formats.Add(mediaFormat);
            }
        }
        info.formats = formats;
        return info;
    }
    private static string? GetJsonValueAsString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }
    
    
    
    public async Task StartDownloadAsync(string arguments, MediaInfo mediaInfo)
    {
        _process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        
        _process.OutputDataReceived += (s, e) =>
        {
            ParseProgress(e.Data, mediaInfo);
        };
        _process.ErrorDataReceived += (s, e) =>
        {

        };
        _process.Exited += (s, e) => OnDownloadFinished?.Invoke();

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        
        await _process.WaitForExitAsync();
    }

    private void ParseProgress(string? line, MediaInfo mi)
    {
        Regex DownloadRegex = new Regex(
            @"\[download\]\s+(?<percent>\d+(?:[\.,]\d+)?)%\s+of\s+~?\s*[\d\.]+\w+\s+at\s+(?<speed>[\d\.]+\w+/s)\s+ETA\s+(?<eta>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        Regex FinishedRegex = new Regex(
            @"\[download\]\s+100%.*?in\s+(?<eta>\S+)\s+at\s+(?<speed>[\d\.]+\w+/s)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        if (line == null)
            return;
        
        var match = DownloadRegex.Match(line);
        if (match.Success)
        {
            var percent = double.Parse(match.Groups["percent"].Value.Replace('.', ','));
            if (mi.Progress <= percent && percent - 100 != 0 )
            {
                mi.Progress = percent;
            }
            mi.Speed = $"{match.Groups["speed"].Value.Trim()}c";
            mi.Eta = match.Groups["eta"].Value.Trim();
        }

        match = FinishedRegex.Match(line);
        if (match.Success)
        {
            mi.Progress = 100.0;
            mi.Speed = "0 KiB/s";
            mi.Eta = "00:00";
        }
    }
    
    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
            _process = null;
            MainWindow.Logs.Add($"[STOP] Stopped download");
        }
    }
}