using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace PuppyPet
{
    /// <summary>极简配置：%APPDATA%\PuppyPet\config.ini，key=value 一行一项</summary>
    public static class Config
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PuppyPet");
        static readonly string FilePath = Path.Combine(Dir, "config.ini");
        static Dictionary<string, string> _items;

        static Dictionary<string, string> Items
        {
            get
            {
                if (_items != null) return _items;
                _items = new Dictionary<string, string>();
                try
                {
                    if (File.Exists(FilePath))
                    {
                        foreach (string line in File.ReadAllLines(FilePath))
                        {
                            int i = line.IndexOf('=');
                            if (i <= 0) continue;
                            _items[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                        }
                    }
                }
                catch { }
                return _items;
            }
        }

        public static int GetInt(string key, int def)
        {
            string v;
            int n;
            if (Items.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return def;
        }

        public static float GetFloat(string key, float def)
        {
            string v;
            float n;
            if (Items.TryGetValue(key, out v) &&
                float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out n) && n > 0.3f && n < 3f) return n;
            return def;
        }

        public static void SetInt(string key, int value)
        {
            Items[key] = value.ToString(CultureInfo.InvariantCulture);
            Save();
        }

        public static void SetFloat(string key, float value)
        {
            Items[key] = value.ToString("0.##", CultureInfo.InvariantCulture);
            Save();
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in Items) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }

    /// <summary>开机自启（写当前用户的 Run 项）</summary>
    public static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "PuppyPet";

        public static bool IsOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(Name) != null;
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(Name, "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else k.DeleteValue(Name, false);
                }
            }
            catch { }
        }
    }
}
