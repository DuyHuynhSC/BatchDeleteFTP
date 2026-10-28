using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP.Views;

public partial class RemoteFolderPickerWindow : Window
{
    private readonly FtpSettings _settings;
    private readonly FtpCleanupService _ftpService;
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
        rootNode.Children.Add(new FtpFolderNode { Name = "Đang tải...", IsPlaceholder = true });

        RootNodes.Add(rootNode);
        FolderTreeView.ItemsSource = RootNodes;

        Loaded += async (_, _) =>
        {
            await LoadChildrenAsync(rootNode, preselected);
            rootNode.IsExpanded = true;
        };
    }

    private async void FolderTreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is FtpFolderNode node)
        {
            await LoadChildrenAsync(node, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
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
            StatusText.Text = "Đã tải xong.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Lỗi tải thư mục.";
            MessageBox.Show($"Không thể tải thư mục '{parentNode.FullPath}':\n{ex.Message}", "Lỗi FTP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var collected = new List<string>();
        foreach (var node in RootNodes)
        {
            CollectSelectedNodes(node, collected);
        }

        if (collected.Count == 0)
        {
            MessageBox.Show("Vui lòng tích chọn ít nhất 1 thư mục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
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
