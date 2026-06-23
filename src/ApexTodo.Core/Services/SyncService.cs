using System.Net;
using System.Text;
using System.Xml.Linq;
using ApexTodo.Core.Models;

namespace ApexTodo.Core.Services;

public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class SyncService
{
    private readonly string _dbPath;
    private readonly Func<AppSettings> _getSettings;
    private Timer? _timer;
    private bool _syncing;

    public event Action<SyncResult>? OnSyncCompleted;

    public SyncService(string dbPath, Func<AppSettings> getSettings)
    {
        _dbPath = dbPath;
        _getSettings = getSettings;
    }

    public void StartAutoSync(int intervalMinutes)
    {
        StopAutoSync();
        if (intervalMinutes <= 0) return;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        _timer = new Timer(async _ => await SyncAsync(), null, interval, interval);
    }

    public void StopAutoSync()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public async Task<SyncResult> SyncAsync()
    {
        if (_syncing)
            return new SyncResult { Success = false, Message = "同步进行中" };

        _syncing = true;
        try
        {
            var settings = _getSettings();
            if (!settings.WebDav.Enabled || string.IsNullOrWhiteSpace(settings.WebDav.Url))
                return new SyncResult { Success = false, Message = "WebDAV 未配置" };

            var result = await DoSyncAsync(settings);
            OnSyncCompleted?.Invoke(result);
            return result;
        }
        catch (Exception ex)
        {
            var fail = new SyncResult { Success = false, Message = ex.Message };
            OnSyncCompleted?.Invoke(fail);
            return fail;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async Task<SyncResult> DoSyncAsync(AppSettings settings)
    {
        var wd = settings.WebDav;
        var baseUrl = wd.Url.TrimEnd('/');
        var remotePath = wd.RemotePath;
        var remoteUrl = $"{baseUrl}{remotePath}";

        using var http = CreateHttpClient(wd);

        // PROPFIND to check remote file
        var remoteExists = false;
        DateTime remoteMtime = DateTime.MinValue;
        try
        {
            remoteMtime = await GetRemoteMtimeAsync(http, remoteUrl);
            remoteExists = remoteMtime != DateTime.MinValue;
        }
        catch
        {
            // Remote file doesn't exist
        }

        var localExists = File.Exists(_dbPath);
        var localMtime = localExists ? File.GetLastWriteTimeUtc(_dbPath) : DateTime.MinValue;

        if (!localExists && !remoteExists)
            return new SyncResult { Success = true, Message = "无文件需要同步" };

        if (remoteExists && (!localExists || remoteMtime > localMtime.AddSeconds(1)))
        {
            // Download: remote is newer
            var response = await http.GetAsync(remoteUrl);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(_dbPath);
            await stream.CopyToAsync(fileStream);
            return new SyncResult { Success = true, Message = "已从远程下载" };
        }

        if (localExists && (!remoteExists || localMtime > remoteMtime.AddSeconds(1)))
        {
            // Upload: local is newer
            using var fileStream = File.OpenRead(_dbPath);
            var content = new StreamContent(fileStream);
            var response = await http.PutAsync(remoteUrl, content);
            response.EnsureSuccessStatusCode();
            return new SyncResult { Success = true, Message = "已上传到远程" };
        }

        return new SyncResult { Success = true, Message = "已是最新" };
    }

    private static HttpClient CreateHttpClient(WebDavConfig wd)
    {
        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(wd.Username))
        {
            handler.Credentials = new NetworkCredential(wd.Username, wd.Password);
        }

        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.Authorization = null;
        return http;
    }

    private static async Task<DateTime> GetRemoteMtimeAsync(HttpClient http, string remoteUrl)
    {
        // PROPFIND request to get modification date
        var propfindBody = """
            <?xml version="1.0" encoding="utf-8"?>
            <D:propfind xmlns:D="DAV:">
              <D:prop>
                <D:getlastmodified/>
              </D:prop>
            </D:propfind>
            """;

        var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), remoteUrl)
        {
            Content = new StringContent(propfindBody, Encoding.UTF8, "application/xml")
        };
        request.Headers.Add("Depth", "0");

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return DateTime.MinValue;

        var body = await response.Content.ReadAsStringAsync();

        // Parse PROPFIND response XML
        var doc = XDocument.Parse(body);
        var ns = XNamespace.Get("DAV:");
        var lastModified = doc.Descendants(ns + "getlastmodified").FirstOrDefault()?.Value;

        if (string.IsNullOrEmpty(lastModified))
            return DateTime.MinValue;

        if (DateTime.TryParse(lastModified, out var mtime))
            return mtime.ToUniversalTime();

        return DateTime.MinValue;
    }
}
