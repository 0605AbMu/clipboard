using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacDesktopApp.Models;

namespace MacDesktopApp.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<StorageService.SavedItem>))]
internal partial class StorageJsonContext : JsonSerializerContext
{
}

public static class StorageService
{
    private static readonly string AppDataDir = OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Clipboard")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboard");

    private static readonly string HistoryFilePath = Path.Combine(AppDataDir, "history.json");
    private static readonly string BackupFilePath = Path.Combine(AppDataDir, "history.json.bak");
    private static readonly string TempFilePath = Path.Combine(AppDataDir, "history.json.tmp");

    public record SavedItem(string Content, DateTime CopiedAt, bool IsPinned);

    public static List<ClipboardItem> LoadHistory()
    {
        // 1. Try reading primary history file
        if (File.Exists(HistoryFilePath))
        {
            try
            {
                var json = File.ReadAllText(HistoryFilePath);
                var savedItems = JsonSerializer.Deserialize(json, StorageJsonContext.Default.ListSavedItem);
                if (savedItems != null && savedItems.Count > 0)
                {
                    return ConvertItems(savedItems);
                }
            }
            catch
            {
                // Corrupted or interrupted write - attempt recovery from backup
            }
        }

        // 2. Try reading resilient backup file
        if (File.Exists(BackupFilePath))
        {
            try
            {
                var json = File.ReadAllText(BackupFilePath);
                var savedItems = JsonSerializer.Deserialize(json, StorageJsonContext.Default.ListSavedItem);
                if (savedItems != null && savedItems.Count > 0)
                {
                    return ConvertItems(savedItems);
                }
            }
            catch { }
        }

        return new List<ClipboardItem>();
    }

    private static List<ClipboardItem> ConvertItems(List<SavedItem> saved)
    {
        return saved.Select(s => new ClipboardItem
        {
            Content = s.Content,
            CopiedAt = s.CopiedAt,
            IsPinned = s.IsPinned
        }).ToList();
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
            var json = JsonSerializer.Serialize(toSave, StorageJsonContext.Default.ListSavedItem);

            // 1. Write atomic temp file first
            File.WriteAllText(TempFilePath, json);

            // 2. Preserve existing valid file as backup
            if (File.Exists(HistoryFilePath))
            {
                try
                {
                    File.Copy(HistoryFilePath, BackupFilePath, overwrite: true);
                }
                catch { }
            }

            // 3. Atomically replace primary file
            File.Move(TempFilePath, HistoryFilePath, overwrite: true);
        }
        catch { }
    }
}

