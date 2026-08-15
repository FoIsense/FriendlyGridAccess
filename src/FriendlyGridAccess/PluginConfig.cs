using Newtonsoft.Json;
using System.IO;

namespace FriendlyGridAccess
{
    public sealed class PluginConfig
    {
        public int MinimumReputation { get; set; } = 1500;
        public double CommandGridSearchRadiusMeters { get; set; } = 25.0;
        public bool RequireFactionLeaderOrFounder { get; set; } = true;

        public static PluginConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var cfg = new PluginConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(cfg, Formatting.Indented));
                return cfg;
            }

            return JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(path)) ?? new PluginConfig();
        }
    }
}
