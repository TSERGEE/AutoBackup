using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AutoBackup.Models
{
    public class BackupMeta
    {
        public DateTime BackupTime { get; set; }
        public string BackupType { get; set; } // "Full" or "Inc"
        public string FullBackupRef { get; set; } // для Inc – путь к полному, относительно которого сделан инкремент
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();

        public static void Save(string metaFilePath, BackupMeta meta)
        {
            File.WriteAllText(metaFilePath, JsonConvert.SerializeObject(meta, Formatting.Indented));
        }

        public static BackupMeta Load(string metaFilePath)
        {
            if (!File.Exists(metaFilePath)) return null;
            return JsonConvert.DeserializeObject<BackupMeta>(File.ReadAllText(metaFilePath));
        }
    }

    public class FileEntry
    {
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Hash { get; set; }
    }
}