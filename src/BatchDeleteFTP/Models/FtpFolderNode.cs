using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchDeleteFTP.Models;

public class FtpFolderNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = "/";
    public bool IsPlaceholder { get; set; }
    public bool ChildrenLoaded { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FtpFolderNode> Children { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
