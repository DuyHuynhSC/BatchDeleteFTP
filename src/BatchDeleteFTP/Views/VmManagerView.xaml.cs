using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Microsoft.Win32;

namespace BatchDeleteFTP.Views;

public partial class VmManagerView : UserControl
{
    private readonly VmManagerService _vmService;
    private readonly ExcelImportService _excelService;
    private readonly RemoteDesktopService _rdpService;
    private readonly List<VirtualMachineEntry> _allEntries = new();
    private readonly ObservableCollection<VirtualMachineEntry> _displayEntries = new();

    public VmManagerView()
    {
        InitializeComponent();
        _vmService = new VmManagerService();
        _excelService = new ExcelImportService();
        _rdpService = new RemoteDesktopService();

        GridVms.ItemsSource = _displayEntries;
        LoadData();
    }

    public void LoadData()
    {
        _allEntries.Clear();
        var loaded = _vmService.LoadAll();
        _allEntries.AddRange(loaded);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = TxtSearch?.Text?.Trim() ?? string.Empty;
        BtnClearSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

        _displayEntries.Clear();

        IEnumerable<VirtualMachineEntry> filtered = _allEntries;
        if (!string.IsNullOrEmpty(query))
        {
            filtered = _allEntries.Where(vm =>
                (vm.No != null && vm.No.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Ip != null && vm.Ip.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.MachineName != null && vm.MachineName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.DomainOrWorkgroup != null && vm.DomainOrWorkgroup.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Os != null && vm.Os.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Username != null && vm.Username.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Tns != null && vm.Tns.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.DatabaseName != null && vm.DatabaseName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.OracleVersion != null && vm.OracleVersion.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Tester != null && vm.Tester.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.Notes != null && vm.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (vm.ExpirationDate != null && vm.ExpirationDate.Contains(query, StringComparison.OrdinalIgnoreCase))
            );
        }

        foreach (var item in filtered)
        {
            _displayEntries.Add(item);
        }

        TxtSummary.Text = $"Tổng cộng: {_allEntries.Count} máy ảo (Đang hiển thị: {_displayEntries.Count})";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = string.Empty;
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void BtnAddVm_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        var dialog = new EditVmDialog { Owner = parentWindow };
        if (dialog.ShowDialog() == true)
        {
            _allEntries.Add(dialog.Entry);
            _vmService.SaveAll(_allEntries);
            ApplyFilter();
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
            _vmService.SaveAll(_allEntries);
            ApplyFilter();
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
                _vmService.SaveAll(_allEntries);
                ApplyFilter();
            }
        }
    }

    private async void BtnToolbarPlay_Click(object sender, RoutedEventArgs e)
    {
        var selected = GridVms.SelectedItem as VirtualMachineEntry ?? _displayEntries.FirstOrDefault();
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
            var imported = await _excelService.ReadExcelAsync(ofd.FileName);
            if (imported.Count == 0)
            {
                MessageBox.Show(parentWindow, "Không tìm thấy dữ liệu hợp lệ trong file Excel đã chọn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var previewDialog = new ExcelImportPreviewDialog(ofd.FileName, imported) { Owner = parentWindow };
            if (previewDialog.ShowDialog() == true)
            {
                var result = _excelService.MergeIntoList(_allEntries, imported, previewDialog.SelectedAction);
                _vmService.SaveAll(_allEntries);
                ApplyFilter();

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
