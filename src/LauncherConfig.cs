using System;
using System.IO;
using System.Text.Json;

namespace ElifootLauncher
{
    public class LauncherConfig
    {
        public int ResolutionWidth { get; set; } = 640;
        public int ResolutionHeight { get; set; } = 480;
        public bool Fullscreen { get; set; } = false;

        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElifootLauncher");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.json");
            }
        }

        public static LauncherConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<LauncherConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }
            return new LauncherConfig();
        }

        public void Save()
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, _opts));
        }
    }
}
