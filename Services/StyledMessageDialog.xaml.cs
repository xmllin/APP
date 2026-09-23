using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;

namespace Nexora.Services
{
    public partial class StyledMessageDialog : Window
    {
        private readonly MessageBoxButton _buttons;

        private StyledMessageDialog(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();

            CaptionText.Text = string.IsNullOrWhiteSpace(caption) ? "Уведомление" : caption;
            SetMessage(message ?? string.Empty);
            _buttons = buttons;

            ApplyKind(image);
            ApplyButtons(buttons);
        }

        public static MessageBoxResult Show(
            string message,
            string caption = "Уведомление",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.Information)
        {
            var dialog = new StyledMessageDialog(message, caption, buttons, image);
            var owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive);

            if (owner != null && owner != dialog)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.ShowDialog();
            return dialog.DialogResultValue;
        }

        private MessageBoxResult DialogResultValue { get; set; } = MessageBoxResult.None;

        private void SetMessage(string message)
        {
            MessageText.Inlines.Clear();
            var parts = Regex.Split(message, "(\\\"[^\\\"]+\\\")");
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (part.Length >= 2 && part[0] == '\"' && part[part.Length - 1] == '\"')
                {
                    var name = part.Substring(1, part.Length - 2);
                    MessageText.Inlines.Add(new Run(name)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(88, 174, 255)),
                        FontWeight = FontWeights.SemiBold
                    });
                }
                else
                {
                    MessageText.Inlines.Add(new Run(part));
                }
            }
        }

        private void ApplyKind(MessageBoxImage image)
        {
            string background;
            string border;
            string glyph;

            if ((image & MessageBoxImage.Error) == MessageBoxImage.Error)
            {
                background = "#9E2738";
                border = "#D34F5F";
                glyph = "!";
            }
            else if ((image & MessageBoxImage.Warning) == MessageBoxImage.Warning)
            {
                background = "#9A6A22";
                border = "#D99B35";
                glyph = "!";
            }
            else if ((image & MessageBoxImage.Question) == MessageBoxImage.Question)
            {
                background = "#176BE0";
                border = "#3D8FE8";
                glyph = "?";
            }
            else
            {
                background = "#176BE0";
                border = "#3D8FE8";
                glyph = "i";
            }

            IconHost.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background));
            IconHost.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));
            IconHost.BorderThickness = new Thickness(1);
            IconText.Text = glyph;
        }

        private void ApplyButtons(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    OkButton.Visibility = Visibility.Collapsed;
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    OkButton.Content = "OK";
                    NoButton.Content = "Отмена";
                    OkButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    break;
                default:
                    OkButton.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = _buttons == MessageBoxButton.OKCancel
                ? MessageBoxResult.Cancel
                : MessageBoxResult.No;
            DialogResult = false;
            Close();
        }
    }
}