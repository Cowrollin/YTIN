using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace YTIN;

public class MediaInfo : INotifyPropertyChanged
{
    private string _id;
    private string _fileName;
    private string _extinsion;
    private string _format;
    private string _fps;
    private double _size;
    private string _compactSize;
    
    private string _title;
    private double _progress;
    private string _speed;
    private string _eta;
    
    private string _url;
    private string _savePath;
    private DateTime _saveDate;
    
    public ObservableCollection<MediaFormat> formats { get; set; } = new ();

    
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(nameof(Id)); }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
    }
    public string Extinsion
    {
        get => _extinsion;
        set { _extinsion = value; OnPropertyChanged(nameof(Extinsion)); }
    }
    public string? Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(nameof(Format)); }
    }
    public string Fps
    {
        get => _fps;
        set { _fps = value; OnPropertyChanged(nameof(Fps)); }
    }
    public double Size
    {
        get => _size;
        set { _size = value; OnPropertyChanged(nameof(Size)); }
    }
    
    public string CompactSize
    {
        get => _compactSize;
        set { _compactSize = value; OnPropertyChanged(nameof(CompactSize)); }
    }
    
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(nameof(Progress)); }
    }
    public string Speed
    {
        get => _speed;
        set { _speed = value; OnPropertyChanged(nameof(Speed)); }
    }
    public string Eta
    {
        get => _eta;
        set { _eta = value; OnPropertyChanged(nameof(Eta)); }
    }
    
    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(nameof(Url)); }
    }
    public string SavePath
    {
        get => _savePath;
        set { _savePath = value; OnPropertyChanged(nameof(SavePath)); }
    }

    public DateTime SaveDate
    {
        get => _saveDate;
        set { _saveDate = value; OnPropertyChanged(nameof(SaveDate)); }
    } 
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    
}