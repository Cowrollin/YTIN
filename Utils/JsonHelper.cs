using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YTIN;

public class JsonHelper
{
    private readonly string _historyPath;

    public JsonHelper(string filename = "history.json")
    {
        _historyPath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, filename);
    }

    public class HistoryContainer
    {
        public ObservableCollection<HistoryEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Overwrites the history to a json file
    /// </summary>
    /// <param name="mediaList"> List for save</param>
    private async Task SaveHistoryToJsonAsync(HistoryContainer container)
    {
        try
        {
            var json = JsonConvert.SerializeObject(container, Formatting.Indented);
            await File.WriteAllTextAsync(_historyPath, json);
            Console.WriteLine("[history.json] Save media list.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[history.json] Save list ERROR : {e}.");
            throw;
        }
    }
    
    /// <summary>
    /// Adds one object to the json file
    /// </summary>
    /// <param name="mediaInfo"> Object for save</param>
    public async Task SaveMediaToHistoryJsonAsync(MediaInfo mediaInfo)
    {
        var history  = await LoadHistoryFromJsonAsync();
        history.Entries.Add(new HistoryEntry
        {
            Type = "video",
            Media = mediaInfo
        });
        await SaveHistoryToJsonAsync(history);
        MainWindow.Log($"Save new media. Title: {mediaInfo.Title}.", "[INFO]");
    }

    /// <summary>
    /// Loads the history from a json file. Creates a new file if the file does not exist
    /// </summary>
    /// <returns>List[MediaInfo]</returns>
    public async Task<HistoryContainer> LoadHistoryFromJsonAsync()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                Console.WriteLine("[history.json] File does not exists. New file is created.");
                return new HistoryContainer();
            }

            var json = await File.ReadAllTextAsync(_historyPath);
            var history  = JsonConvert.DeserializeObject<HistoryContainer>(json);
            return history ?? new HistoryContainer();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[history.json] Load ERROR: {e}.");
            throw;
        }
    }

    /// <summary>
    /// Deletes one object by id
    /// </summary>
    /// <param name="id">MediaInfo.Id</param>
    public async Task RemoveMediaByIdAsync(string id)
    {
        var history = await LoadHistoryFromJsonAsync();

        var toRemove = history.Entries.FirstOrDefault(e => e.Type == "video" && e.Media?.Id == id);
        if (toRemove != null)
        {
            history.Entries.Remove(toRemove);
            MainWindow.Log($"Removed media: {toRemove.Media.Title}", "[INFO]");
        }
        
        foreach (var entry in history.Entries.Where(e => e.Type == "playlist" && e.Playlist?.MediaList != null))
        {
            var mediaToRemove = entry.Playlist.MediaList.FirstOrDefault(v => v.Id == id);
            if (mediaToRemove != null)
            {
                entry.Playlist.MediaList.Remove(mediaToRemove);
                MainWindow.Log($"Removed from playlist '{entry.Playlist.PlaylistTitle}': {mediaToRemove.Title}", "[INFO]");
            }
        }

        await SaveHistoryToJsonAsync(history);
    }
    
    public async Task SavePlaylistToHistoryJsonAsync(MediaPlaylist playlist)
    {
        var history = await LoadHistoryFromJsonAsync();

        history.Entries.Add(new HistoryEntry
        {
            Type = "playlist",
            Playlist = playlist
        });

        await SaveHistoryToJsonAsync(history);
        MainWindow.Log($"Saved playlist: {playlist.PlaylistTitle}", "[INFO]");
    }

    public async Task RemovePlaylistByTitleAsync(string playlistTitle)
    {
        var history = await LoadHistoryFromJsonAsync();

        var toRemove = history.Entries.FirstOrDefault(e =>
            e.Type == "playlist" && e.Playlist?.PlaylistTitle == playlistTitle);

        if (toRemove != null)
        {
            history.Entries.Remove(toRemove);
            await SaveHistoryToJsonAsync(history);
            MainWindow.Log($"Removed playlist: {playlistTitle}", "[INFO]");
        }
    }
}

public class HistoryEntry
{
    public string Type { get; set; }
    public MediaInfo? Media { get; set; }
    public MediaPlaylist? Playlist { get; set; }
}