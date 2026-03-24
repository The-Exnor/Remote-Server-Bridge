using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using RemoteServerBridge.Configuration;
using System.Text;

namespace RemoteServerBridge;

public class RemoteServerTask : IScheduledTask
{
    private readonly ILogger<RemoteServerTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly string _stubsPath;

    public RemoteServerTask(ILogger<RemoteServerTask> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _stubsPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "RemoteBridgeStubs");
    }

    public string Name => "Remote Server Bridge Sync";
    public string Key => "RemoteServerTask";
    public string Description => "Syncs media via Username/Password login (Android Client Mimic).";
    public string Category => "Library";

    private string NuclearClean(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        // Strip standard quotes, curly quotes, and surrounding whitespace
        return input.Replace("\"", "").Replace("'", "").Replace("“", "").Replace("”", "").Trim();
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null) return;

        // Clean inputs immediately
        string cleanUrl = NuclearClean(config.RemoteServerUrl).TrimEnd('/');
        string cleanUser = NuclearClean(config.Username);
        string cleanPass = NuclearClean(config.Password);

        if (string.IsNullOrEmpty(cleanUrl) || string.IsNullOrEmpty(cleanUser))
        {
            _logger.LogError("Remote Bridge: Configuration is incomplete.");
            return;
        }

        if (!Directory.Exists(_stubsPath)) Directory.CreateDirectory(_stubsPath);

        try
        {
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            using var client = new HttpClient(handler);

            // 1. Setup Headers to match the Jellyfin Android Client exactly
            string deviceId = "Bridge-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            string authHeader = $"MediaBrowser Client=\"Jellyfin Android\", Device=\"RemoteBridge\", DeviceId=\"{deviceId}\", Version=\"2.6.0\"";
            
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-Emby-Authorization", authHeader);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Android/2.6.0");

            // 2. Construct the Payload
            var authPayload = new { Username = cleanUser, Pw = cleanPass };
            var jsonPayload = JsonSerializer.Serialize(authPayload);
            
            _logger.LogInformation("Remote Bridge: Attempting login for [{User}] at {Url}", cleanUser, cleanUrl);

            // 3. Send Request
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            // Important: Explicitly set content type to avoid 'charset=utf-8'
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = await client.PostAsync($"{cleanUrl}/Users/AuthenticateByName", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Remote Bridge: Login Failed ({Status}). Server: {Error}", response.StatusCode, error);
                return;
            }

            // 4. Parse Success
            var authRoot = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            string token = authRoot.GetProperty("AccessToken").GetString()!;
            string userId = authRoot.GetProperty("User").GetProperty("Id").GetString()!;

            _logger.LogInformation("Remote Bridge: LOGIN SUCCESSFUL. Syncing library...");

            // 5. Fetch Items using the new Token
            client.DefaultRequestHeaders.Add("X-Emby-Token", token);
            string itemsUrl = $"{cleanUrl}/Users/{userId}/Items?IncludeItemTypes=Movie&Recursive=true&Fields=Path";
            
            var itemsRoot = await client.GetFromJsonAsync<JsonElement>(itemsUrl, cancellationToken);
            if (itemsRoot.TryGetProperty("Items", out var items))
            {
                int count = 0;
                foreach (var item in items.EnumerateArray())
                {
                    string name = item.GetProperty("Name").GetString()!;
                    string id = item.GetProperty("Id").GetString()!;
                    
                    string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                    string streamUrl = $"{cleanUrl}/Videos/{id}/stream.mp4?static=true";
                    string filePath = Path.Combine(_stubsPath, $"{safeName}.strm");

                    if (!File.Exists(filePath))
                    {
                        await File.WriteAllTextAsync(filePath, streamUrl, cancellationToken);
                        count++;
                    }
                }
                _logger.LogInformation("Remote Bridge: Sync finished. Added {Count} movies.", count);
            }

            EnsureVirtualFolder();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote Bridge: Error during sync.");
        }
    }

    private void EnsureVirtualFolder()
    {
        var collections = _libraryManager.GetVirtualFolders();
        if (!collections.Exists(c => c.Name == "Remote Server"))
        {
            var options = new LibraryOptions { PathInfos = new[] { new MediaPathInfo { Path = _stubsPath } } };
            // Using null for type allows auto-detection
            _libraryManager.AddVirtualFolder("Remote Server", null, options, true);
        }
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[] { new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(2).Ticks } };
    }
}