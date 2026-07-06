using System;
using System.Collections.Generic;
using System.IO;

namespace ElifootLauncher
{
    // Formato: INI simples key=value. Zero dependencia externa (evita
    // problema do System.Text.Json em Framework Dependent netfx).
    public class LauncherConfig
    {
        public int ResolutionWidth { get; set; } = 640;
        public int ResolutionHeight { get; set; } = 480;
        public bool Fullscreen { get; set; } = false;

        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElifootLauncher");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.ini");
            }
        }

        public static LauncherConfig Load()
        {
            var cfg = new LauncherConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    switch (key.ToLowerInvariant())
                    {
                        case "width":
                            if (int.TryParse(val, out var w)) cfg.ResolutionWidth = w;
                            break;
                        case "height":
                            if (int.TryParse(val, out var h)) cfg.ResolutionHeight = h;
                            break;
                        case "fullscreen":
                            cfg.Fullscreen = val == "1" || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "; Elifoot 98 Launcher config",
                "width=" + ResolutionWidth,
                "height=" + ResolutionHeight,
                "fullscreen=" + (Fullscreen ? "1" : "0"),
            };
            File.WriteAllLines(ConfigPath, lines);
        }
    }
}
