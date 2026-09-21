using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using WpfApp1.Models;
using WpfApp1.Services;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WpfApp1.Pages
{
    public partial class WallpaperPreviewPage : UserControl
    {
        private readonly MainWindow m;
        private WallpaperItem wallpaper;
        private static readonly string FavoritesFile = UserDataPath.File("wallpaper_favorites.txt");

        private double _previewZoom = 1.0;
        private double _fullscreenZoom = 1.0;
        private Point _previewDragStart;
        private Point _previewStartOffset;
        private Point _fullscreenDragStart;
        private Point _fullscreenStartOffset;
        private bool _previewDragging;
        private bool _fullscreenDragging;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private static readonly ConcurrentDictionary<string, BitmapImage> PreviewCache = new ConcurrentDictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public void SetFullscreenScrollOffset(bool fullscreen)
        {
            if (PreviewScrollContainer != null)
                PreviewScrollContainer.Margin = fullscreen ? new Thickness(0, 0, 18, 0) : new Thickness(0);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        private const uint SPI_SETDESKWALLPAPER = 20;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        public WallpaperPreviewPage(MainWindow x)
        {
            InitializeComponent();
            m = x;
            wallpaper = m.SelectedWallpaper;
            Loaded += WallpaperPreviewPage_Loaded;
            Unloaded += WallpaperPreviewPage_Unloaded;
        }

        private void WallpaperPreviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (FullscreenOverlay != null && FullscreenOverlay.Visibility == Visibility.Visible)
            {
                FullscreenOverlay.Visibility = Visibility.Collapsed;
                m.ExitWallpaperFullscreen();
            }
        }

        private async void WallpaperPreviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WallpaperPreviewPage_Loaded;
            Focusable = true;
            Focus();

            if (wallpaper == null || (!wallpaper.IsRemote && !File.Exists(wallpaper.FullPath)))
            {
                StatusText.Text = "Изображение не найдено.";
                m.ShowNotification("Изображение обоев не найдено. Возможно, файл был перемещён или удалён.", NotificationKind.Warning, "wallpaper-missing:" + (wallpaper?.FullPath ?? string.Empty));
                SetButton.IsEnabled = false;
                FavoriteButton.IsEnabled = false;
                return;
            }

            try
            {
                await wallpaper.EnsureLocalAsync(System.Threading.CancellationToken.None);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось загрузить изображение.";
                m.ShowNotification("Не удалось загрузить обои «" + (wallpaper.Title ?? "") + ": " + WallpaperCacheService.DescribeHttpError(ex), NotificationKind.Error, "wallpaper-download:" + (wallpaper.FullPath ?? string.Empty));
                SetButton.IsEnabled = false;
                FavoriteButton.IsEnabled = false;
                return;
            }

            TitleText.Text = wallpaper.Title;
            SizeText.Text = wallpaper.SizeText + " • " + wallpaper.Category;
            DescriptionText.Text = wallpaper.Description;
            SetButton.Content = CreateActionContent("interface/white/brand-windows.svg", "Установить обои");
            SaveCopyButton.Content = CreateActionContent("interface/white/fluent-folder-open.svg", "Сохранить копию");
            UpdateFavoriteButton(LoadFavoritesSync());

            try
            {
                wallpaper.RefreshLocalInfo();
                PreviewImage.Source = await Task.Run(() => LoadPreview(wallpaper.FullPath));
                FullscreenImage.Source = PreviewImage.Source;
                FullscreenTitleText.Text = wallpaper.Title;
                ResetZoom();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось открыть изображение.";
                m.ShowNotification("Не удалось открыть обои «" + (wallpaper?.Title ?? "") + "»: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "wallpaper-open:" + (wallpaper?.FullPath ?? string.Empty));
            }
        }

        private static HashSet<string> LoadFavoritesSync()
        {
            try
            {
                if (!File.Exists(FavoritesFile))
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var lines = File.ReadAllLines(FavoritesFile);
                return new HashSet<string>(lines.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static BitmapImage LoadPreview(string path)
        {
            if (PreviewCache.TryGetValue(path, out var cached))
                return cached;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 1920;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewCache[path] = bitmap;
            return bitmap;
        }

        private async Task<HashSet<string>> LoadFavoritesAsync()
        {
            try
            {
                if (!File.Exists(FavoritesFile))
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var lines = await Task.Run(() => File.ReadAllLines(FavoritesFile));
                return new HashSet<string>(lines.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task SaveFavoritesAsync(HashSet<string> favorites)
        {
            string dir = Path.GetDirectoryName(FavoritesFile);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await Task.Run(() => File.WriteAllLines(FavoritesFile, favorites));
        }

        private void UpdateFavoriteButton(HashSet<string> favorites)
        {
            if (wallpaper == null) return;
            bool favorite = favorites.Contains(wallpaper.FullPath);
            FavoriteButton.Content = CreateActionContent("interface/white/heart.svg", favorite ? "Убрать из избранного" : "В избранное");
        }

        private static StackPanel CreateActionContent(string icon, string text)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, icon.Replace('/', Path.DirectorySeparatorChar));
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new Image { Source = SvgImageLoader.Load(path), Width = 17, Height = 17, Margin = new Thickness(0, 0, 8, 0) });
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        private async void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaper == null) return;
            FavoriteButton.IsEnabled = false;
            try
            {
                var favorites = await LoadFavoritesAsync();
                if (!favorites.Add(wallpaper.FullPath)) favorites.Remove(wallpaper.FullPath);
                await SaveFavoritesAsync(favorites);
                UpdateFavoriteButton(favorites);
                StatusText.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось сохранить избранное.";
                m.ShowNotification("Не удалось сохранить обои в избранное: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "wallpaper-favorite:" + (wallpaper?.FullPath ?? string.Empty));
            }
            finally { FavoriteButton.IsEnabled = true; }
        }

        private async void SetWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaper == null) return;
            try
            {
                await wallpaper.EnsureLocalAsync(System.Threading.CancellationToken.None);
                if (!File.Exists(wallpaper.FullPath)) return;
                bool ok = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaper.FullPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                ShowStatus(ok ? "Обои установлены на рабочий стол." : "Windows не удалось установить эти обои.");
            }
            catch (Exception ex) { ShowStatus("Не удалось установить обои."); m.ShowNotification("Не удалось установить обои: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "wallpaper-set:" + (wallpaper?.FullPath ?? string.Empty)); }
        }

        private void ShowStatus(string message)
        {
            _statusTimer?.Stop();
            StatusText.BeginAnimation(OpacityProperty, null);
            StatusText.Text = message;
            StatusText.Opacity = 1;
            _statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statusTimer.Tick += (sender, args) =>
            {
                _statusTimer.Stop();
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(450));
                fade.Completed += (_, __) => StatusText.Text = "";
                StatusText.BeginAnimation(OpacityProperty, fade);
            };
            _statusTimer.Start();
        }

        private void ClearStatus()
        {
            _statusTimer?.Stop();
            StatusText.BeginAnimation(OpacityProperty, null);
            StatusText.Text = "";
            StatusText.Opacity = 1;
        }

        private async void SaveCopy_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaper == null || !File.Exists(wallpaper.FullPath)) return;
            var dialog = new SaveFileDialog
            {
                FileName = wallpaper.FileName,
                Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Все файлы (*.*)|*.*",
                DefaultExt = Path.GetExtension(wallpaper.FullPath)
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                SetButton.IsEnabled = false;
                FavoriteButton.IsEnabled = false;
                StatusText.Text = "Сохранение копии…";
                await Task.Run(() => File.Copy(wallpaper.FullPath, dialog.FileName, true));
                StatusText.Text = "Копия сохранена.";
            }
            catch (Exception ex) { StatusText.Text = "Не удалось сохранить копию."; m.ShowNotification("Не удалось сохранить копию обоев: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "wallpaper-copy:" + (wallpaper?.FullPath ?? string.Empty)); }
            finally { SetButton.IsEnabled = true; FavoriteButton.IsEnabled = true; }
        }

        private void ResetZoom()
        {
            _previewZoom = 1.0;
            _fullscreenZoom = 1.0;
            SetTransform(PreviewScale, PreviewTranslate, 1.0, 0, 0);
            SetTransform(FullscreenScale, FullscreenTranslate, 1.0, 0, 0);
        }

        private static void SetTransform(System.Windows.Media.ScaleTransform scale, System.Windows.Media.TranslateTransform translate, double zoom, double x, double y)
        {
            if (scale == null || translate == null) return;
            scale.ScaleX = zoom;
            scale.ScaleY = zoom;
            translate.X = x;
            translate.Y = y;
        }

        private static double ChangeZoom(double value, int wheelDelta)
        {
            double step = wheelDelta > 0 ? 0.10 : -0.10;
            return Math.Max(1.0, Math.Min(5.0, Math.Round(value + step, 2)));
        }

        private void FullscreenOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            WallpaperPreviewPage_PreviewKeyDown(sender, e);
        }

        private void WallpaperPreviewPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                NavigateToAdjacentWallpaper(e.Key == Key.Left ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && FullscreenOverlay != null && FullscreenOverlay.Visibility == Visibility.Visible)
            {
                CloseFullscreen();
                e.Handled = true;
            }
        }

        private void PreviousWallpaper_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAdjacentWallpaper(-1);
        }

        private void NextWallpaper_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAdjacentWallpaper(1);
        }

        private async void NavigateToAdjacentWallpaper(int offset)
        {
            var next = m.GetWallpaperPage()?.GetAdjacentWallpaper(wallpaper, offset);
            if (next == null) return;

            m.SelectedWallpaper = next;
            wallpaper = next;
            await LoadCurrentWallpaperAsync();
        }

        private async Task LoadCurrentWallpaperAsync()
        {
            if (wallpaper == null) return;

            await wallpaper.EnsureLocalAsync(System.Threading.CancellationToken.None);
            if (!File.Exists(wallpaper.FullPath)) return;
            wallpaper.RefreshLocalInfo();

            ClearStatus();

            TitleText.Text = wallpaper.Title;
            SizeText.Text = wallpaper.SizeText + " • " + wallpaper.Category;
            DescriptionText.Text = wallpaper.Description;
            UpdateFavoriteButton(LoadFavoritesSync());

            try
            {
                var source = await Task.Run(() => LoadPreview(wallpaper.FullPath));
                PreviewImage.Source = source;
                FullscreenImage.Source = source;
                FullscreenTitleText.Text = wallpaper.Title;
                ResetZoom();
                if (FullscreenOverlay.Visibility == Visibility.Visible)
                    await Dispatcher.InvokeAsync(new Action(ApplyFullscreenBounds));
            }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось открыть изображение.";
                m.ShowNotification("Не удалось открыть обои «" + (wallpaper?.Title ?? "") + "»: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "wallpaper-open:" + (wallpaper?.FullPath ?? string.Empty));
            }
        }

        private void PreviewHost_MouseEnter(object sender, MouseEventArgs e)
        {
            SetPreviewControlsVisibility(true);
            AnimateHint(true);
        }

        private void PreviewHost_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_previewDragging && e.LeftButton != MouseButtonState.Pressed) EndPreviewDrag();
            AnimateHint(false);
            SetPreviewControlsVisibility(false);
        }

        private void SetPreviewControlsVisibility(bool visible)
        {
            var state = visible ? Visibility.Visible : Visibility.Collapsed;
            PreviousButton.Visibility = state;
            NextButton.Visibility = state;
        }

        private void FullscreenOverlay_MouseEnter(object sender, MouseEventArgs e)
        {
            SetFullscreenControlsVisibility(true);
        }

        private void SetFullscreenControlsVisibility(bool visible)
        {
            var state = visible ? Visibility.Visible : Visibility.Collapsed;
            FullscreenPreviousButton.Visibility = state;
            FullscreenNextButton.Visibility = state;
            FullscreenInfoPanel.Visibility = state;
            CloseFullscreenButton.Visibility = state;
        }

        private void AnimateHint(bool show)
        {
            if (HintPanel == null || HintPanelTranslate == null) return;

            var duration = new Duration(TimeSpan.FromMilliseconds(180));
            HintPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, duration));
            HintPanelTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(show ? 0 : 18, duration));
        }

        private void PreviewImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _previewZoom = ChangeZoom(_previewZoom, e.Delta);
            ApplyPreviewBounds();
            e.Handled = true;
        }

        private void FullscreenOverlay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _fullscreenZoom = ChangeZoom(_fullscreenZoom, e.Delta);
            if (_fullscreenZoom <= 1.0)
            {
                _fullscreenZoom = 1.0;
                SetTransform(FullscreenScale, FullscreenTranslate, 1.0, 0, 0);
            }
            else
            {
                ApplyFullscreenBounds();
            }
            e.Handled = true;
        }

        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_previewZoom <= 1.0 || !(e.OriginalSource is System.Windows.Controls.Image)) return;
            _previewDragging = true;
            _previewDragStart = e.GetPosition(PreviewHost);
            _previewStartOffset = new Point(PreviewTranslate.X, PreviewTranslate.Y);
            PreviewHost.CaptureMouse();
            PreviewHost.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void PreviewImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_previewDragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point p = e.GetPosition(PreviewHost);
            PreviewTranslate.X = _previewStartOffset.X + p.X - _previewDragStart.X;
            PreviewTranslate.Y = _previewStartOffset.Y + p.Y - _previewDragStart.Y;
            ApplyPreviewBounds();
        }

        private void PreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndPreviewDrag();
            e.Handled = true;
        }

        private void PreviewImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_previewDragging && e.LeftButton != MouseButtonState.Pressed) EndPreviewDrag();
        }

        private void EndPreviewDrag()
        {
            _previewDragging = false;
            if (PreviewHost.IsMouseCaptured) PreviewHost.ReleaseMouseCapture();
            PreviewHost.Cursor = Cursors.Arrow;
            ApplyPreviewBounds();
        }

        private bool IsInsideFullscreenButton(MouseEventArgs e)
        {
            if (CloseFullscreenButton == null || CloseFullscreenButton.Visibility != Visibility.Visible || !CloseFullscreenButton.IsHitTestVisible) return false;
            Point p = e.GetPosition(CloseFullscreenButton);
            return p.X >= 0 && p.Y >= 0 && p.X <= CloseFullscreenButton.ActualWidth && p.Y <= CloseFullscreenButton.ActualHeight;
        }

        private void FullscreenOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInsideFullscreenButton(e))
                SetFullscreenControlsVisibility(false);

            if (_fullscreenZoom <= 1.0 || IsInsideFullscreenButton(e)) return;
            _fullscreenDragging = true;
            _fullscreenDragStart = e.GetPosition(FullscreenOverlay);
            _fullscreenStartOffset = new Point(FullscreenTranslate.X, FullscreenTranslate.Y);
            FullscreenOverlay.CaptureMouse();
            FullscreenOverlay.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void FullscreenOverlay_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_fullscreenDragging)
                SetFullscreenControlsVisibility(true);

            if (!_fullscreenDragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point p = e.GetPosition(FullscreenOverlay);
            FullscreenTranslate.X = _fullscreenStartOffset.X + p.X - _fullscreenDragStart.X;
            FullscreenTranslate.Y = _fullscreenStartOffset.Y + p.Y - _fullscreenDragStart.Y;
            ApplyFullscreenBounds();
        }

        private void FullscreenOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_fullscreenDragging) EndFullscreenDrag();
            e.Handled = true;
        }

        private void FullscreenOverlay_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_fullscreenDragging && e.LeftButton != MouseButtonState.Pressed) EndFullscreenDrag();
            SetFullscreenControlsVisibility(false);
        }

        private void EndFullscreenDrag()
        {
            _fullscreenDragging = false;
            if (FullscreenOverlay.IsMouseCaptured) FullscreenOverlay.ReleaseMouseCapture();
            FullscreenOverlay.Cursor = Cursors.Arrow;
            ApplyFullscreenBounds();
        }

        private void ApplyPreviewBounds()
        {
            if (PreviewImage.Source == null || PreviewHost.ActualWidth <= 0 || PreviewHost.ActualHeight <= 0) return;
            Point offset = ClampOffset(PreviewHost.ActualWidth, PreviewHost.ActualHeight, ((BitmapSource)PreviewImage.Source).PixelWidth, ((BitmapSource)PreviewImage.Source).PixelHeight, _previewZoom, PreviewTranslate.X, PreviewTranslate.Y, false);
            PreviewTranslate.X = offset.X;
            PreviewTranslate.Y = offset.Y;
            PreviewScale.ScaleX = _previewZoom;
            PreviewScale.ScaleY = _previewZoom;
        }

        private void ApplyFullscreenBounds()
        {
            if (FullscreenImage.Source == null || FullscreenOverlay.ActualWidth <= 0 || FullscreenOverlay.ActualHeight <= 0) return;
            Point offset = ClampOffset(FullscreenOverlay.ActualWidth, FullscreenOverlay.ActualHeight, ((BitmapSource)FullscreenImage.Source).PixelWidth, ((BitmapSource)FullscreenImage.Source).PixelHeight, _fullscreenZoom, FullscreenTranslate.X, FullscreenTranslate.Y, true);
            FullscreenTranslate.X = offset.X;
            FullscreenTranslate.Y = offset.Y;
            FullscreenScale.ScaleX = _fullscreenZoom;
            FullscreenScale.ScaleY = _fullscreenZoom;
        }

        private static Point ClampOffset(double viewportWidth, double viewportHeight, int pixelWidth, int pixelHeight, double zoom, double x, double y, bool fillViewport)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0) return new Point(0, 0);
            double imageRatio = (double)pixelWidth / pixelHeight;
            double viewportRatio = viewportWidth / viewportHeight;
            double baseWidth;
            double baseHeight;
            if (fillViewport)
            {
                // UniformToFill: изображение полностью заполняет область.
                if (imageRatio > viewportRatio)
                {
                    baseHeight = viewportHeight;
                    baseWidth = viewportHeight * imageRatio;
                }
                else
                {
                    baseWidth = viewportWidth;
                    baseHeight = viewportWidth / imageRatio;
                }
            }
            else
            {
                // Uniform: всё изображение видно, возможны полосы по краям.
                if (imageRatio > viewportRatio)
                {
                    baseWidth = viewportWidth;
                    baseHeight = viewportWidth / imageRatio;
                }
                else
                {
                    baseHeight = viewportHeight;
                    baseWidth = viewportHeight * imageRatio;
                }
            }

            double scaledWidth = baseWidth * zoom;
            double scaledHeight = baseHeight * zoom;
            double maxX = Math.Max(0, (scaledWidth - viewportWidth) / 2.0);
            double maxY = Math.Max(0, (scaledHeight - viewportHeight) / 2.0);
            return new Point(Math.Max(-maxX, Math.Min(maxX, x)), Math.Max(-maxY, Math.Min(maxY, y)));
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (wallpaper == null || PreviewImage.Source == null) return;
            FullscreenImage.Source = PreviewImage.Source;
            FullscreenTitleText.Text = wallpaper.Title;
            _fullscreenZoom = 1.0;
            SetTransform(FullscreenScale, FullscreenTranslate, 1.0, 0, 0);
            SetFullscreenControlsVisibility(true);
            m.EnterWallpaperFullscreen();
            FullscreenOverlay.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyFullscreenBounds();
                FullscreenOverlay.Focus();
                Keyboard.Focus(FullscreenOverlay);
            }));
        }

        private void CloseFullscreen_Click(object sender, RoutedEventArgs e)
        {
            CloseFullscreen();
            e.Handled = true;
        }


        public void CloseFullscreen()
        {
            if (FullscreenOverlay == null || FullscreenOverlay.Visibility != Visibility.Visible)
                return;

            EndFullscreenDrag();
            FullscreenOverlay.Visibility = Visibility.Collapsed;
            m.ExitWallpaperFullscreen();
            Focusable = true;
            Focus();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            m.Navigate("wallpapers");
        }
    }
}
