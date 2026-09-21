using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services.Libraries
{
    public sealed class LibraryCatalogService
    {
        private readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "libraries.json");
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public async Task<IReadOnlyList<LibraryDefinition>> LoadAsync()
        {
            if (!File.Exists(_path)) return new List<LibraryDefinition>();
            var json = await File.ReadAllTextAsync(_path).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json)) return new List<LibraryDefinition>();
            return JsonSerializer.Deserialize<List<LibraryDefinition>>(json, Options) ?? new List<LibraryDefinition>();
        }
    }
}
