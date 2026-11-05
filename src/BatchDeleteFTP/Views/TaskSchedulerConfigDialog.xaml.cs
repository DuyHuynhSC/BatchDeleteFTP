using System;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP.Views;

public partial class TaskSchedulerConfigDialog : Window
{
    private readonly TaskSchedulerService _schedulerService;

    public TaskSchedulerConfigDialog()
    {
        InitializeComponent();
        _schedulerService = new TaskSchedulerService();
        UpdateCommandPreview();
    }

    private string GetSelectedMode()
    {
        return CboExecutionMode?.SelectedIndex == 1 ? "--preview" : "--auto";
    }

    private void UpdateCommandPreview(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TxtCmdPreview == null) return;

        string taskName = string.IsNullOrWhiteSpace(TxtTaskName?.Text) ? TaskSchedulerService.DefaultTaskName : TxtTaskName.Text.Trim();
        string runTime = string.IsNullOrWhiteSpace(TxtRunTime?.Text) ? "23:00" : TxtRunTime.Text.Trim();
        string mode = GetSelectedMode();
        bool all = ChkAllProfiles?.IsChecked == true;
        bool email = ChkSendEmail?.IsChecked == true;

        TxtCmdPreview.Text = TaskSchedulerService.BuildSchtasksCreateCommand(taskName, runTime, mode, all, email);
    }

    private void TxtRunTime_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCommandPreview();
    }

    private void CboExecutionMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCommandPreview();
    }

    private void BtnCopyCmd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtCmdPreview.Text);
            MessageBox.Show(this, "Đã sao chép câu lệnh vào Clipboard!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Không thể sao chép: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRegisterTask_Click(object sender, RoutedEventArgs e)
    {
        string taskName = string.IsNullOrWhiteSpace(TxtTaskName.Text) ? TaskSchedulerService.DefaultTaskName : TxtTaskName.Text.Trim();
        string runTime = string.IsNullOrWhiteSpace(TxtRunTime.Text) ? "23:00" : TxtRunTime.Text.Trim();
        string mode = GetSelectedMode();
        bool all = ChkAllProfiles.IsChecked == true;
        bool email = ChkSendEmail.IsChecked == true;

        BtnRegisterTask.IsEnabled = false;
        try
        {
            var (success, output) = await _schedulerService.RegisterDailyTaskAsync(taskName, runTime, mode, all, email);
            if (success)
            {
                MessageBox.Show(this, $"Đã tạo/cập nhật Task '{taskName}' thành công trong Windows Task Scheduler!\n\nChi tiết:\n{output}",
                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, $"Không thể tạo Task.\n\nThông báo từ Windows: {output}\n\nLưu ý: Nếu cần quyền Quản trị (Admin), hãy mở ứng dụng dưới quyền 'Run as administrator' hoặc copy câu lệnh để chạy trong CMD (Admin).",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnRegisterTask.IsEnabled = true;
        }
    }

    private async void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
    {
        string taskName = string.IsNullOrWhiteSpace(TxtTaskName.Text) ? TaskSchedulerService.DefaultTaskName : TxtTaskName.Text.Trim();
        var confirm = MessageBox.Show(this, $"Bạn có chắc chắn muốn xóa Task '{taskName}' khỏi Windows Task Scheduler?",
            "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnDeleteTask.IsEnabled = false;
        try
        {
            var (success, output) = await _schedulerService.DeleteTaskAsync(taskName);
            if (success)
            {
                MessageBox.Show(this, $"Đã xóa task '{taskName}' khỏi hệ thống.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, $"Không thể xóa task: {output}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnDeleteTask.IsEnabled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
