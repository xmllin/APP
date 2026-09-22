using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Services;

namespace WpfApp1.Pages
{
    public partial class DiskCleanupPage : UserControl
    {
        private readonly MainWindow _main;
        private readonly DiskCleanupService _service = new DiskCleanupService();
        private readonly ObservableCollection<DiskCleanupItem> _items = new ObservableCollection<DiskCleanupItem>();
        private CancellationTokenSource? _cts;
        private bool _busy;

        public DiskCleanupPage(MainWindow main)
        {
            _main = main;
            InitializeComponent();
            CleanupItemsControl.ItemsSource = _items;
            Loaded += DiskCleanupPage_Loaded;
        }

        private async void DiskCleanupPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= DiskCleanupPage_Loaded;
            await ScanAsync();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e) => await ScanAsync();

        private async Task ScanAsync()
        {
            if (_busy) return;
            _busy = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            SetBusy(true, "Сканирование диска…");
            try
            {
                if (_items.Count == 0)
                {
                    foreach (var item in _service.GetCleanupItems())
                    {
                        item.PropertyChanged += (_, __) => Dispatcher.BeginInvoke(new Action(UpdateSummary));
                        _items.Add(item);
                    }
                }

                foreach (var item in _items)
                {
                    item.Status = "Сканирование…";
                    item.SizeBytes = 0;
                    item.FileCount = 0;
                }

                await _service.ScanAllAsync(_items, _cts.Token);
                PageStatusText.Text = "Сканирование завершено.";
            }
            catch (OperationCanceledException)
            {
                PageStatusText.Text = "Сканирование отменено.";
            }
            catch (Exception ex)
            {
                PageStatusText.Text = "Ошибка сканирования: " + ex.Message;
            }
            finally
            {
                _busy = false;
                SetBusy(false, PageStatusText.Text);
                UpdateSummary();
            }
        }

        private async void CleanItem_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            if (!(sender is Button button) || !(button.Tag is DiskCleanupItem item) || item.SizeBytes <= 0) return;

            var message = item.IsDangerous
                ? $"«{item.Name}» может содержать данные для восстановления Windows. Продолжить очистку?"
                : $"Удалить найденные данные из «{item.Name}»?";

            if (MessageBox.Show(message, "Очистка диска", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await CleanItemsAsync(new[] { item });
        }

        private async void CleanSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var selected = _items.Where(i => i.IsSelected && i.SizeBytes > 0).ToList();
            if (selected.Count == 0)
            {
                PageStatusText.Text = "Нет выбранных данных для очистки.";
                return;
            }

            var total = selected.Sum(i => i.SizeBytes);
            if (MessageBox.Show(
                    $"Будет обработано элементов: {selected.Count}\nМожно освободить примерно {DiskCleanupItem.FormatBytes(total)}.\n\nПродолжить?",
                    "Очистка диска",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await CleanItemsAsync(selected);
        }

        private async Task CleanItemsAsync(System.Collections.Generic.IEnumerable<DiskCleanupItem> selected)
        {
            _busy = true;
            SetBusy(true, "Очистка…");
            long freed = 0;
            try
            {
                foreach (var item in selected)
                    freed += await _service.CleanAsync(item, _cts?.Token ?? CancellationToken.None);

                PageStatusText.Text = freed > 0
                    ? $"Освобождено {DiskCleanupItem.FormatBytes(freed)}."
                    : "Очистка завершена. Заблокированные файлы пропущены.";

                await _service.ScanAllAsync(_items, _cts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                PageStatusText.Text = "Очистка отменена.";
            }
            catch (Exception ex)
            {
                PageStatusText.Text = "Ошибка очистки: " + ex.Message;
            }
            finally
            {
                _busy = false;
                SetBusy(false, PageStatusText.Text);
                UpdateSummary();
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            var shouldSelect = _items.Any(i => i.SizeBytes > 0 && !i.IsSelected);
            foreach (var item in _items)
                item.IsSelected = shouldSelect && item.SizeBytes > 0 && !item.IsDangerous;
            UpdateSummary();
        }

        private void SetBusy(bool busy, string status)
        {
            ScanButton.IsEnabled = !busy;
            PageStatusText.Text = status;
        }

        private void UpdateSummary()
        {
            var selected = _items.Where(i => i.IsSelected && i.SizeBytes > 0).ToList();
            var bytes = selected.Sum(i => i.SizeBytes);
            var count = selected.Sum(i => i.FileCount);
            SummaryText.Text = DiskCleanupItem.FormatBytes(bytes);
            SummaryDetailsText.Text = $"{selected.Count} элементов · {count:N0} файлов";
        }
    }
}
