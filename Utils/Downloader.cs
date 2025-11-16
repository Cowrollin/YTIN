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

    public async Task<MediaPlaylist> GetMediaInfo(string url)
    {
        _getInfoProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "yt-dlp.exe"),
                Arguments = $"--dump-single-json --no-warnings \"{url}\"",
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
                var playlist = new MediaPlaylist();
                return ParseMedia(jsonOutput, playlist);
            }
            else
            {
                MainWindow.Log($"No output received for URL: {url}", "[ERROR]");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"error ! {e}");
            MainWindow.Log($"Url is invalid: {url}", "[ERROR]");
            throw;
        }
        finally
        {
            _getInfoProcess.Dispose();
        }
        return null!;
    }
    
    private static MediaPlaylist ParseMedia(string? json, MediaPlaylist playlist)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (root.TryGetProperty("entries", out var entries))
        {
            foreach (JsonElement element in entries.EnumerateArray())
            {
                playlist.MediaList.Add(ParseSingleMedia(element.ToString()));
            }
            playlist.PlaylistTitle = root.GetProperty("title").GetString();
            playlist.Id = root.GetProperty("id").GetString();
        }
        else
        {
            playlist.MediaList.Add(ParseSingleMedia(json));
        }
        return playlist;
    }

    private static MediaInfo ParseSingleMedia(string? json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var info = new MediaInfo
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Title = root.TryGetProperty("title", out var t) ? t.GetString() : null,
            Url = root.TryGetProperty("webpage_url", out var url) ? url.GetString() : null,
        };
        
        if (root.TryGetProperty("formats", out var formatsElement))
        {
            foreach (JsonElement element in formatsElement.EnumerateArray())
            {
                info.formats.Add(ParseMediaFormat(element));
            }
        }

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

    private static MediaFormat ParseMediaFormat(JsonElement element)
    {
        return new MediaFormat
        {
            FormatId = GetJsonValueAsString(element, "format_id"),
            Ext = GetJsonValueAsString(element, "ext"),
            Resolution = GetJsonValueAsString(element, "resolution"),
            Fps = GetJsonValueAsString(element, "fps")
        };
    }
    
    
    /// <summary>
    /// Downloading media on url
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="mediaInfo"></param>
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

        try
        {
            _downloadProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                Console.WriteLine(e.Data);
                ParseProgress(e.Data, mediaInfo);
            };
            _downloadProcess.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                Console.WriteLine(e.Data);
                if (ParseError(e.Data, mediaInfo))
                {
                    throw new Exception("yt-dlp exited");
                }
            };
            _downloadProcess.Exited += (s, e) => OnDownloadFinished?.Invoke();

            _downloadProcess.Start();
            _downloadProcess.BeginOutputReadLine();
            _downloadProcess.BeginErrorReadLine();

            await _downloadProcess.WaitForExitAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            _downloadProcess.Dispose();
        }
        
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
            mi.Eta = $"{match.Groups["eta"].Value.Trim()}";
        }

        match = FinishedRegex.Match(line);
        if (match.Success)
        {
            mi.CompactSize = match.Groups["size"].Value.Trim();
            mi.Progress = 100.0;
            mi.Speed = "0 KiB/s";
            mi.Eta = "00:00";
        }
    }

    private bool ParseError(string errorLine, MediaInfo mi)
    {
        var knownErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            //{ "ERROR:", "Critical error during download." },
            { "HTTP Error", "Network issue or invalid URL." },
            { "404", "Video not found (HTTP 404)." },
            { "Unsupported URL", "Unsupported link format." },
            { "Unable to extract", "Video information could not be extracted." },
            { "Geo-restricted", "Video is restricted in your region." },
            { "This video is private", "The video is private." },
            { "Sign in to confirm your age", "Age restriction: login required." },
            { "broken", "Download broken or incomplete." },
        };
        
        var match = knownErrors.FirstOrDefault(kv => errorLine.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(match.Key))
        {
            AppLog.Write($"[YT-DLP ERROR] {match.Value} ({errorLine})", "ERROR");

            StopProcess(_downloadProcess, mi);
            return true;
        }
        else if (errorLine.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            AppLog.Write($"[YT-DLP ERROR] Unknown error: {errorLine}", "ERROR");

            StopProcess(_downloadProcess, mi);
            return true;
        }
        return false;
    }

    private static void StopProcess(Process process, MediaInfo mi)
    {
        try
        {
            if (process is {HasExited: false})
            {
                process.Kill(true);
                process.Dispose();
                MainWindow.Log($"Stopped download", "[WARN]");
            }
        }
        catch (Exception e)
        {
            MainWindow.Log($"The download stopped with an error: {e.Data}", "[ERROR]");
        }
    }
}