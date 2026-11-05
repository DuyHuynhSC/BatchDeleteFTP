using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using WorkManagement.Models;

namespace WorkManagement.Views;

public class ProfileSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public FtpSiteProfile Profile { get; set; } = new();

    public string Name => Profile.Name;
    public string HostPort => $"{Profile.Host}:{Profile.Port}";
    public string Username => string.IsNullOrWhiteSpace(Profile.Username) ? "(Chưa đặt)" : Profile.Username;
    public string RemotePaths => Profile.RemotePaths != null && Profile.RemotePaths.Count > 0 ? string.Join(";", Profile.RemotePaths) : "/";
    public string RetentionDaysText => $"+{Profile.RetentionDays} ngày";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SelectProfilesWindow : Window
{
    private readonly ObservableCollection<ProfileSelectionItem> _items = new();

    public List<string> SelectedProfileNames { get; private set; } = new();

    public SelectProfilesWindow(IEnumerable<FtpSiteProfile> allProfiles, IEnumerable<string>? previouslySelectedNames)
    {
        InitializeComponent();

        var selectedSet = new HashSet<string>(previouslySelectedNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var p in allProfiles)
        {
            var item = new ProfileSelectionItem
            {
                Profile = p,
                IsSelected = selectedSet.Contains(p.Name)
            };
            item.PropertyChanged += (_, _) => UpdateSelectionCount();
            _items.Add(item);
        }

        // Nếu trước đó chưa chọn gì và có items, chọn item đầu tiên
        if (_items.Count > 0 && !_items.Any(i => i.IsSelected))
        {
            _items[0].IsSelected = true;
        }

        GridProfiles.ItemsSource = _items;
        UpdateSelectionCount();
    }

    private void UpdateSelectionCount()
    {
        int selectedCount = _items.Count(i => i.IsSelected);
        TxtSelectionCount.Text = $"Đã chọn: {selectedCount} / {_items.Count} profile";
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.IsSelected = true;
        UpdateSelectionCount();
    }

    private void BtnUnselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items) item.IsSelected = false;
        UpdateSelectionCount();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn ít nhất 1 Profile để thực hiện quét.", "Chưa chọn Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedProfileNames = selected;
        DialogResult = true;
        Close();
    }
}
