using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services.Downloads
{
    public static class FileValidator
    {
        public static async Task ValidateAsync(string path, DownloadDefinition definition, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Файл загрузки не найден.");

            var info = new FileInfo(path);
            if (info.Length <= 0)
                throw new InvalidOperationException("Сервер не передал файл загрузки или файл пустой.");

            if (definition?.ExpectedSize.HasValue == true && info.Length != definition.ExpectedSize.Value)
                throw new InvalidOperationException("Размер загруженного файла не совпадает с ожидаемым.");

            await ValidateMagicBytesAsync(path, token);

            if (!string.IsNullOrWhiteSpace(definition?.Sha256))
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var hash = await sha.ComputeHashAsync(stream, token);
                    var actual = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                    if (!string.Equals(actual, definition.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Проверка SHA-256 загруженного файла не пройдена.");
                }
            }
        }

        public static bool IsHtmlPrefix(byte[] buffer, int count)
        {
            if (buffer == null || count <= 0) return false;
            var length = Math.Min(count, buffer.Length);
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, length).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return text.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<head", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<body", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<script", StringComparison.OrdinalIgnoreCase);
        }

        public static void ValidatePrefix(string fileName, byte[] buffer, int count)
        {
            var expected = GetExpectedSignature(fileName);
            if (expected == null || expected.Length == 0) return;
            if (buffer == null || count < expected.Length)
                throw new InvalidOperationException("Сервер вернул слишком короткий ответ вместо файла.");

            for (var i = 0; i < expected.Length; i++)
            {
                if (buffer[i] != expected[i])
                    throw new InvalidOperationException("Сервер вернул содержимое не того типа: вместо файла получены данные страницы/ошибки.");
            }
        }

        private static async Task ValidateMagicBytesAsync(string path, CancellationToken token)
        {
            var expected = GetExpectedSignature(path);
            if (expected == null || expected.Length == 0) return;

            var header = new byte[expected.Length];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, expected.Length, true))
            {
                var read = await stream.ReadAsync(header, 0, header.Length, token);
                if (read != expected.Length)
                    throw new InvalidOperationException("Загруженный файл повреждён или имеет неверный формат.");
            }

            ValidatePrefix(path, header, header.Length);
        }

        private static byte[] GetExpectedSignature(string pathOrFileName)
        {
            var raw = pathOrFileName ?? string.Empty;
            if (raw.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(0, raw.Length - ".part".Length);
            var extension = Path.GetExtension(raw).ToLowerInvariant();
            switch (extension)
            {
                case ".exe":
                case ".dll":
                    return new byte[] { 0x4D, 0x5A }; // MZ / PE
                case ".msi":
                    return new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
                case ".zip":
                case ".msix":
                case ".appx":
                    return new byte[] { 0x50, 0x4B }; // PK
                case ".7z":
                    return new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
                case ".rar":
                    return new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 };
                default:
                    return null;
            }
        }
    }
}
