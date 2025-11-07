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
using System.Threading.Tasks;

namespace YTIN;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    
    private static readonly Dictionary<string, string> FormatMap = new()
    {
        { "256x144", "MP4 (144p)"          },
        { "426x240", "MP4 (240p)"          },
        { "640x360", "MP4 (360p)"          },
        { "854x480", "MP4 (480p)"          },
        { "1280x720", "MP4 (720p)"         },
        { "1920х1080", "MP4 (1080p)"       },
        { "2560x1440", "MP4 (1440p)"       },
        { "audio only", "MP3 (audio only)" },
    };
    private readonly Downloader _downloader = new();
    private readonly JsonHelper _jsonHelper = new();
    
    
    private string _inputUrl;
    private string _downloadDirectory;
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
    public string SelectedFormat { get; set; } = FormatMap.Values.First();
    
    
    public static ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<MediaInfo> DownloadsList { get; set; } = new(); // List downloads media
    public ObservableCollection<string> FormatList { get; } = new(FormatMap.Values); // List formats values for view (MainWindow.axaml)
    
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public MainWindow()
    {
        DataContext = this;
        Loaded += async (s, e) => await UpdateDownloadsListAsync();
        DownloadDirectory = "O:/Downloads";
        InputUrl = "https://youtu.be/PPRjukghBYE?si=UsDKzQt3AfWqmNXZ";
        InitializeComponent();
    }

    public void DownloadButton_IsClicked(object sender, RoutedEventArgs e)
    {
        _ = DownloadFile();
    }

    // change format
    public void ChangeFormatDownload_IsComboBoxSelect(object? sender, SelectionChangedEventArgs selectionChangedEventArgs)
    {
        SelectedFormat = selectionChangedEventArgs.AddedItems[0].ToString();
    }

    // select save folder
    private async void SetSaveDirectoryButton_IsClicked(object? sender, RoutedEventArgs e)
    {
        var folder = await SelectDownloadFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            if (DataContext is MainWindow vm)
                vm.DownloadDirectory = folder;
        }
    }
    
    // open folder dialog
    private async Task<string?> SelectDownloadFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для сохранения видео"
        };

        var result = await dialog.ShowAsync(this);
        return result;
    }

    // open folder
    private void DownloadItem_IsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MediaInfo item })
        {
            if (item.SavePath != "")
            {
                Process.Start("explorer.exe", @$"{Path.Combine(item.SavePath.Split('/'))}");
            }
        }
    }

    private void RemoveMediaItem_IsClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MediaInfo item })
        {
            _ = _jsonHelper.RemoveMediaByIdAsync(item.Id);
            DownloadsList.Remove(item);
        }
    }

    private async Task UpdateDownloadsListAsync()
    {
        var loadedList = await _jsonHelper.LoadHistoryFromJsonAsync();
    
        DownloadsList.Clear();
        foreach (var item in loadedList)
        {
            DownloadsList.Add(item);
        }
        Logs.Add($"[INFO] Load {DownloadsList?.Count ?? 0} records from history.");
    }
    
    private async Task DownloadFile()
    {
        string arguments;
        Logs.Add($"[GETS] Gets information from url: {InputUrl}.");
        var mediaInfo = await _downloader.GetMediaInfo(InputUrl);
        mediaInfo.Extinsion = SelectedFormat.Split(' ')[0].ToLower();
        mediaInfo.FileName = $"{mediaInfo.Title} [{mediaInfo.Id}].{mediaInfo.Extinsion}";
        if (mediaInfo.formats.Count != 0)
        {
            DownloadsList.Add(mediaInfo);
            foreach (var item in mediaInfo.formats)
            {
                Console.WriteLine($"id {item.FormatId} " +
                                  $"resol {item.Resolution} " +
                                  $"ext {item.Ext} " +
                                  $"fps {item.Fps}");
            }
            
            var matchingFormat = mediaInfo.formats.LastOrDefault(item =>
            {
                try
                {
                    return FormatMap.ContainsKey(item.Resolution) &&
                           SelectedFormat == FormatMap[item.Resolution] && 
                           item.Ext.ToLower() == SelectedFormat.Split(' ')[0].ToLower();
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
                mediaInfo.Format = SelectedFormat.Split(' ').ToString();
                mediaInfo.Fps = matchingFormat.Fps;
                arguments = $"-U -f {matchingFormat.FormatId} -P \"{DownloadDirectory}\" {InputUrl}";
                Console.WriteLine(arguments);
                Logs.Add($"[START] Download on title: {mediaInfo.Title}.{mediaInfo.Extinsion}.");
                await _downloader.StartDownloadAsync(arguments, mediaInfo);
                mediaInfo.SaveDate = DateTime.Now;
                await _jsonHelper.SaveMediaToHistoryJsonAsync(mediaInfo);
                Logs.Add($"[COMPLETE] Download on title: {mediaInfo.Title}.{mediaInfo.Extinsion}.");
            }
            else
            {
                Logs.Add($"[ERROR] Not have this format ({SelectedFormat}) in title: {mediaInfo.FileName}.");
            }
        }
        else
        {
            Logs.Add($"[ERROR] Not have any formats in title: {mediaInfo.FileName}.");
        }
    }
    
}