using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YTIN;

public static class AppLog
{
    /// <summary>
    /// Max lines of log
    /// </summary>
    private static readonly int MAX_LOG_LENGTH = AppConfig.MAX_LOG_LENGTH;
    
    /// <summary>
    /// File path and name ("../YTIN/log.cfg")
    /// </summary>
    private static readonly string LogFilePath = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "Log.txt");

    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message">Log message</param>
    /// <param name="level">INFO, ERROR, ...</param>
    public static async void Write(string message, string level = "INFO")
    {
        var result = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}";
        
        try
        {
            await _fileLock.WaitAsync();

            await File.AppendAllTextAsync(LogFilePath, result + Environment.NewLine);
            await TrimLogFileIfNeededAsync();
            
            // для отладки — можно видеть лог в консоли
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG ERROR] {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }
    private static async Task TrimLogFileIfNeededAsync()
    {
        if (!File.Exists(LogFilePath)) return;

        var lines = await File.ReadAllLinesAsync(LogFilePath);

        if (lines.Length <= MAX_LOG_LENGTH) return;
        
        var trimmed = lines.Skip(lines.Length - MAX_LOG_LENGTH).ToArray();

        await File.WriteAllLinesAsync(LogFilePath, trimmed);
    }
}

public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
            return Brushes.White;

        if (text.Contains("[ERROR]"))
            return Brushes.Red;

        if (text.Contains("[WARN]"))
            return Brushes.Yellow;

        if (text.Contains("[INFO]"))
            return Brushes.LightGreen;

        if (text.Contains("[DEBUG]"))
            return Brushes.LightBlue;

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}