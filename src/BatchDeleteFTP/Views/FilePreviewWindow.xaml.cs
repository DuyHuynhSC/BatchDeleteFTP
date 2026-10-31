using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using BatchDeleteFTP.Models;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace BatchDeleteFTP.Views;

public partial class FilePreviewWindow : Window
{
    private readonly FileManagerEntry _entry;
    private readonly string _localFilePath;

    public FilePreviewWindow(FileManagerEntry entry, string localFilePath)
    {
        InitializeComponent();
        _entry = entry;
        _localFilePath = localFilePath;

        InitPreview();
    }

    private void InitPreview()
    {
        TxtFileName.Text = _entry.Name;
        TxtRemotePath.Text = _entry.FullPath;
        TxtFileSize.Text = FileManagerEntry.FormatFileSize(_entry.Size);
        TxtFileModified.Text = _entry.ModifiedFormatted;
        TxtFileType.Text = _entry.TypeLabel;

        if (Enum.TryParse<PackIconKind>(_entry.IconKind, out var iconKind))
        {
            IconFile.Kind = iconKind;
        }

        string ext = Path.GetExtension(_entry.Name).ToLowerInvariant();

        if (IsImageExtension(ext))
        {
            ShowImagePreview();
        }
        else if (IsLikelyTextExtension(ext) || _entry.Size < 512 * 1024)
        {
            ShowTextPreview();
        }
        else
        {
            ShowBinaryFallback();
        }
    }

    private static bool IsImageExtension(string ext) =>
        ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp";

    private static bool IsLikelyTextExtension(string ext) =>
        ext is ".txt" or ".log" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".css"
            or ".js" or ".ts" or ".cs" or ".sql" or ".md" or ".ini" or ".config" or ".yml"
            or ".yaml" or ".env" or ".bat" or ".cmd" or ".ps1" or ".sh" or ".py" or ".php"
            or ".java" or ".cpp" or ".c" or ".h" or ".svg";

    private void ShowImagePreview()
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(_localFilePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            ImgPreview.Source = bitmap;
            ImageContainer.Visibility = Visibility.Visible;
            TextContainer.Visibility = Visibility.Collapsed;
            BinaryContainer.Visibility = Visibility.Collapsed;
            ChkWarpText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ShowBinaryFallback();
        }
    }

    private void ShowTextPreview()
    {
        try
        {
            const int maxBytes = 2 * 1024 * 1024; // 2MB limit for smooth in-memory preview
            var fileInfo = new FileInfo(_localFilePath);
            if (!fileInfo.Exists)
            {
                TxtContent.Text = "[Lỗi: Không tìm thấy tập tin tạm]";
                return;
            }

            string content;
            if (fileInfo.Length > maxBytes)
            {
                using var fs = new FileStream(_localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[maxBytes];
                int read = fs.Read(buffer, 0, maxBytes);
                content = Encoding.UTF8.GetString(buffer, 0, read) +
                          $"\r\n\r\n--- [Đã đạt giới hạn xem trước 2 MB. Tổng dung lượng: {FileManagerEntry.FormatFileSize(fileInfo.Length)}] ---";
            }
            else
            {
                content = File.ReadAllText(_localFilePath, Encoding.UTF8);
            }

            TxtContent.Text = content;
            TextContainer.Visibility = Visibility.Visible;
            ImageContainer.Visibility = Visibility.Collapsed;
            BinaryContainer.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            TxtContent.Text = $"[Không thể đọc nội dung văn bản: {ex.Message}]";
            TextContainer.Visibility = Visibility.Visible;
            ImageContainer.Visibility = Visibility.Collapsed;
            BinaryContainer.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowBinaryFallback()
    {
        TextContainer.Visibility = Visibility.Collapsed;
        ImageContainer.Visibility = Visibility.Collapsed;
        BinaryContainer.Visibility = Visibility.Visible;
        ChkWarpText.Visibility = Visibility.Collapsed;
    }

    private void ChkWarpText_Changed(object sender, RoutedEventArgs e)
    {
        if (TxtContent == null) return;
        bool isWrap = ChkWarpText.IsChecked == true;
        TxtContent.TextWrapping = isWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        TxtContent.HorizontalScrollBarVisibility = isWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private void BtnOpenDefault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(_localFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _localFilePath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show(this, "Không tìm thấy file tạm để mở.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Không thể mở tập tin bằng ứng dụng mặc định: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string ext = Path.GetExtension(_entry.Name);
            var dlg = new SaveFileDialog
            {
                FileName = _entry.Name,
                Filter = string.IsNullOrEmpty(ext) ? "All Files (*.*)|*.*" : $"File (*{ext})|*{ext}|All Files (*.*)|*.*",
                Title = "Lưu bản sao tập tin"
            };

            if (dlg.ShowDialog(this) == true)
            {
                File.Copy(_localFilePath, dlg.FileName, overwrite: true);
                MessageBox.Show(this, $"Đã lưu tập tin thành công vào:\r\n{dlg.FileName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Lỗi khi lưu tập tin: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
