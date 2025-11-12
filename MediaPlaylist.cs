using System.Collections.ObjectModel;
using System.ComponentModel;

namespace YTIN;

public class MediaPlaylist : INotifyPropertyChanged, IDownloadItem
{
    private string _id;
    private string _playlistTitle;
    private int _entries;
    
    public ObservableCollection<MediaInfo> MediaList { get; set; } = new();
    
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(nameof(Id)); }
    }
    public string PlaylistTitle
    {
        get => _playlistTitle;
        set { _playlistTitle = value; OnPropertyChanged(nameof(PlaylistTitle)); }
    }
    public int Entries
    {
        get => _entries;
        set { _entries = value; OnPropertyChanged(nameof(Entries)); }
    }
    
    
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}