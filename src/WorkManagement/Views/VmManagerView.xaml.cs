using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WorkManagement.Models;
using WorkManagement.Services;
using Microsoft.Win32;

namespace WorkManagement.Views;

public partial class VmManagerView : UserControl
{
    private readonly VmManagerService _vmService;
    private readonly ExcelImportService _excelService;
    private readonly RemoteDesktopService _rdpService;
    private readonly List<VirtualMachineEntry> _allEntries = new();
    private readonly ObservableCollection<VirtualMachineEntry> _displayEntries = new();
    private readonly ICollectionView _collectionView;
    private readonly DispatcherTimer _searchDebounceTimer;
    private bool _isUpdatingSelectAllHeader;
    private string _currentSearchQuery = string.Empty;

    public VmManagerView()
    {
        InitializeComponent();
        _vmService = new VmManagerService();
        _excelService = new ExcelImportService();
        _rdpService = new RemoteDesktopService();

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        };

        _collectionView = CollectionViewSource.GetDefaultView(_displayEntries);
        _collectionView.Filter = FilterVm;

        GridVms.ItemsSource = _collectionView;
        LoadData();
    }

    public void LoadData()
    {
        _allEntries.Clear();
        var loaded = _vmService.LoadAll();
        _allEntries.AddRange(loaded);

        _displayEntries.Clear();
        foreach (var item in _allEntries)
        {
            _displayEntries.Add(item);
        }

        ApplyFilter();
        _ = Dispatcher.BeginInvoke(new Action(AutoFitColumns), DispatcherPriority.Loaded);
    }

    private void ApplyFilter()
    {
        _currentSearchQuery = TxtSearch?.Text?.Trim() ?? string.Empty;
        if (BtnClearSearch != null)
        {
            BtnClearSearch.Visibility = string.IsNullOrEmpty(_currentSearchQuery) ? Visibility.Collapsed : Visibility.Visible;
        }

        _collectionView?.Refresh();
        UpdateSummary();
        UpdateSelectAllState();
    }

    private bool FilterVm(object obj)
    {
        if (obj is not VirtualMachineEntry vm) return false;
        if (string.IsNullOrWhiteSpace(_currentSearchQuery)) return true;

        var terms = _currentSearchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var term in terms)
        {
            bool matches =
                (vm.Ip != null && vm.Ip.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.MachineName != null && vm.MachineName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.DomainOrWorkgroup != null && vm.DomainOrWorkgroup.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Os != null && vm.Os.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Username != null && vm.Username.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Tns != null && vm.Tns.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.DatabaseName != null && vm.DatabaseName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.OracleVersion != null && vm.OracleVersion.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Tester != null && vm.Tester.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Notes != null && vm.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.ExpirationDate != null && vm.ExpirationDate.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (vm.No != null && vm.No.Contains(term, StringComparison.OrdinalIgnoreCase));

            if (!matches) return false;
        }

        return true;
    }

    private void UpdateSummary()
    {
        if (TxtSummary == null) return;
        int total = _allEntries.Count;
        int visible = _collectionView?.Cast<object>().Count() ?? _displayEntries.Count;
        TxtSummary.Text = $"Tổng cộng: {total} máy ảo (Đang hiển thị: {visible})";
    }

    public void AutoFitColumns()
    {
        if (GridVms == null || GridVms.Columns.Count == 0) return;

        for (int i = 0; i < GridVms.Columns.Count; i++)
        {
            var col = GridVms.Columns[i];
            string header = col.Header?.ToString() ?? string.Empty;

            if (i == 0 || col.Header is CheckBox)
            {
                col.Width = new DataGridLength(42);
            }
            else if (header == "Play")
            {
                col.Width = new DataGridLength(85);
            }
            else if (header == "Thao Tác")
            {
                col.Width = new DataGridLength(85);
            }
            else
            {
                col.Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
            }
        }
    }

    private void BtnAutoFitColumns_Click(object sender, RoutedEventArgs e)
    {
        AutoFitColumns();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = TxtSearch?.Text?.Trim() ?? string.Empty;
        if (BtnClearSearch != null)
        {
            BtnClearSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
        }

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _searchDebounceTimer.Stop();
            TxtSearch.Text = string.Empty;
            ApplyFilter();
            e.Handled = true;
        }
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        TxtSearch.Text = string.Empty;
        ApplyFilter();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    #region Selection & Multi-Delete Logic

    private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelectAllHeader || ChkSelectAll == null) return;

        var visibleItems = (_collectionView?.Cast<VirtualMachineEntry>() ?? _displayEntries).ToList();
        bool allSelected = visibleItems.Count > 0 && visibleItems.All(x => x.IsSelected);
        bool newState = !allSelected;

        SetAllSelected(newState);

        _isUpdatingSelectAllHeader = true;
        ChkSelectAll.IsChecked = newState;
        _isUpdatingSelectAllHeader = false;
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectAllState();
    }

    private void SetAllSelected(bool isSelected)
    {
        if (_collectionView == null) return;
        foreach (var obj in _collectionView)
        {
            if (obj is VirtualMachineEntry vm)
            {
                vm.IsSelected = isSelected;
            }
        }
    }

    private void UpdateSelectAllState()
    {
        if (ChkSelectAll == null || _collectionView == null) return;

        var visibleItems = _collectionView.Cast<VirtualMachineEntry>().ToList();
        if (visibleItems.Count == 0)
        {
            _isUpdatingSelectAllHeader = true;
            ChkSelectAll.IsChecked = false;
            _isUpdatingSelectAllHeader = false;
            return;
        }

        int selectedCount = visibleItems.Count(x => x.IsSelected);

        _isUpdatingSelectAllHeader = true;
        if (selectedCount == 0)
        {
            ChkSelectAll.IsChecked = false;
        }
        else if (selectedCount == visibleItems.Count)
        {
            ChkSelectAll.IsChecked = true;
        }
        else
        {
            ChkSelectAll.IsChecked = null; // Indeterminate state (dash)
        }
        _isUpdatingSelectAllHeader = false;
    }

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedVms = _allEntries.Where(x => x.IsSelected).ToList();
        if (selectedVms.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Vui lòng tích chọn ít nhất một máy ảo trong danh sách để xóa.",
                "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var parentWindow = Window.GetWindow(this);
        var confirm = MessageBox.Show(parentWindow,
            $"Bạn có chắc chắn muốn xóa {selectedVms.Count} máy ảo đã chọn không?",
            "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            var selectedIds = new HashSet<string>(selectedVms.Select(x => x.Id));
            _allEntries.RemoveAll(x => selectedIds.Contains(x.Id));

            for (int i = _displayEntries.Count - 1; i >= 0; i--)
            {
                if (selectedIds.Contains(_displayEntries[i].Id))
                {
                    _displayEntries.RemoveAt(i);
                }
            }

            _vmService.SaveAll(_allEntries);
            _collectionView?.Refresh();
            UpdateSummary();
            UpdateSelectAllState();

            MessageBox.Show(parentWindow,
                $"Đã xóa thành công {selectedVms.Count} máy ảo.",
                "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    private void BtnAddVm_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        var dialog = new EditVmDialog { Owner = parentWindow };
        if (dialog.ShowDialog() == true)
        {
            _allEntries.Add(dialog.Entry);
            _displayEntries.Add(dialog.Entry);
            _vmService.SaveAll(_allEntries);
            _collectionView?.Refresh();
            UpdateSummary();
            UpdateSelectAllState();
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry)
        {
            OpenEditDialog(entry);
        }
    }

    private void GridVms_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridVms.SelectedItem is VirtualMachineEntry entry)
        {
            OpenEditDialog(entry);
        }
    }

    private void OpenEditDialog(VirtualMachineEntry entry)
    {
        var parentWindow = Window.GetWindow(this);
        var dialog = new EditVmDialog(entry) { Owner = parentWindow };
        if (dialog.ShowDialog() == true)
        {
            int idx = _allEntries.FindIndex(x => x.Id == entry.Id);
            if (idx >= 0)
            {
                _allEntries[idx] = dialog.Entry;
            }

            int idxDisp = _displayEntries.IndexOf(entry);
            if (idxDisp >= 0)
            {
                _displayEntries[idxDisp] = dialog.Entry;
            }

            _vmService.SaveAll(_allEntries);
            _collectionView?.Refresh();
            UpdateSummary();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry)
        {
            var parentWindow = Window.GetWindow(this);
            var confirm = MessageBox.Show(parentWindow,
                $"Bạn có chắc chắn muốn xóa máy ảo {entry.MachineName} ({entry.Ip})?",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                _allEntries.RemoveAll(x => x.Id == entry.Id);
                _displayEntries.Remove(entry);
                _vmService.SaveAll(_allEntries);
                _collectionView?.Refresh();
                UpdateSummary();
                UpdateSelectAllState();
            }
        }
    }

    private void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
    {
        if (_allEntries.Count == 0)
        {
            MessageBox.Show(Window.GetWindow(this), "Danh sách máy ảo hiện đang trống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var parentWindow = Window.GetWindow(this);
        var confirm = MessageBox.Show(parentWindow,
            $"Bạn có chắc chắn muốn XÓA TOÀN BỘ {_allEntries.Count} máy ảo hiện tại không?\n\nThao tác này sẽ dọn sạch danh sách để bạn có thể import lại từ đầu.",
            "Xác nhận xóa tất cả", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            _allEntries.Clear();
            _displayEntries.Clear();
            _vmService.ClearAll();
            _collectionView?.Refresh();
            UpdateSummary();
            UpdateSelectAllState();
            MessageBox.Show(parentWindow, "Đã xóa toàn bộ danh sách máy ảo thành công.", "Hoàn tất", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnToolbarPlay_Click(object sender, RoutedEventArgs e)
    {
        var selected = GridVms.SelectedItem as VirtualMachineEntry ?? _collectionView?.Cast<VirtualMachineEntry>().FirstOrDefault();
        if (selected == null)
        {
            MessageBox.Show(Window.GetWindow(this), "Vui lòng chọn một máy ảo từ danh sách để kết nối Remote Desktop.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ConnectToVmAsync(selected);
    }

    private async void BtnPlayRdp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry)
        {
            await ConnectToVmAsync(entry);
        }
    }

    private async System.Threading.Tasks.Task ConnectToVmAsync(VirtualMachineEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Ip))
        {
            MessageBox.Show(Window.GetWindow(this), "Địa chỉ IP của máy ảo này đang bị trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success = await _rdpService.ConnectAsync(entry);
        if (!success)
        {
            MessageBox.Show(Window.GetWindow(this), $"Không thể mở Remote Desktop tới {entry.Ip}.", "Lỗi RDP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCopyIp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry && !string.IsNullOrEmpty(entry.Ip))
        {
            try
            {
                Clipboard.SetText(entry.Ip);
            }
            catch { }
        }
    }

    private void BtnCopyPass_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry && !string.IsNullOrEmpty(entry.Password))
        {
            try
            {
                Clipboard.SetText(entry.Password);
            }
            catch { }
        }
    }

    private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is VirtualMachineEntry entry)
        {
            entry.IsPasswordMasked = !entry.IsPasswordMasked;
        }
    }

    private async void BtnImportExcel_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        var ofd = new OpenFileDialog
        {
            Title = "Chọn file danh sách máy ảo Excel",
            Filter = "File Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog(parentWindow) != true) return;

        try
        {
            var sheets = await _excelService.ReadSheetsFromExcelAsync(ofd.FileName);
            if (sheets.Count == 0 || sheets.All(s => s.Entries.Count == 0))
            {
                MessageBox.Show(parentWindow, "Không tìm thấy dữ liệu hợp lệ trong file Excel đã chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var previewDialog = new ExcelImportPreviewDialog(ofd.FileName, sheets) { Owner = parentWindow };
            if (previewDialog.ShowDialog() == true && previewDialog.ImportedList.Count > 0)
            {
                var result = _excelService.MergeIntoList(_allEntries, previewDialog.ImportedList, previewDialog.SelectedAction);
                _displayEntries.Clear();
                foreach (var item in _allEntries)
                {
                    _displayEntries.Add(item);
                }

                _vmService.SaveAll(_allEntries);
                _collectionView?.Refresh();
                UpdateSummary();
                UpdateSelectAllState();
                _ = Dispatcher.BeginInvoke(new Action(AutoFitColumns), DispatcherPriority.Loaded);

                MessageBox.Show(parentWindow,
                    $"Import hoàn tất!\n- Tổng số dòng đọc được: {result.TotalRead}\n- Thêm mới: {result.AddedCount}\n- Cập nhật (ghi đè): {result.UpdatedCount}\n- Bỏ qua: {result.SkippedCount}",
                    "Kết quả Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(parentWindow, $"Lỗi khi đọc file Excel: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        var sfd = new SaveFileDialog
        {
            Title = "Lưu file mẫu Excel",
            Filter = "File Excel (*.xlsx)|*.xlsx",
            FileName = "DanhSachMayAo_Template.xlsx"
        };

        if (sfd.ShowDialog(parentWindow) != true) return;

        try
        {
            await _excelService.ExportTemplateAsync(sfd.FileName);
            MessageBox.Show(parentWindow, $"Đã xuất file mẫu thành công tại:\n{sfd.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(parentWindow, $"Không thể xuất file mẫu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
