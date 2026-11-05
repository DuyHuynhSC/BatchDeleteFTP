using System.Collections.Generic;
using System.IO;
using System.Windows;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP.Views;

public partial class ExcelImportPreviewDialog : Window
{
    public List<VirtualMachineEntry> ImportedList { get; }
    public ExcelImportConflictAction SelectedAction { get; private set; } = ExcelImportConflictAction.OverwriteExisting;

    public ExcelImportPreviewDialog(string filePath, List<VirtualMachineEntry> entries)
    {
        InitializeComponent();
        ImportedList = entries;

        TxtFilePathSubtitle.Text = $"File: {Path.GetFileName(filePath)} ({filePath})";
        TxtCountBadge.Text = $"{entries.Count} máy";
        GridPreview.ItemsSource = ImportedList;
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (RbSkip.IsChecked == true)
        {
            SelectedAction = ExcelImportConflictAction.SkipExisting;
        }
        else if (RbAppend.IsChecked == true)
        {
            SelectedAction = ExcelImportConflictAction.AppendAll;
        }
        else
        {
            SelectedAction = ExcelImportConflictAction.OverwriteExisting;
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
