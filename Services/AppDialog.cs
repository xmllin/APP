using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace WpfApp1.Services
{
    public static class AppDialog
    {
        public static void ShowInfo(Window owner, string title, string message)
        {
            Show(owner, title, message, false);
        }

        public static bool ShowConfirm(Window owner, string title, string message)
        {
            return Show(owner, title, message, true);
        }

        public static void ShowError(Window owner, string title, string message)
        {
            Show(owner, title, message, false);
        }

        private static bool Show(Window owner, string title, string message, bool confirm)
        {
            var dialog = new Window
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                ShowInTaskbar = false,
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 620,
                Title = title
            };

            var root = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 22, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(49, 81, 110)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0)
            };
            root.Cursor = System.Windows.Input.Cursors.Arrow;
            root.MouseLeftButtonDown += (sender, args) =>
            {
                var source = args.OriginalSource as DependencyObject;
                while (source != null)
                {
                    if (source is Button) return;
                    source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
                }
                root.Cursor = System.Windows.Input.Cursors.Hand;
                dialog.DragMove();
                root.Cursor = System.Windows.Input.Cursors.Arrow;
            };
            var shell = new DockPanel();
            var titleBar = new Grid { Height = 36, Cursor = System.Windows.Input.Cursors.Arrow };
            titleBar.Margin = new Thickness(16, 0, 0, 0);
            titleBar.ColumnDefinitions.Add(new ColumnDefinition());
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleBarText = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(238, 245, 255)),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeButton = CreateCloseButton();
            closeButton.Click += (sender, args) => dialog.DialogResult = false;
            Grid.SetColumn(closeButton, 1);
            titleBar.Children.Add(titleBarText);
            titleBar.Children.Add(closeButton);
            DockPanel.SetDock(titleBar, Dock.Top);
            shell.Children.Add(titleBar);

            var layout = new Grid { MinWidth = 360, Margin = new Thickness(22, 0, 22, 22) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var messageBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(194, 210, 228)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 560,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(messageBlock, 0);
            layout.RowDefinitions[0].Height = GridLength.Auto;
            layout.Children.Add(messageBlock);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = CreateButton(confirm ? "Нет" : "ОК", false);
            ok.Click += (sender, args) => { dialog.DialogResult = confirm ? false : true; };
            buttons.Children.Add(ok);
            if (confirm)
            {
                var yes = CreateButton("Да", true);
                yes.Click += (sender, args) => dialog.DialogResult = true;
                buttons.Children.Add(yes);
            }
            Grid.SetRow(buttons, 1);
            layout.Children.Add(buttons);
            shell.Children.Add(layout);
            root.Child = shell;
            dialog.Content = root;
            dialog.KeyDown += (sender, args) =>
            {
                if (args.Key == System.Windows.Input.Key.Escape) dialog.DialogResult = false;
            };
            return dialog.ShowDialog() == true;
        }

        private static Button CreateButton(string text, bool primary)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 78,
                Height = 32,
                Padding = new Thickness(14, 4, 14, 4),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(primary ? Color.FromRgb(23, 107, 224) : Color.FromRgb(15, 32, 53)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 246, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(49, 81, 110)),
                BorderThickness = new Thickness(1),
                FocusVisualStyle = null
            };
            var resourceName = primary ? "PrimaryButton" : "GhostButton";
            var appStyle = System.Windows.Application.Current?.TryFindResource(resourceName) as Style;
            if (appStyle != null) button.Style = appStyle;
            return button;
        }

        private static Button CreateCloseButton()
        {
            var button = new Button
            {
                Content = new Image
                {
                    Source = SvgImageLoader.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "interface", "white", "fluent-dismiss.svg")),
                    Width = 14,
                    Height = 14
                },
                Width = 28,
                Height = 26,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(194, 210, 228)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FocusVisualStyle = null
            };
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.TemplateProperty, CreateCloseTemplate()));
            style.Triggers.Add(new System.Windows.Trigger
            {
                Property = Button.IsMouseOverProperty,
                Value = true,
                Setters = { new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(139, 47, 62))), new Setter(Button.ForegroundProperty, Brushes.White) }
            });
            button.Style = style;
            return button;
        }

        private static ControlTemplate CreateCloseTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "CloseBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(139, 47, 62)), "CloseBorder"));
            hover.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(108, 34, 48)), "CloseBorder"));
            template.Triggers.Add(hover);
            template.Triggers.Add(pressed);
            return template;
        }
    }
}
