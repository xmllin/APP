using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services.Apps
{
    public enum AppRepositoryState
    {
        NotStarted,
        Loading,
        Loaded,
        Failed
    }

    public sealed class AppRepository
    {
        private readonly string _filePath;
        private readonly string _longDescriptionsPath;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private IReadOnlyList<AppDefinition> _cache;
        private Exception _lastError;
        private AppRepositoryState _state = AppRepositoryState.NotStarted;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public AppRepository()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "apps.json");
            _longDescriptionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "app-long-descriptions.json");
        }

        public AppRepositoryState State => _state;
        public Exception LastError => _lastError;

        public async Task<IReadOnlyList<AppDefinition>> LoadAsync(CancellationToken token = default(CancellationToken), bool forceReload = false)
        {
            if (!forceReload && _state == AppRepositoryState.Loaded && _cache != null)
                return _cache;

            await _loadLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!forceReload && _state == AppRepositoryState.Loaded && _cache != null)
                    return _cache;

                _state = AppRepositoryState.Loading;
                _lastError = null;
                try
                {
                    var result = await LoadCoreAsync(token).ConfigureAwait(false);
                    _cache = result;
                    _state = AppRepositoryState.Loaded;
                    return _cache;
                }
                catch (Exception ex)
                {
                    _lastError = ex;
                    _state = AppRepositoryState.Failed;
                    throw;
                }
            }
            finally { _loadLock.Release(); }
        }

        public Task<IReadOnlyList<AppDefinition>> ReloadAsync(CancellationToken token = default(CancellationToken))
        {
            return LoadAsync(token, true);
        }

        private async Task<IReadOnlyList<AppDefinition>> LoadCoreAsync(CancellationToken token)
        {
            if (!File.Exists(_filePath)) throw new FileNotFoundException("Не найден каталог приложений.", _filePath);
            var json = await File.ReadAllTextAsync(_filePath, token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Файл каталога приложений пуст.");

            var result = JsonSerializer.Deserialize<List<AppDefinition>>(json, JsonOptions) ?? new List<AppDefinition>();
            if (File.Exists(_longDescriptionsPath))
            {
                var longJson = await File.ReadAllTextAsync(_longDescriptionsPath, token).ConfigureAwait(false);
                var descriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(longJson, JsonOptions);
                if (descriptions != null)
                {
                    foreach (var app in result)
                        if (app != null && descriptions.TryGetValue(app.Id ?? string.Empty, out var description))
                            app.LongDescription = description;
                }
            }
            return result;
        }
    }
}
