using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YTIN;
// https://www.youtube.com/watch?v=IgUXMfjCAS0
// https://youtu.be/PPRjukghBYE?si=UsDKzQt3AfWqmNXZ
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Убрать, заменить на id совпадающего формата
    private static readonly Dictionary<string, string> formats = new()
    {
        ["MP4 (144p)"] = "-f \"bv*[ext=mp4][height<=144]+ba[ext=m4a]/b[ext=mp4][height<=144]\"",
        ["MP4 (240p)"] = "-f \"bv[ext=mp4][height<=240]+ba/b[height<=240]\"",
        ["MP4 (360p)"] = "-f \"bv[ext=mp4][height<=360]+ba/b[height<=360]\"",
        ["MP4 (480p)"] = "-f \"bv[ext=mp4][height<=480]+ba/b[height<=480]\"",
        ["MP4 (720p)"] = "-f \"bv[ext=mp4][height<=720]+ba/b[height<=720]\"",
        ["MP4 (1080p)"] = "-f \"bv[ext=mp4][height<=1080]+ba/b[height<=1080]\"",
        ["MP4 (1440p)"] = "-f \"bv[ext=mp4][height<=1440]+ba/b[height<=1440]\"",
        ["MP3 (audio only)"] = "-x --audio-format mp3",
        ["WEBM"] = "-f \"bv[ext=webm]+ba/b[ext=webm]\"",
        ["list formats"] = "--list-formats" // tests
    };

    private static readonly Dictionary<string, string> FormatMap = new()
    {
        { "256x144", "MP4 (144p)"},
        { "426x240", "MP4 (240p)" },
        { "640x360", "MP4 (360p)" },
        { "854x480", "MP4 (480p)" },
        { "1280x720", "MP4 (720p)" },
        { "1920х1080", "MP4 (1080p)" },
        { "2560x1440", "MP4 (1440p)" },
        { "audio only", "MP3 (audio only)" },
    };
    
    private string _inputUrl;
    public string InputUrl { 
        get => _inputUrl;
        set
        {
            if (_inputUrl != value)
            {
                _inputUrl = value;
                OnPropertyChanged();
            }
        } }
    private string _downloadDirectory;
    public string DownloadDirectory { 
        get => _downloadDirectory;
        set
        {
            if (_downloadDirectory != value)
            {
                _downloadDirectory = value;
                OnPropertyChanged();
            }
        } }
    public static ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<MediaInfo> DownloadsList { get; set; } = new();
    public ObservableCollection<string> FormatList { get; } = new(FormatMap.Values);
    public string FormatDownload { get; set; } = FormatMap.Values.First();
    private readonly Downloader _downloader = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public MainWindow()
    {
        DataContext = this;
        DownloadDirectory = "D:/Downloads/VideoYTIN";
        InputUrl = "https://youtu.be/PPRjukghBYE?si=UsDKzQt3AfWqmNXZ";
        var mitest = new MediaInfo
        {
            FileName = "«День народного единства». Познавательный мультфильм к уроку «Разговоры о важном» [2A3FkpOEW9c].mp4",
            Progress = 59.9,
            Speed = "111kib/s",
            Eta = "99:99c"
        };
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        DownloadsList.Add(mitest);
        InitializeComponent();
    }

    public void DownloadButton_IsClicked(object sender, RoutedEventArgs e)
    {
        _ = DownloadFile();
    }

    public void ChangeFormatDownload_IsComboBoxSelect(object? sender, SelectionChangedEventArgs selectionChangedEventArgs)
    {
        FormatDownload = selectionChangedEventArgs.AddedItems[0].ToString();
        Console.WriteLine(FormatDownload);
    }

    private async void SetSaveDirectoryButton_IsClicked(object? sender, RoutedEventArgs e)
    {
        var folder = await SelectDownloadFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            if (DataContext is MainWindow vm)
                vm.DownloadDirectory = folder;
        }
    }
    
    private async Task<string?> SelectDownloadFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для сохранения видео"
        };

        var result = await dialog.ShowAsync(this);
        return result;
    }

    private async Task DownloadFile()
    {
        string arguments;
        Logs.Add($"[GETS] Gets information from url: {InputUrl}");
        var mediaInfo = await _downloader.GetMediaInfo(InputUrl);
        mediaInfo.Extinsion = FormatDownload.Split(' ')[0].ToLower();
        mediaInfo.FileName = $"{mediaInfo.Title} [{mediaInfo.Id}].{mediaInfo.Extinsion}";
        if (mediaInfo.formats.Count != 0)
        {
            DownloadsList.Add(mediaInfo);
            foreach (var item in mediaInfo.formats)
            {
                Console.WriteLine($"id {item.format_id} " +
                                  $"resol {item.resolution} " +
                                  $"ext {item.ext} " +
                                  $"fps {item.fps}");
            }
            
            var matchingFormat = mediaInfo.formats.LastOrDefault(item =>
            {
                try
                {
                    return FormatMap.ContainsKey(item.resolution) &&
                           FormatDownload == FormatMap[item.resolution];
                }
                catch
                {
                    return false;
                }
            });

            if (matchingFormat != null)
            {
                mediaInfo.Url = InputUrl;
                mediaInfo.SavePath = DownloadDirectory;
                arguments = $"-f {matchingFormat.format_id} -P \"{DownloadDirectory}\" {InputUrl}";
                Console.WriteLine(arguments);
                Logs.Add($"[START] Download on title: {mediaInfo.Title}.{mediaInfo.Extinsion}");
                await _downloader.StartDownloadAsync(arguments, mediaInfo);
                Logs.Add($"[COMPLETE] Download on title: {mediaInfo.Title}.{mediaInfo.Extinsion}");
            }
            else
            {
                Logs.Add($"[ERROR] Not have this format ({FormatDownload}) in title: {mediaInfo.FileName}");
            }
        }
        else
        {
            Logs.Add($"[ERROR] Not have any formats in title: {mediaInfo.FileName}");
        }
        
    }

    private void DownloadItem_IsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is MediaInfo item)
        {
            if (File.Exists($"{item.SavePath}/{item.FileName}"))
            {
                Process.Start("explorer.exe", @$"{Path.Combine(item.SavePath.Split('/'))}");
            }
        }
        
    }
}