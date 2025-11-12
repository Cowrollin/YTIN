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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YTIN;

// логика, если нет формата, берем следующий в списке по нижней границе 
// поиск &list= значит плейлист
// https://www.youtube.com/watch?v=IzuBj1vDGYM&list=PLxi_U6Wx-TZW5T015lBiiMA0Wbr39PfhK

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
    private string _downloadDirectory = AppConfig.DownloadPath;
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
    public ObservableCollection<IDownloadItem> DownloadsList { get; set; } = new();
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
        _ = AppConfig.LoadAsync();
        DownloadDirectory = AppConfig.DownloadPath;
        // DownloadDirectory = "D:/Downloads/VideoYTIN";
        InputUrl = "https://youtu.be/PPRjukghBYE?si=UsDKzQt3AfWqmNXZ";
        InitializeComponent();
    }

    public void DownloadButton_IsClicked(object sender, RoutedEventArgs e)
    {
        DownloadFile();
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
            AppConfig.DownloadPath = folder;
            await AppConfig.SaveAsync();
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
        if (sender is Button { Tag: MediaInfo media })
        {
            _ = _jsonHelper.RemoveMediaByIdAsync(media.Id);
            var single = DownloadsList.OfType<MediaInfo>().FirstOrDefault(v => v.Id == media.Id);
            if (single is not null)
            {
                DownloadsList.Remove(single);
                _ = _jsonHelper.RemoveMediaByIdAsync(media.Id);
                Logs.Add($"[INFO] Removed media: {media.FileName}");
                return;
            }
            
            var playlist = DownloadsList.OfType<MediaPlaylist>()
                .FirstOrDefault(p => p.MediaList.Any(v => v.Id == media.Id));

            if (playlist is not null)
            {
                var videoToRemove = playlist.MediaList.First(v => v.Id == media.Id);
                playlist.MediaList.Remove(videoToRemove);
                _ = _jsonHelper.RemoveMediaByIdAsync(media.Id);
                Logs.Add($"[INFO] Removed media: {media.FileName}");
            }
        }
    }

    private async Task UpdateDownloadsListAsync()
    {
        try
        {
            var history = await _jsonHelper.LoadHistoryFromJsonAsync();

            DownloadsList.Clear();
            foreach (var entry in history.Entries)
            {
                switch (entry.Type)
                {
                    case "Video" when entry.Media!= null:
                        DownloadsList.Add(entry.Media);
                        break;

                    case "playlist" when entry.Playlist != null:
                        DownloadsList.Add(entry.Playlist);
                        break;
                }
            }
            Logs.Add($"[INFO] Loaded {DownloadsList.Count} media and {DownloadsList.OfType<MediaInfo>().Count() +
                                                                      DownloadsList.OfType<MediaPlaylist>().Sum(p => p.MediaList.Count)} playlists from history.");
        }
        catch (Exception ex)
        {
            Logs.Add($"[ERROR] Failed to load history: {ex.Message}");
            Console.WriteLine($"[history.json] Load error: {ex}");
        }
    }
    
    private async void DownloadFile()
    {
        try
        {
            Logs.Add($"[GETS] Gets information from url: {InputUrl}.");
            var playlist = await _downloader.GetMediaInfo(InputUrl);

            if (playlist.MediaList.Count == 1)
            {
                var media = playlist.MediaList.First();
                DownloadsList.Add(media);
                Download(media);
                await _jsonHelper.SaveMediaToHistoryJsonAsync(media);
            }
            else if (playlist.MediaList.Count > 1)
            {
                DownloadsList.Add(playlist);
                foreach (var media in playlist.MediaList)
                {
                    Download(media, playlist.PlaylistTitle);
                }
                await _jsonHelper.SavePlaylistToHistoryJsonAsync(playlist);
            }
            else // TODO log
            {
                Console.WriteLine("Ссылка нерабочая");
            }
        }
        catch (Exception e)
        {
            throw; // TODO handle exception
        }
    }

    private async void Download(MediaInfo mediaInfo, string playlistName = "")
    {
        try
        {
            string arguments;
            mediaInfo.Extinsion = SelectedFormat.Split(' ')[0].ToLower();
            mediaInfo.FileName = $"{mediaInfo.Title} [{mediaInfo.Id}].{mediaInfo.Extinsion}";
            if (mediaInfo.formats.Count != 0)
            {
                                    foreach (var item in mediaInfo.formats)
                                    {
                                        Console.WriteLine($"id {item.FormatId} " +
                                                          $"resol {item.Resolution} " +
                                                          $"ext {item.Ext} " +
                                                          $"fps {item.Fps}");
                                    }

                var miFormat = GetFormatID(mediaInfo.formats.ToList());

                if (miFormat != null)
                {
                    mediaInfo.SavePath = playlistName != "" ? $"{DownloadDirectory}/{playlistName}" : DownloadDirectory;
                    mediaInfo.Format = SelectedFormat.Split(' ').ToString();
                    mediaInfo.Fps = miFormat.Fps;
                    arguments = $"-U -f {miFormat.FormatId} -P \"{mediaInfo.SavePath}\" {mediaInfo.Url}";
                    Console.WriteLine(arguments);
                    Logs.Add($"[START] Download on title: {mediaInfo.Title}.{mediaInfo.Extinsion}.");
                    await _downloader.StartDownloadAsync(arguments, mediaInfo);
                    mediaInfo.SaveDate = DateTime.Now;
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
        catch (Exception e)
        {
            throw; // TODO handle exception
        }
    }

    private MediaFormat? GetFormatID(List<MediaFormat> formats)
    {
        var selectedExt = SelectedFormat.Split(' ')[0].ToLower();
        var selectedRes = FormatMap.FirstOrDefault(x => x.Value == SelectedFormat).Key;
        var selectedHeight = ParseResolution(selectedRes);
        
        var resultFormat = formats
            .Where(f => f.Ext.Equals(selectedExt, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(f => f.Resolution == selectedRes) ?? 
            formats
            .Where(f => f.Ext.Equals(selectedExt, StringComparison.OrdinalIgnoreCase))
            .Select(f => new { Format = f, Height = ParseResolution(f.Resolution) })
            .Where(x => x.Height < selectedHeight && x.Height > 0)
            .OrderByDescending(x => x.Height)
            .FirstOrDefault()?.Format;

        return resultFormat;
    }
    
    private int ParseResolution(string resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution))
            return 0;
        
        var match = Regex.Match(resolution, @"(?:(\d{3,4})x(\d{3,4}))|(\d{3,4})p", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (match.Groups[2].Success)
                return int.Parse(match.Groups[2].Value);
            if (match.Groups[3].Success)
                return int.Parse(match.Groups[3].Value);
        }
        return 0;
    }
}