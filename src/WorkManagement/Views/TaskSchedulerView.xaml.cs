using System;
using System.Windows;
using System.Windows.Controls;
using WorkManagement.Services;

namespace WorkManagement.Views;

public partial class TaskSchedulerView : UserControl
{
    private readonly TaskSchedulerService _schedulerService;

    public TaskSchedulerView()
    {
        InitializeComponent();
        _schedulerService = new TaskSchedulerService();
        UpdateCommandPreview();
    }

    private string GetSelectedMode()
    {
        return CboExecutionMode?.SelectedIndex == 1 ? "--auto" : "--preview";
    }

    private TaskSchedulerTriggerType GetSelectedTriggerType()
    {
        return RbTriggerLogon?.IsChecked == true
            ? TaskSchedulerTriggerType.AtLogonWithDelay
            : TaskSchedulerTriggerType.DailyTime;
    }

    private void TriggerType_Changed(object sender, RoutedEventArgs e)
    {
        bool isDaily = RbTriggerDaily?.IsChecked == true;
        if (TxtRunTime != null)
        {
            TxtRunTime.IsEnabled = isDaily;
        }

        if (TxtTaskName != null)
        {
            if (isDaily && TxtTaskName.Text == TaskSchedulerService.DefaultMorningTaskName)
            {
                TxtTaskName.Text = TaskSchedulerService.DefaultTaskName;
            }
            else if (!isDaily && TxtTaskName.Text == TaskSchedulerService.DefaultTaskName)
            {
                TxtTaskName.Text = TaskSchedulerService.DefaultMorningTaskName;
            }
        }

        UpdateCommandPreview();
    }

    private void TxtTaskName_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCommandPreview();
    }

    public void UpdateCommandPreview(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TxtCmdPreview == null) return;

        var triggerType = GetSelectedTriggerType();
        string defaultName = triggerType == TaskSchedulerTriggerType.AtLogonWithDelay
            ? TaskSchedulerService.DefaultMorningTaskName
            : TaskSchedulerService.DefaultTaskName;

        string taskName = string.IsNullOrWhiteSpace(TxtTaskName?.Text) ? defaultName : TxtTaskName.Text.Trim();
        string runTime = string.IsNullOrWhiteSpace(TxtRunTime?.Text) ? "23:00" : TxtRunTime.Text.Trim();
        string mode = GetSelectedMode();
        bool all = ChkAllProfiles?.IsChecked == true;
        bool email = ChkSendEmail?.IsChecked == true;

        TxtCmdPreview.Text = TaskSchedulerService.BuildCommandPreview(triggerType, taskName, runTime, mode, all, email, delayMinutes: 1);
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
            MessageBox.Show(Window.GetWindow(this), "Đã sao chép câu lệnh vào Clipboard!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"Không thể sao chép: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRegisterTask_Click(object sender, RoutedEventArgs e)
    {
        var triggerType = GetSelectedTriggerType();
        string defaultName = triggerType == TaskSchedulerTriggerType.AtLogonWithDelay
            ? TaskSchedulerService.DefaultMorningTaskName
            : TaskSchedulerService.DefaultTaskName;

        string taskName = string.IsNullOrWhiteSpace(TxtTaskName.Text) ? defaultName : TxtTaskName.Text.Trim();
        string runTime = string.IsNullOrWhiteSpace(TxtRunTime.Text) ? "23:00" : TxtRunTime.Text.Trim();
        string mode = GetSelectedMode();
        bool all = ChkAllProfiles.IsChecked == true;
        bool email = ChkSendEmail.IsChecked == true;

        BtnRegisterTask.IsEnabled = false;
        try
        {
            var (success, output) = await _schedulerService.RegisterTaskAsync(triggerType, taskName, runTime, mode, all, email, delayMinutes: 1);
            if (success)
            {
                string triggerDesc = triggerType == TaskSchedulerTriggerType.AtLogonWithDelay
                    ? "khi khởi động/đăng nhập máy (delay 1 phút)"
                    : $"hàng ngày lúc {runTime}";

                MessageBox.Show(Window.GetWindow(this), $"Đã tạo/cập nhật Task '{taskName}' ({triggerDesc}) thành công trong Windows Task Scheduler!\n\nChi tiết:\n{output}",
                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(Window.GetWindow(this), $"Không thể tạo Task.\n\nThông báo từ Windows: {output}\n\nLưu ý: Nếu cần quyền Quản trị (Admin), hãy mở ứng dụng dưới quyền 'Run as administrator' hoặc copy câu lệnh để chạy trong CMD/PowerShell (Admin).",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnRegisterTask.IsEnabled = true;
        }
    }

    private async void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
    {
        string taskName = string.IsNullOrWhiteSpace(TxtTaskName.Text) ? TaskSchedulerService.DefaultTaskName : TxtTaskName.Text.Trim();
        var confirm = MessageBox.Show(Window.GetWindow(this), $"Bạn có chắc chắn muốn xóa Task '{taskName}' khỏi Windows Task Scheduler?",
            "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnDeleteTask.IsEnabled = false;
        try
        {
            var (success, output) = await _schedulerService.DeleteTaskAsync(taskName);
            if (success)
            {
                MessageBox.Show(Window.GetWindow(this), $"Đã xóa task '{taskName}' khỏi hệ thống.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(Window.GetWindow(this), $"Không thể xóa task: {output}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnDeleteTask.IsEnabled = true;
        }
    }
}
