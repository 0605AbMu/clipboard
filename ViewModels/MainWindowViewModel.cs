using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacDesktopApp.Models;
using MacDesktopApp.Services;

namespace MacDesktopApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxHistoryCount = 80;
    private string _lastCopiedText = string.Empty;
    private readonly DispatcherTimer _clipboardTimer;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ClipboardItem? _selectedItem;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _pinnedCount;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _needsAccessibility;

    public ObservableCollection<ClipboardItem> AllItems { get; } = new();
    public ObservableCollection<ClipboardItem> FilteredItems { get; } = new();

    public event Action? RequestHideWindow;

    public MainWindowViewModel()
    {
        CheckAccessibility();
        InitializeSampleData();

        _clipboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _clipboardTimer.Tick += OnCheckClipboard;
        _clipboardTimer.Start();

        UpdateCounts();
    }

    public void CheckAccessibility()
    {
        NeedsAccessibility = !PlatformService.Current.IsAccessibilityGranted();
    }

    private void InitializeSampleData()
    {
        var saved = StorageService.LoadHistory();
        if (saved != null && saved.Count > 0)
        {
            foreach (var item in saved)
            {
                AllItems.Add(item);
            }
            ApplyFilter();
        }

        var currentPb = PlatformService.Current.GetClipboardText();

        if (!string.IsNullOrWhiteSpace(currentPb))
        {
            _lastCopiedText = currentPb;
            if (!AllItems.Any(x => x.Content == currentPb))
            {
                AddNewItem(currentPb, false);
            }
        }
        else if (AllItems.Count == 0)
        {
            AddNewItem("Salom, bu cross-platform plain-text clipboard menejeri.", true);
            AddNewItem("Istalgan joyda matnni nusxalang (Ctrl+C / Cmd+C).", false);
            AddNewItem("dotnet run", false);
        }
    }

    private void OnCheckClipboard(object? sender, EventArgs e)
    {
        if (!PlatformService.Current.HasClipboardChanged())
        {
            return;
        }

        var text = PlatformService.Current.GetClipboardText();

        if (string.IsNullOrWhiteSpace(text)) return;
        if (text == _lastCopiedText) return;

        _lastCopiedText = text;
        AddNewItem(text, false);
    }

    public void AddNewItem(string rawText, bool isPinned = false)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return;

        var text = rawText.Replace("\0", string.Empty).Trim();
        if (string.IsNullOrEmpty(text)) return;

        var existing = AllItems.FirstOrDefault(x => x.Content == text);
        if (existing != null)
        {
            if (existing.IsPinned) isPinned = true;
            AllItems.Remove(existing);
        }

        var newItem = new ClipboardItem
        {
            Content = text,
            CopiedAt = DateTime.Now,
            IsPinned = isPinned
        };

        AllItems.Insert(0, newItem);

        while (AllItems.Count > MaxHistoryCount)
        {
            var unpinned = AllItems.LastOrDefault(x => !x.IsPinned);
            if (unpinned != null) AllItems.Remove(unpinned);
            else break;
        }

        ApplyFilter();
        UpdateCounts();
        StorageService.SaveHistory(AllItems);
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredItems.Clear();

        var query = SearchText?.Trim() ?? string.Empty;
        var items = string.IsNullOrEmpty(query)
            ? AllItems.AsEnumerable()
            : AllItems.Where(x => x.Content.Contains(query, StringComparison.OrdinalIgnoreCase));

        var sorted = items
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CopiedAt)
            .ToList();

        int index = 1;
        foreach (var item in sorted)
        {
            item.DisplayIndex = index++;
            FilteredItems.Add(item);
        }

        IsEmpty = FilteredItems.Count == 0;
        SelectedItem = FilteredItems.FirstOrDefault();
    }

    [RelayCommand]
    public void SelectAndCopy(ClipboardItem? item)
    {
        var target = item ?? SelectedItem;
        if (target == null) return;

        _lastCopiedText = target.Content;
        // 1. Set purely unformatted plain text to clipboard
        PlatformService.Current.SetClipboardText(target.Content);

        // 2. Hide window and return focus to active application
        RequestHideWindow?.Invoke();

        // 3. Auto paste
        if (PlatformService.Current.IsAccessibilityGranted())
        {
            PlatformService.Current.SimulatePaste();
        }
        else
        {
            PlatformService.Current.OpenAccessibilitySettings();
        }
    }

    [RelayCommand]
    public void OpenSettings()
    {
        PlatformService.Current.OpenAccessibilitySettings();
    }

    [RelayCommand]
    public void TogglePin(ClipboardItem? item)
    {
        if (item == null) return;

        item.IsPinned = !item.IsPinned;
        ApplyFilter();
        UpdateCounts();
        StorageService.SaveHistory(AllItems);
    }

    [RelayCommand]
    public void DeleteItem(ClipboardItem? item)
    {
        if (item == null) return;

        AllItems.Remove(item);
        ApplyFilter();
        UpdateCounts();
        StorageService.SaveHistory(AllItems);
    }

    [RelayCommand]
    public void ClearAll()
    {
        var toRemove = AllItems.Where(x => !x.IsPinned).ToList();
        foreach (var item in toRemove)
        {
            AllItems.Remove(item);
        }

        ApplyFilter();
        UpdateCounts();
        StorageService.SaveHistory(AllItems);
    }

    [RelayCommand]
    public void Hide()
    {
        RequestHideWindow?.Invoke();
    }

    [RelayCommand]
    public void ExitApp()
    {
        Environment.Exit(0);
    }

    private void UpdateCounts()
    {
        TotalCount = AllItems.Count;
        PinnedCount = AllItems.Count(x => x.IsPinned);
        IsEmpty = FilteredItems.Count == 0;
    }
}
