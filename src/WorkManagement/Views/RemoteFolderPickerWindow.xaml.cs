using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorkManagement.Models;
using WorkManagement.Services;

namespace WorkManagement.Views;

public partial class RemoteFolderPickerWindow : Window
{
    private readonly FtpSettings _settings;
    private readonly FtpCleanupService _ftpService;
    private bool _isUpdatingSelection;

    public ObservableCollection<FtpFolderNode> RootNodes { get; } = new();
    public List<string> SelectedPaths { get; private set; } = new();

    public RemoteFolderPickerWindow(FtpSettings settings, FtpCleanupService ftpService, IEnumerable<string> currentPaths)
    {
        InitializeComponent();
        _settings = settings;
        _ftpService = ftpService;

        var preselected = new HashSet<string>(
            FtpCleanupService.NormalizeRemotePaths(currentPaths),
            StringComparer.OrdinalIgnoreCase);

        var rootNode = new FtpFolderNode
        {
            Name = "Thư mục gốc",
            FullPath = "/",
            IsSelected = preselected.Contains("/")
        };
        HookNodeEvents(rootNode);
        rootNode.Children.Add(new FtpFolderNode { Name = "Đang tải...", IsPlaceholder = true });

        RootNodes.Add(rootNode);
        FolderTreeView.ItemsSource = RootNodes;

        Loaded += async (_, _) =>
        {
            await LoadChildrenAsync(rootNode, preselected);
            rootNode.IsExpanded = true;
            UpdateSelectedSummary();
        };
    }

    private void HookNodeEvents(FtpFolderNode node)
    {
        node.PropertyChanged -= Node_PropertyChanged;
        node.PropertyChanged += Node_PropertyChanged;

        node.Children.CollectionChanged -= Children_CollectionChanged;
        node.Children.CollectionChanged += Children_CollectionChanged;

        foreach (var child in node.Children)
        {
            HookNodeEvents(child);
        }
    }

    private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (FtpFolderNode item in e.NewItems)
            {
                HookNodeEvents(item);
            }
        }
    }

    private async void FolderTreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is FtpFolderNode node)
        {
            await LoadChildrenAsync(node, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        UpdateSelectedSummary();
    }

    private void FolderRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is CheckBox) return;

        if (sender is FrameworkElement element && element.DataContext is FtpFolderNode node && !node.IsPlaceholder)
        {
            node.IsSelected = !node.IsSelected;
            e.Handled = true;
        }
    }

    private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && item.DataContext is FtpFolderNode node && !node.IsPlaceholder)
        {
            e.Handled = true;
            _isUpdatingSelection = true;
            try
            {
                foreach (var root in RootNodes)
                {
                    UncheckAll(root);
                }
                node.IsSelected = true;
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            BtnConfirm_Click(sender, e);
        }
    }

    private async System.Threading.Tasks.Task LoadChildrenAsync(FtpFolderNode parentNode, HashSet<string> preselected)
    {
        if (parentNode.ChildrenLoaded || parentNode.IsPlaceholder) return;

        try
        {
            StatusText.Text = $"Đang tải {parentNode.FullPath}...";
            var children = await _ftpService.GetSubDirectoriesAsync(_settings, parentNode.FullPath);
            parentNode.Children.Clear();
            foreach (var child in children)
            {
                if (preselected.Contains(child.FullPath))
                {
                    child.IsSelected = true;
                }
                parentNode.Children.Add(child);
            }
            parentNode.ChildrenLoaded = true;
            UpdateSelectedSummary();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Lỗi tải thư mục.";
            MessageBox.Show($"Không thể tải thư mục '{parentNode.FullPath}':\n{ex.Message}", "Lỗi FTP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingSelection || e.PropertyName != nameof(FtpFolderNode.IsSelected)) return;
        if (sender is not FtpFolderNode node) return;

        _isUpdatingSelection = true;
        try
        {
            if (node.FullPath != "/" && node.IsSelected)
            {
                // Khi tích chọn thư mục con, tự động bỏ chọn Thư mục gốc ("/")
                var root = RootNodes.FirstOrDefault(n => n.FullPath == "/");
                if (root != null)
                {
                    root.IsSelected = false;
                }
            }
            else if (node.FullPath == "/" && node.IsSelected)
            {
                // Khi tích chọn Thư mục gốc, tự động bỏ chọn tất cả thư mục con bên dưới
                UncheckAllChildren(node);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        UpdateSelectedSummary();
    }

    private static void UncheckAll(FtpFolderNode node)
    {
        if (node.IsPlaceholder) return;
        node.IsSelected = false;
        foreach (var child in node.Children)
        {
            UncheckAll(child);
        }
    }

    private static void UncheckAllChildren(FtpFolderNode parent)
    {
        foreach (var child in parent.Children)
        {
            if (!child.IsPlaceholder)
            {
                child.IsSelected = false;
                UncheckAllChildren(child);
            }
        }
    }

    private void UpdateSelectedSummary()
    {
        var collected = new List<string>();
        foreach (var node in RootNodes)
        {
            CollectSelectedNodes(node, collected);
        }

        if (collected.Count > 1 && collected.Contains("/"))
        {
            collected.Remove("/");
        }

        if (collected.Count == 0)
        {
            if (FolderTreeView.SelectedItem is FtpFolderNode highlighted && !highlighted.IsPlaceholder)
            {
                StatusText.Text = $"Đang trỏ: {highlighted.FullPath} (click để chọn)";
            }
            else
            {
                StatusText.Text = "Chưa chọn thư mục nào.";
            }
        }
        else
        {
            string pathsDisplay = string.Join(", ", collected.Take(2));
            if (collected.Count > 2) pathsDisplay += $", ... (+{collected.Count - 2})";
            StatusText.Text = $"Đã chọn ({collected.Count}): {pathsDisplay}";
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var collected = new List<string>();
        foreach (var node in RootNodes)
        {
            CollectSelectedNodes(node, collected);
        }

        // 1. Nếu có cả "/" và các thư mục con khác, ưu tiên thư mục con được chọn cụ thể
        if (collected.Count > 1 && collected.Contains("/"))
        {
            collected.Remove("/");
        }

        // 2. Nếu người dùng chưa tick checkbox nào hoặc chỉ có "/" từ trước,
        // nhưng đang click highlight vào một thư mục con cụ thể thì ưu tiên thư mục con đó
        if (collected.Count == 0 || (collected.Count == 1 && collected[0] == "/"))
        {
            if (FolderTreeView.SelectedItem is FtpFolderNode highlighted && !highlighted.IsPlaceholder && highlighted.FullPath != "/")
            {
                collected.Clear();
                collected.Add(highlighted.FullPath);
            }
        }

        if (collected.Count == 0)
        {
            MessageBox.Show("Vui lòng tích chọn hoặc nhấp chọn ít nhất 1 thư mục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedPaths = FtpCleanupService.NormalizeRemotePaths(collected).ToList();
        DialogResult = true;
        Close();
    }

    private static void CollectSelectedNodes(FtpFolderNode node, List<string> result)
    {
        if (node.IsPlaceholder) return;
        if (node.IsSelected)
        {
            result.Add(node.FullPath);
        }
        foreach (var child in node.Children)
        {
            CollectSelectedNodes(child, result);
        }
    }
}
