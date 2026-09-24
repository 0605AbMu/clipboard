using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MacDesktopApp.Models;

namespace MacDesktopApp.Services;

public static class StorageService
{
    private static readonly string AppDataDir = OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Clipboard")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboard");

    private static readonly string HistoryFilePath = Path.Combine(AppDataDir, "history.json");

    public record SavedItem(string Content, DateTime CopiedAt, bool IsPinned);

    public static List<ClipboardItem> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath))
            {
                return new List<ClipboardItem>();
            }

            var json = File.ReadAllText(HistoryFilePath);
            var savedItems = JsonSerializer.Deserialize<List<SavedItem>>(json);

            if (savedItems == null) return new List<ClipboardItem>();

            return savedItems.Select(s => new ClipboardItem
            {
                Content = s.Content,
                CopiedAt = s.CopiedAt,
                IsPinned = s.IsPinned
            }).ToList();
        }
        catch
        {
            return new List<ClipboardItem>();
        }
    }

    public static void SaveHistory(IEnumerable<ClipboardItem> items)
    {
        try
        {
            if (!Directory.Exists(AppDataDir))
            {
                Directory.CreateDirectory(AppDataDir);
            }

            var toSave = items.Take(80).Select(i => new SavedItem(i.Content, i.CopiedAt, i.IsPinned)).ToList();
            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryFilePath, json);
        }
        catch { }
    }
}
