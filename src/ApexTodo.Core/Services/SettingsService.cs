using System.Text.Json;
using ApexTodo.Core.Data;
using ApexTodo.Core.Models;
using Dapper;

namespace ApexTodo.Core.Services;

public class SettingsService
{
    private readonly DatabaseContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(DatabaseContext db)
    {
        _db = db;
    }

    public AppSettings Load()
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        var rows = conn.Query("SELECT key, value FROM settings").ToList();

        if (rows.Count == 0)
            return new AppSettings();

        var dict = new Dictionary<string, string>();
        foreach (var row in rows)
            dict[(string)row.key] = (string)row.value;

        if (dict.TryGetValue("appSettings", out var json))
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        using var conn = _db.CreateConnection();
        conn.Open();
        conn.Execute("""
            INSERT OR REPLACE INTO settings (key, value)
            VALUES ('appSettings', @Value)
            """, new { Value = json });
    }
}
