using System;
using System.IO;

namespace WpfApp1.Infrastructure.FileSystem
{
    public sealed class FileBackupService
    {
        private readonly string _root;

        public FileBackupService(string root = null)
        {
            _root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfApp1", "Backups");
        }

        public string BackupOnce(string name, string sourcePath)
        {
            if (!File.Exists(sourcePath)) return null;
            Directory.CreateDirectory(_root);
            var target = Path.Combine(_root, Sanitize(name) + ".bak");
            if (!File.Exists(target)) File.Copy(sourcePath, target, false);
            return target;
        }

        public bool Restore(string name, string destinationPath)
        {
            var source = Path.Combine(_root, Sanitize(name) + ".bak");
            if (!File.Exists(source)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(source, destinationPath, true);
            return true;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Имя резервной копии не задано.", nameof(value));
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
        }
    }
}
