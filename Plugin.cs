using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using RemoteServerBridge.Configuration;

namespace RemoteServerBridge;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public override string Name => "Remote Server Bridge";
    public override Guid Id => Guid.Parse("096df3f4-4363-44f6-8208-410a0e5b854e");
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "RemoteServerBridge",
                EmbeddedResourcePath = "RemoteServerBridge.Configuration.configPage.html"
            }
        };
    }
}