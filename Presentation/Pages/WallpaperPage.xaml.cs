using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexora.Models;
using Nexora.Services;

namespace Nexora.Pages
{
    public partial class WallpaperPage : UserControl
    {
        private readonly MainWindow _main;
        private readonly WallpaperService _service = new WallpaperService();
        private readonly List<WallpaperItem> _all = new List<WallpaperItem>();
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _category = "Все";
        private CancellationTokenSource _loadCts;
        private CancellationTokenSource _thumbnailCts;
        private Task _loadTask;
        private readonly PaginationState<WallpaperItem> _pagination = new PaginationState<WallpaperItem>(18);
        private static readonly string FavoritesFile = UserDataPath.File("wallpaper_favorites.txt");

        public WallpaperPage(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            Loaded += WallpaperPage_Loaded;
            Unloaded += WallpaperPage_Unloaded;
            _loadCts = new CancellationTokenSource();
            _loadTask = LoadAsync(_loadCts.Token);
        }

        private async void WallpaperPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _loadTask;
            if (_loadTask.Status == TaskStatus.RanToCompletion)
            {
                await RefreshFavoritesAsync();
                ApplyFilter();
            }
        }

        private void WallpaperPage_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelThumbnailLoading();
            // Keep the metadata catalog in memory when switching tabs.
        }

        private async void RefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            if (_loadCts != null)
            {
                _loadCts.Cancel();
                _loadCts.Dispose();
            }
            CancelThumbnailLoading();
            _loadCts = new CancellationTokenSource();
            _loadTask = LoadAsync(_loadCts.Token);
            await _loadTask;
            if (_loadTask.Status == TaskStatus.RanToCompletion)
            {
                await RefreshFavoritesAsync();
                ApplyFilter();
            }
        }

        private async Task LoadAsync(CancellationToken token)
        {
            LoadingText.Text = "Загрузка библиотеки обоев…";
            try
            {
                var items = await _service.LoadAsync(token);
                token.ThrowIfCancellationRequested();

                _all.Clear();
                _all.AddRange(items.Select(WallpaperItem.FromWallpaper)
                    .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase));
                BuildCategories();
                ApplyFilter();
                LoadingText.Text = "";
                if (_service.UsedOfflineCatalog)
                    _main.ShowNotification("Не удалось обновить библиотеку. Используются сохранённые данные.", NotificationKind.Warning, "wallpapers-offline");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { LoadingText.Text = "Не удалось загрузить библиотеку."; _main.ShowNotification(_service.LastError ?? "Не удалось обновить библиотеку.", NotificationKind.Error, "wallpapers-load:" + ex.GetType().FullName); }
        }

        private async Task RefreshFavoritesAsync()
        {
            _favorites.Clear();
            foreach (var item in await LoadFavoritesAsync())
                _favorites.Add(item);
        }

        private static async Task<HashSet<string>> LoadFavoritesAsync()
        {
            try
            {
                if (!System.IO.File.Exists(FavoritesFile))
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var lines = await System.IO.File.ReadAllLinesAsync(FavoritesFile);
                return new HashSet<string>(lines.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void BuildCategories()
        {
            CategoryPanel.Children.Clear();
            AddCategory("Все");
            AddCategory("Избранное");
            foreach (var category in _all.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
                AddCategory(category);
            UpdateCategoryVisuals();
        }

        private void AddCategory(string category)
        {
            var b = new Button
            {
                Content = category,
                CommandParameter = category,
                Tag = string.Equals(category, _category, StringComparison.OrdinalIgnoreCase) ? "Selected" : "Normal",
                Style = (Style)FindResource("WallpaperCategoryButton"),
                Margin = new Thickness(0, 0, 8, 8)
            };
            b.Click += Category_Click;
            CategoryPanel.Children.Add(b);
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            var selectedCategory = button.CommandParameter as string;
            _category = string.IsNullOrWhiteSpace(selectedCategory) ? "Все" : selectedCategory;
            UpdateCategoryVisuals();
            ApplyFilter();
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private void UpdateCategoryVisuals()
        {
            foreach (var child in CategoryPanel.Children)
            {
                if (!(child is Button button)) continue;
                var selected = string.Equals(button.CommandParameter as string, _category, StringComparison.OrdinalIgnoreCase);
                button.Tag = selected ? "Selected" : "Normal";
                button.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(selected ? "#1468D8" : "#0F2035"));
                button.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(selected ? "#1468D8" : "#24415F"));
                button.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(selected ? "#FFFFFF" : "#D9E7F5"));
                button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void ApplyFilter()
        {
            List<WallpaperItem> result;
            if (_category == "Все")
                result = _all;
            else if (_category == "Избранное")
                result = _all.Where(x => _favorites.Contains(x.FullPath)).ToList();
            else
                result = _all.Where(x => x.Category == _category).ToList();

            _pagination.SetItems(result, true);
            var visible = _pagination.GetCurrentPageItems();
            WallpaperItems.ItemsSource = visible;
            CountText.Text = "Обои · " + result.Count;
            PageText.Text = _pagination.PageLabel;
            TopPageText.Text = PageText.Text;
            var hasPreviousPage = _pagination.HasPreviousPage;
            var hasNextPage = _pagination.HasNextPage;
            PreviousPageButton.Visibility = hasPreviousPage ? Visibility.Visible : Visibility.Hidden;
            TopPreviousPageButton.Visibility = hasPreviousPage ? Visibility.Visible : Visibility.Hidden;
            NextPageButton.Visibility = hasNextPage ? Visibility.Visible : Visibility.Hidden;
            TopNextPageButton.Visibility = hasNextPage ? Visibility.Visible : Visibility.Hidden;
            PreviousPageButton.IsEnabled = hasPreviousPage;
            NextPageButton.IsEnabled = hasNextPage;
            TopPreviousPageButton.IsEnabled = hasPreviousPage;
            TopNextPageButton.IsEnabled = hasNextPage;
            BottomPaginationPanel.Visibility = _pagination.HasMultiplePages ? Visibility.Visible : Visibility.Collapsed;

            _ = LoadVisibleThumbnailsAsync(visible);
        }

        private async Task LoadVisibleThumbnailsAsync(IReadOnlyList<WallpaperItem> visible)
        {
            CancelThumbnailLoading();
            _thumbnailCts = new CancellationTokenSource();
            var token = _thumbnailCts.Token;
            var semaphore = new SemaphoreSlim(4, 4);
            try
            {
                var tasks = visible.Where(x => x != null && x.Thumbnail == null).Select(async item =>
                {
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        var thumbnail = !string.IsNullOrWhiteSpace(item.ThumbnailUrl)
                            ? await WallpaperItem.CreateRemoteThumbnailAsync(item.ThumbnailUrl, 320, token).ConfigureAwait(false)
                            : null;
                        token.ThrowIfCancellationRequested();
                        if (thumbnail != null)
                        {
                            await Dispatcher.InvokeAsync(() => item.SetThumbnail(thumbnail));
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                semaphore.Dispose();
            }
        }

        private void CancelThumbnailLoading()
        {
            if (_thumbnailCts == null) return;
            try { _thumbnailCts.Cancel(); }
            catch { }
            _thumbnailCts.Dispose();
            _thumbnailCts = null;
        }

        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pagination.MovePrevious()) ApplyFilterKeepPage();
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pagination.MoveNext()) ApplyFilterKeepPage();
        }

        private void ApplyFilterKeepPage()
        {
            List<WallpaperItem> result;
            if (_category == "Все") result = _all;
            else if (_category == "Избранное") result = _all.Where(x => _favorites.Contains(x.FullPath)).ToList();
            else result = _all.Where(x => x.Category == _category).ToList();

            _pagination.SetItems(result, false);
            var visible = _pagination.GetCurrentPageItems();
            WallpaperItems.ItemsSource = visible;
            CountText.Text = "Обои · " + result.Count;
            PageText.Text = _pagination.PageLabel;
            TopPageText.Text = PageText.Text;
            var hasPreviousPage = _pagination.HasPreviousPage;
            var hasNextPage = _pagination.HasNextPage;
            PreviousPageButton.Visibility = hasPreviousPage ? Visibility.Visible : Visibility.Hidden;
            TopPreviousPageButton.Visibility = hasPreviousPage ? Visibility.Visible : Visibility.Hidden;
            NextPageButton.Visibility = hasNextPage ? Visibility.Visible : Visibility.Hidden;
            TopNextPageButton.Visibility = hasNextPage ? Visibility.Visible : Visibility.Hidden;
            PreviousPageButton.IsEnabled = hasPreviousPage;
            NextPageButton.IsEnabled = hasNextPage;
            TopPreviousPageButton.IsEnabled = hasPreviousPage;
            TopNextPageButton.IsEnabled = hasNextPage;
            BottomPaginationPanel.Visibility = _pagination.HasMultiplePages ? Visibility.Visible : Visibility.Collapsed;
            _ = LoadVisibleThumbnailsAsync(visible);
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var item = (WallpaperItem)((Button)sender).DataContext;
            foreach (var wallpaper in _all) wallpaper.IsSelected = ReferenceEquals(wallpaper, item);
            _main.SelectedWallpaper = item;
            _main.Navigate("wallpaperdetail");
        }

        public WallpaperItem GetAdjacentWallpaper(WallpaperItem current, int offset)
        {
            if (current == null) return null;

            IEnumerable<WallpaperItem> filtered = _all;
            if (_category == "Избранное")
                filtered = filtered.Where(x => _favorites.Contains(x.FullPath));
            else if (_category != "Все")
                filtered = filtered.Where(x => x.Category == _category);

            var items = filtered.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList();
            var index = items.FindIndex(x => string.Equals(x.FullPath, current.FullPath, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || items.Count == 0) return null;

            var nextIndex = index + offset;
            if (nextIndex < 0) nextIndex = items.Count - 1;
            if (nextIndex >= items.Count) nextIndex = 0;
            return items[nextIndex];
        }
    }
}
