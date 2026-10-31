using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace BatchDeleteFTP.Views;

public partial class PromptDialogWindow : Window
{
    public string InputValue { get; private set; } = string.Empty;

    public PromptDialogWindow(string title, string message, string defaultVal = "", PackIconKind icon = PackIconKind.FolderPlusOutline)
    {
        InitializeComponent();
        Title = title;
        TxtTitle.Text = title;
        TxtPromptMessage.Text = message;
        TxtInput.Text = defaultVal;
        IconPrompt.Kind = icon;

        Loaded += (_, _) =>
        {
            TxtInput.Focus();
            TxtInput.SelectAll();
        };
    }

    public static string? Show(Window owner, string title, string message, string defaultVal = "", PackIconKind icon = PackIconKind.FolderPlusOutline)
    {
        var dlg = new PromptDialogWindow(title, message, defaultVal, icon)
        {
            Owner = owner
        };
        return dlg.ShowDialog() == true ? dlg.InputValue : null;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string val = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(val))
        {
            MessageBox.Show(this, "Vui lòng nhập giá trị hợp lệ.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        InputValue = val;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnOk_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            BtnCancel_Click(sender, e);
        }
    }
}
