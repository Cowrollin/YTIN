using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace YTIN;

public static class AppConfig
{
    public static string DownloadPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    
    
    private static readonly string _configFilePath =  Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "config.cfg");

    public static async Task LoadAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            await SaveAsync();
            return;
        }
        
        var lines = await File.ReadAllLinesAsync(_configFilePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "DownloadPath":
                    DownloadPath = value;
                    break;
                // add parameter
            }
        }
    }
    
    public static async Task SaveAsync()
    {
        var lines = new List<string>
        {
            $"DownloadPath={DownloadPath}",
        };

        await File.WriteAllLinesAsync(_configFilePath, lines);
    }
}