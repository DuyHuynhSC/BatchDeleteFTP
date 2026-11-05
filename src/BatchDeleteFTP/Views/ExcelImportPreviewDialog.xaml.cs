using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP.Views;

public partial class ExcelImportPreviewDialog : Window
{
    private readonly List<ExcelSheetData> _sheets;
    public List<VirtualMachineEntry> ImportedList { get; private set; } = new();
    public ExcelImportConflictAction SelectedAction { get; private set; } = ExcelImportConflictAction.OverwriteExisting;

    public ExcelImportPreviewDialog(string filePath, List<ExcelSheetData> sheets)
    {
        InitializeComponent();
        _sheets = sheets ?? new List<ExcelSheetData>();

        TxtFilePathSubtitle.Text = $"File: {Path.GetFileName(filePath)} ({filePath})";

        CboSheets.ItemsSource = _sheets;
        if (_sheets.Count > 0)
        {
            CboSheets.SelectedIndex = 0;
            UpdatePreviewWithSheet(_sheets[0]);
        }
    }

    public ExcelImportPreviewDialog(string filePath, List<VirtualMachineEntry> entries)
        : this(filePath, new List<ExcelSheetData>
        {
            new ExcelSheetData
            {
                SheetName = "Mặc định",
                Entries = entries,
                IsRecommended = true
            }
        })
    {
    }

    private void CboSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboSheets.SelectedItem is ExcelSheetData selectedSheet)
        {
            UpdatePreviewWithSheet(selectedSheet);
        }
    }

    private void UpdatePreviewWithSheet(ExcelSheetData sheet)
    {
        ImportedList = sheet.Entries;
        TxtCountBadge.Text = $"{ImportedList.Count} máy";
        GridPreview.ItemsSource = null;
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
