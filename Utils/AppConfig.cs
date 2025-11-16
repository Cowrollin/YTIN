using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace YTIN;

public static class AppConfig
{
    public static string DownloadPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public static string DownloadFormat { get; set; } = "MP4 (720p)";
    public static int MAX_LOG_LENGTH { get; set; } = 1024;

    private static readonly string ConfigFilePath =  Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "config.cfg");
    public static void Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            Save();
            return;
        }
        
        var lines = File.ReadAllLines(ConfigFilePath);
        var dict = lines
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#") && l.Contains('='))
            .Select(l => l.Split('=',2))
            .ToDictionary(k => k[0].Trim(), v => v[1].Trim());
        
        foreach (var prop in typeof(AppConfig).GetProperties(BindingFlags.Static | BindingFlags.Public))
        {
            if (!dict.TryGetValue(prop.Name, out var parameter)) continue;

            try
            {
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var convertedValue = Convert.ChangeType(parameter, targetType);
                prop.SetValue(null, convertedValue);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
    
    public static void Save()
    {
        var lines = new List<string>();

        foreach (var prop in typeof(AppConfig).GetProperties(BindingFlags.Static | BindingFlags.Public))
        {
            var value = prop.GetValue(null);
            lines.Add($"{prop.Name}={value}");
        }

        File.WriteAllLines(ConfigFilePath, lines);
    }
}