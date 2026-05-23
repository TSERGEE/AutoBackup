using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AutoBackup.Models
{
    public class BackupMeta
    {
        public DateTime BackupTime { get; set; }
        public string BackupType { get; set; } // "Full" or "Diff"
        public string FullBackupRef { get; set; } // для Diff – путь к полному, относительно которого сделан дифф
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
        public string RelativePath { get; set; } // путь от корня источника (например, "Documents\\file.txt")
        public long Size { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Hash { get; set; } // простой быстрый хеш (например, комбинация размера+даты, или SHA1 первых 1KB)
    }
}