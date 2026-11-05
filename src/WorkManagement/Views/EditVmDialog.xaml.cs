using System;
using System.Windows;
using WorkManagement.Models;
using WorkManagement.Services;

namespace WorkManagement.Views;

public partial class EditVmDialog : Window
{
    public VirtualMachineEntry Entry { get; private set; }
    private readonly RemoteDesktopService _rdpService;

    public EditVmDialog(VirtualMachineEntry? existing = null)
    {
        InitializeComponent();
        _rdpService = new RemoteDesktopService();

        if (existing != null)
        {
            Entry = existing.Clone();
            TxtHeaderTitle.Text = $"Chỉnh Sửa Máy Ảo: {Entry.MachineName ?? Entry.Ip}";
            LoadFromEntry();
        }
        else
        {
            Entry = new VirtualMachineEntry();
            TxtHeaderTitle.Text = "Thêm Máy Ảo Mới";
            TxtExpirationDate.Text = "Vô hạn";
        }
    }

    private void LoadFromEntry()
    {
        TxtNo.Text = Entry.No;
        TxtIp.Text = Entry.Ip;
        TxtMachineName.Text = Entry.MachineName;
        TxtDomain.Text = Entry.DomainOrWorkgroup;
        TxtOs.Text = Entry.Os;
        TxtUsername.Text = Entry.Username;
        TxtPassword.Text = Entry.Password;
        TxtTns.Text = Entry.Tns;
        TxtDatabaseName.Text = Entry.DatabaseName;
        TxtOracleVersion.Text = Entry.OracleVersion;
        TxtTester.Text = Entry.Tester;
        TxtNotes.Text = Entry.Notes;
        TxtExpirationDate.Text = string.IsNullOrWhiteSpace(Entry.ExpirationDate) ? "Vô hạn" : Entry.ExpirationDate;
    }

    private void SaveToEntry()
    {
        Entry.No = TxtNo.Text.Trim();
        Entry.Ip = TxtIp.Text.Trim();
        Entry.MachineName = TxtMachineName.Text.Trim();
        Entry.DomainOrWorkgroup = TxtDomain.Text.Trim();
        Entry.Os = TxtOs.Text.Trim();
        Entry.Username = TxtUsername.Text.Trim();
        Entry.Password = TxtPassword.Text.Trim();
        Entry.Tns = TxtTns.Text.Trim();
        Entry.DatabaseName = TxtDatabaseName.Text.Trim();
        Entry.OracleVersion = TxtOracleVersion.Text.Trim();
        Entry.Tester = TxtTester.Text.Trim();
        Entry.Notes = TxtNotes.Text.Trim();
        Entry.ExpirationDate = string.IsNullOrWhiteSpace(TxtExpirationDate.Text) ? "Vô hạn" : TxtExpirationDate.Text.Trim();
    }

    private async void BtnTestRdp_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtIp.Text))
        {
            MessageBox.Show(this, "Vui lòng nhập địa chỉ IP trước khi thử mở Remote Desktop.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveToEntry();
        bool success = await _rdpService.ConnectAsync(Entry);
        if (!success)
        {
            MessageBox.Show(this, "Không thể mở Remote Desktop. Vui lòng kiểm tra lại địa chỉ IP và quyền hệ thống.", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtIp.Text))
        {
            MessageBox.Show(this, "Địa chỉ IP là bắt buộc.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtIp.Focus();
            return;
        }

        SaveToEntry();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
