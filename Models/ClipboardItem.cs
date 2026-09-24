using System;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MacDesktopApp.Models;

public partial class ClipboardItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString();

    public string Content { get; init; } = string.Empty;

    public DateTime CopiedAt { get; init; } = DateTime.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinIcon))]
    [NotifyPropertyChangedFor(nameof(PinText))]
    private bool _isPinned;

    [ObservableProperty]
    private int _displayIndex;

    public string PinIcon => IsPinned ? "📌" : "•";

    public string PinText => IsPinned ? "Qadashni olish" : "Qadash";

    public string TimeDisplay => CopiedAt.ToString("HH:mm");

    public string SingleLineText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Content)) return "(Bo'sh)";
            // Flatten newlines and multiple spaces for compact single-line view
            var single = Regex.Replace(Content, @"\s+", " ").Trim();
            return single.Length > 120 ? single[..120] + "…" : single;
        }
    }
}
