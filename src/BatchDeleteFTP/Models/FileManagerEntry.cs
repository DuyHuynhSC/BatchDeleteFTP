using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchDeleteFTP.Models;

public class FileManagerEntry : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsParentEntry { get; set; }
    public long Size { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Permissions { get; set; } = string.Empty;

    public string IconKind => IsParentEntry
        ? "FolderArrowUpOutline"
        : (IsDirectory ? "Folder" : GetFileIcon(Name));

    public string IconColor => IsParentEntry
        ? "#78909C"
        : (IsDirectory ? "#FFA000" : "#42A5F5");

    public string TypeLabel => IsParentEntry
        ? "Thư mục cha"
        : (IsDirectory ? "Thư mục" : GetExtensionLabel(Name));

    public string SizeFormatted => IsDirectory || IsParentEntry
        ? "<DIR>"
        : FormatFileSize(Size);

    public string ModifiedFormatted => IsParentEntry || ModifiedDate == DateTime.MinValue
        ? string.Empty
        : ModifiedDate.ToString("yyyy-MM-dd HH:mm:ss");

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes < 1024) return $"{bytes:N0} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB ({bytes:N0})";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetExtensionLabel(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName);
        return string.IsNullOrEmpty(ext) ? "File" : $"{ext.TrimStart('.').ToUpperInvariant()} File";
    }

    private static string GetFileIcon(string fileName)
    {
        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".log" or ".md" or ".csv" => "FileDocumentOutline",
            ".json" or ".xml" or ".config" or ".yml" or ".yaml" or ".ini" => "FileCodeOutline",
            ".cs" or ".js" or ".ts" or ".py" or ".sql" or ".html" or ".css" => "CodeBracesBox",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "FolderZipOutline",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" => "FileImageOutline",
            ".pdf" => "FilePdfBox",
            ".xls" or ".xlsx" => "FileExcelOutline",
            ".doc" or ".docx" => "FileWordOutline",
            ".exe" or ".dll" or ".bat" or ".ps1" or ".sh" => "ApplicationCogOutline",
            _ => "FileOutline"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
