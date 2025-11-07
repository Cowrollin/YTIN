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

    /// <summary>
    /// Overwrites the history to a json file
    /// </summary>
    /// <param name="mediaList"> List for save</param>
    private async Task SaveHistoryToJsonAsync(ObservableCollection<MediaInfo> mediaList)
    {
        try
        {
            var json = JsonConvert.SerializeObject(mediaList, Formatting.Indented);
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
        ObservableCollection<MediaInfo> mediaList = await LoadHistoryFromJsonAsync();
        mediaList.Add(mediaInfo);
        await SaveHistoryToJsonAsync(mediaList);
        MainWindow.Logs.Add($"[INFO] Save new media. Title: {mediaInfo.Title}.");
    }

    /// <summary>
    /// Loads the history from a json file. Creates a new file if the file does not exist
    /// </summary>
    /// <returns>List[MediaInfo]</returns>
    public async Task<ObservableCollection<MediaInfo>> LoadHistoryFromJsonAsync()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                Console.WriteLine("[history.json] File does not exists. New file is created.");
                return new ObservableCollection<MediaInfo>();
            }

            var json = await File.ReadAllTextAsync(_historyPath);
            var mediaList = JsonConvert.DeserializeObject<ObservableCollection<MediaInfo>>(json);
            return mediaList ?? new ObservableCollection<MediaInfo>();
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
        var mediaList = await LoadHistoryFromJsonAsync();
        var removeItem = mediaList.FirstOrDefault(m => m.Id == id);
        if (removeItem is not null)
        {
            mediaList.Remove(removeItem);
            await SaveHistoryToJsonAsync(mediaList);
            MainWindow.Logs.Add($"[INFO] Remove media. Title: {removeItem.Title}.");
        }
    }
    
    /// <summary>
    /// Clears the history
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        await SaveHistoryToJsonAsync(new ObservableCollection<MediaInfo>());
        MainWindow.Logs.Add($"[INFO] History cleared.");
    }
}