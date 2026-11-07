using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace WorkManagement.Views;

public class CommandPaletteItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string CategoryBadge { get; set; } = string.Empty;
    public PackIconKind IconKind { get; set; } = PackIconKind.FlashOutline;
    public string SearchKeywords { get; set; } = string.Empty;
    public Action? ExecuteAction { get; set; }
}

public partial class CommandPaletteWindow : Window
{
    private readonly List<CommandPaletteItem> _allItems;
    private readonly ObservableCollection<CommandPaletteItem> _filteredItems = new();

    public CommandPaletteItem? SelectedCommand { get; private set; }

    public CommandPaletteWindow(IEnumerable<CommandPaletteItem> items)
    {
        InitializeComponent();
        _allItems = items.ToList();
        LstResults.ItemsSource = _filteredItems;

        ApplyFilter(string.Empty);
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            TxtQuery.Focus();
            Keyboard.Focus(TxtQuery);
        };
    }

    private void ApplyFilter(string query)
    {
        _filteredItems.Clear();
        string q = query.Trim();

        IEnumerable<CommandPaletteItem> matched;
        if (string.IsNullOrEmpty(q))
        {
            matched = _allItems.Take(50);
        }
        else
        {
            var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matched = _allItems.Where(item =>
                terms.All(t =>
                    item.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    item.Subtitle.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    item.CategoryBadge.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    item.SearchKeywords.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(50);
        }

        foreach (var item in matched)
        {
            _filteredItems.Add(item);
        }

        if (_filteredItems.Count > 0)
        {
            LstResults.SelectedIndex = 0;
        }

        TxtResultCount.Text = $"{_filteredItems.Count} kết quả";
    }

    private void TxtQuery_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(TxtQuery.Text);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Down)
        {
            if (_filteredItems.Count > 0)
            {
                int next = Math.Min(LstResults.SelectedIndex + 1, _filteredItems.Count - 1);
                LstResults.SelectedIndex = next;
                LstResults.ScrollIntoView(LstResults.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_filteredItems.Count > 0)
            {
                int prev = Math.Max(LstResults.SelectedIndex - 1, 0);
                LstResults.SelectedIndex = prev;
                LstResults.ScrollIntoView(LstResults.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ExecuteSelected();
        }
    }

    private void LstResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelected();
    }

    private void ExecuteSelected()
    {
        if (LstResults.SelectedItem is CommandPaletteItem item)
        {
            SelectedCommand = item;
            DialogResult = true;
            Close();
        }
    }
}
