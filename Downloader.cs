using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace YTIN;

public class Downloader
{
    private Process? _downloadProcess;
    private Process? _getInfoProcess;
    public event Action? OnDownloadFinished;
    public event Action? OnErrorOccurred;
    public event Action? OnProgressUpdated;

    public async Task<MediaInfo> GetMediaInfo(string url)
    {
        _getInfoProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "yt-dlp.exe"),
                Arguments = $"--dump-json --no-warnings \"{url}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        
        try
        {
            _getInfoProcess.Start();
            
            var outputTask = _getInfoProcess.StandardOutput.ReadToEndAsync();
            var errorTask = _getInfoProcess.StandardError.ReadToEndAsync();
            
            await _getInfoProcess.WaitForExitAsync();
            await Task.WhenAll(outputTask, errorTask);
            
            string jsonOutput = outputTask.Result;
            string errorOutput = errorTask.Result;
            
            if (!string.IsNullOrEmpty(errorOutput))
            {
                Console.WriteLine("YT-DLP error: " + errorOutput);
            }
        
            if (!string.IsNullOrWhiteSpace(jsonOutput))
            {
                return ParseMedia(jsonOutput);
            }
            else
            {
                MainWindow.Logs.Add($"[ERROR] No output received for URL: {url}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"error ! {e}");
            MainWindow.Logs.Add($"[ERROR] Url is invalid: {url}");
            throw;
        }
        finally
        {
            _getInfoProcess.Dispose();
        }
        
        return null!;
    }
    
    private static MediaInfo ParseMedia(string? json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var info = new MediaInfo
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Title = root.TryGetProperty("title", out var t) ? t.GetString() : null
        };

        var formats = new ObservableCollection<MediaFormat>();
        if (root.TryGetProperty("formats", out var formatsElement))
        {
            foreach (JsonElement element in formatsElement.EnumerateArray())
            {
                var mediaFormat = new MediaFormat
                {
                    FormatId = GetJsonValueAsString(element, "format_id"),
                    Ext = GetJsonValueAsString(element, "ext"),
                    Resolution = GetJsonValueAsString(element, "resolution"),
                    Fps = GetJsonValueAsString(element, "fps")
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
        _downloadProcess = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "yt-dlp.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        
        _downloadProcess.OutputDataReceived += (s, e) =>
        {
            Console.WriteLine(e.Data);
            ParseProgress(e.Data, mediaInfo);
        };
        _downloadProcess.ErrorDataReceived += (s, e) =>
        {
            Console.WriteLine(e.Data);
        };
        _downloadProcess.Exited += (s, e) => OnDownloadFinished?.Invoke();

        _downloadProcess.Start();
        _downloadProcess.BeginOutputReadLine();
        _downloadProcess.BeginErrorReadLine();
        
        await _downloadProcess.WaitForExitAsync();
        _downloadProcess.Dispose();
    }

    private void ParseProgress(string? line, MediaInfo mi)
    {
        Regex DownloadRegex = new Regex(
            @"\[download\]\s+(?<percent>\d+(?:[\.,]\d+)?)%\s+of\s+~?\s*[\d\.]+\w+\s+at\s+(?<speed>[\d\.]+\w+/s)\s+ETA\s+(?<eta>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        Regex FinishedRegex = new Regex(
            @"\[download\]\s+100%\s+of\s+(?<size>[\d\.]+\s*\w+)",
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
            mi.CompactSize = match.Groups["size"].Value.Trim();
            mi.Progress = 100.0;
            mi.Speed = "-- KiB/s";
            mi.Eta = "--:--";
        }
    }
    
    public void Stop()
    {
        if (_downloadProcess != null && !_downloadProcess.HasExited)
        {
            _downloadProcess.Kill();
            _downloadProcess.Dispose();
            _downloadProcess = null;
            MainWindow.Logs.Add($"[STOP] Stopped download");
        }
    }
}