using MediaBrowser.Model.Plugins;

namespace RemoteServerBridge.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string RemoteServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}