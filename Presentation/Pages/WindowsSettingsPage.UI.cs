using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Nexora.Services;
using Nexora.Services.Libraries;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Nexora.Infrastructure.Registry;
using Nexora.Infrastructure.Processes;
using Nexora.Services.WindowsSettings;
using Nexora.Domain.WindowsSettings;

namespace Nexora.Pages
{
    public partial class WindowsSettingsPage : UserControl
    {
		private ComboBox CreateManagedCombo(string tag, IEnumerable<string> items, double width)
		{
			var combo = CreateTaskbarCombo(tag, items, width);
			_managedCombos[tag] = combo;
			return combo;
		}

		private const string SettingsSearchPlaceholder = "Поиск настроек Windows...";
		private TextBlock _noSettingsResultsText;

		private void SettingsCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshSettingsSearch();
		}

		private void SettingsSearchBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (string.Equals(SettingsSearchBox.Text, SettingsSearchPlaceholder, StringComparison.Ordinal))
			{
				SettingsSearchBox.Text = string.Empty;
				SettingsSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(241, 246, 255));
			}
		}

		private void SettingsSearchBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SettingsSearchBox.Text))
			{
				SettingsSearchBox.Text = SettingsSearchPlaceholder;
				SettingsSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(241, 246, 255));
			}
		}

		private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (SettingsSearchBox == null || SettingsStack == null)
				return;

			RefreshSettingsSearch();
		}

		private void RefreshSettingsSearch()
		{
			string category = SettingsCategoryComboBox?.SelectedItem is ComboBoxItem item
				? item.Tag as string
				: "Все";

			SetSettingsCategoryVisibility(category);

			string query = SettingsSearchBox?.Text?.Trim() ?? string.Empty;
			if (string.Equals(query, SettingsSearchPlaceholder, StringComparison.OrdinalIgnoreCase))
				query = string.Empty;

			var rows = FindVisualElements<Border>(SettingsStack)
				.Where(IsWindowsSettingsRow)
				.Distinct()
				.ToList();

			// Сначала возвращаем все строки в пределах выбранной категории.
			foreach (var row in rows)
				row.Visibility = Visibility.Visible;

			if (!string.IsNullOrWhiteSpace(query))
			{
				var normalizedQuery = query.ToLowerInvariant();

				foreach (var row in rows)
				{
					var text = string.Join(" ",
						FindTextBlocks(row)
							.Select(block => block.Text)
							.Where(value => !string.IsNullOrWhiteSpace(value)))
						.ToLowerInvariant();

					row.Visibility = text.Contains(normalizedQuery, StringComparison.Ordinal)
						? Visibility.Visible
						: Visibility.Collapsed;
				}

				// Скрываем целиком категории, в которых после фильтрации
				// не осталось ни одной подходящей настройки. Используем
				// сами панели категорий, а не SettingsStack.Children: часть
				// категорий создаётся динамически после InitializeComponent.
				foreach (var section in GetSettingsCategoryPanels())
				{
					if (section == null)
						continue;

					var sectionRows = FindVisualElements<Border>(section)
						.Where(IsWindowsSettingsRow)
						.Distinct()
						.ToList();

					section.Visibility = sectionRows.Any(row => row.Visibility == Visibility.Visible)
						? Visibility.Visible
						: Visibility.Collapsed;
				}
			}
			else if (_noSettingsResultsText != null)
			{
				_noSettingsResultsText.Visibility = Visibility.Collapsed;
			}

			bool hasVisibleResult = string.IsNullOrWhiteSpace(query)
				|| rows.Any(row => row.Visibility == Visibility.Visible);

			if (!hasVisibleResult)
			{
				if (_noSettingsResultsText == null)
				{
					_noSettingsResultsText = new TextBlock
					{
						Text = "Ничего не найдено",
						FontSize = 18,
						FontWeight = FontWeights.SemiBold,
						Foreground = new SolidColorBrush(Colors.White),
						TextAlignment = TextAlignment.Center,
						HorizontalAlignment = HorizontalAlignment.Stretch,
						Margin = new Thickness(0, 36, 0, 36)
					};

					SettingsStack.Children.Add(_noSettingsResultsText);
				}

				_noSettingsResultsText.Visibility = Visibility.Visible;
			}
			else if (_noSettingsResultsText != null)
			{
				_noSettingsResultsText.Visibility = Visibility.Collapsed;
			}
		}
		private bool IsWindowsSettingsRow(Border border)
		{
			return border != null && ReferenceEquals(border.Style, FindResource("SettingRow"));
		}
		private IEnumerable<Border> GetSettingsCategoryPanels()
		{
			yield return ExplorerSettingsPanel;
			yield return DesktopSettingsPanel;
			yield return PowerSettingsPanel;
			yield return MouseSettingsPanel;
			yield return WindowsUpdateSettingsPanel;
			yield return _startSearchSettingsPanel;
			yield return _gamingSettingsPanel;
			yield return _systemSettingsPanel;
			yield return _securityBehaviorPanel;
		}


		private void SetSettingsCategoryVisibility(string category)
		{
			bool showAll = string.Equals(category, "Все", StringComparison.OrdinalIgnoreCase);
			SetPanelVisibility(ExplorerSettingsPanel, showAll || category == "Проводник");
			SetPanelVisibility(DesktopSettingsPanel, showAll || category == "Рабочий стол");
			SetPanelVisibility(PowerSettingsPanel, showAll || category == "Питание");
			SetPanelVisibility(MouseSettingsPanel, showAll || category == "Мышь");
			SetPanelVisibility(WindowsUpdateSettingsPanel, showAll || category == "Windows Update");
			SetPanelVisibility(_startSearchSettingsPanel, showAll || category == "Пуск и поиск");
			SetPanelVisibility(_gamingSettingsPanel, showAll || category == "Игры и производительность");
			SetPanelVisibility(_systemSettingsPanel, showAll || category == "Система");
			SetPanelVisibility(_securityBehaviorPanel, showAll || category == "Безопасность и конфиденциальность");
			SetPanelVisibility(PrivacySettingsPanel, false);
		}

		private static void SetPanelVisibility(FrameworkElement panel, bool visible)
		{
			if (panel == null)
				return;

			panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		}

		private async void WindowsSettingsPage_Loaded(object sender, RoutedEventArgs e)
		{
			_hostWindow = Window.GetWindow(this);
			if (_hostWindow != null) _hostWindow.Activated += HostWindow_Activated;
			RefreshSettingsSearch();
			EnsureRestartHints();
			LoadExplorerSettings();
			RefreshMouseSettings();
			ApplyAdminToggleLockState();
			await LoadPowerShellScriptsStateAsync();
			await UpdateGameBarAvailabilityAsync();
		}

		private async Task UpdateGameBarAvailabilityAsync()
		{
			try
			{
				_gameBarAvailable = await _uwpPackageService.IsPackageInstalledAsync("Microsoft.XboxGamingOverlay", CancellationToken.None);
			}
			catch
			{
				_gameBarAvailable = false;
			}

			if (_managedToggles.TryGetValue("GameBar", out var toggle))
			{
				if (!_gameBarAvailable)
				{
					SetToggle(toggle, false);
					SetAdditionalStatusLabel("GameBar", "Недоступно: Xbox Game Bar не установлен", false);
				}
				ApplyAdminToggleLockState();
			}
		}

		private void WindowsSettingsPage_Unloaded(object sender, RoutedEventArgs e)
		{
			if (_hostWindow != null) _hostWindow.Activated -= HostWindow_Activated;
			_hostWindow = null;
		}

		private void HostWindow_Activated(object sender, EventArgs e)
		{
			if (IsVisible)
				LoadExplorerSettings();
		}

		private void MoveSecuritySettingsToBottom()
		{
			var memoryRow = DisableMemoryIntegrityToggle.Parent is StackPanel rightPanel && rightPanel.Parent is Grid grid && grid.Parent is Border row
				? row
				: null;
			if (memoryRow == null) return;

			var memoryGrid = (Grid)memoryRow.Child;
			var memoryDescription = memoryGrid.Children.OfType<StackPanel>().FirstOrDefault();
			if (memoryDescription != null && memoryDescription.Children.Count > 1)
			{
				((TextBlock)memoryDescription.Children[0]).Text = "Отключить целостность памяти (HVCI)";
				((TextBlock)memoryDescription.Children[1]).Text = "Отключить Hypervisor-protected Code Integrity";
			}

			SettingsStack.Children.Remove(memoryRow);
			_disableVbsLabel = new TextBlock
			{
				Text = "Отключено",
				Foreground = new SolidColorBrush(Colors.Red),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 10, 0)
			};
			_disableVbsToggle = new ToggleButton
			{
				Tag = "DisableVbs",
				Style = (Style)FindResource("ExplorerToggleButton")
			};
			_disableVbsToggle.Checked += WindowsFeatureToggle_Changed;
			_disableVbsToggle.Unchecked += WindowsFeatureToggle_Changed;

			var vbsRow = new Border { Style = (Style)FindResource("SettingRow") };
			var vbsGrid = new Grid();
			vbsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			vbsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			var vbsDescription = new StackPanel();
			vbsDescription.Children.Add(new TextBlock { Text = "Отключить виртуализацию на основе безопасности (VBS)", FontWeight = FontWeights.SemiBold });
			vbsDescription.Children.Add(new TextBlock { Text = "Отключить Virtualization Based Security", Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)), FontSize = 11 });
			var vbsControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			vbsControls.Children.Add(_disableVbsLabel);
			vbsControls.Children.Add(_disableVbsToggle);
			Grid.SetColumn(vbsDescription, 0);
			Grid.SetColumn(vbsControls, 1);
			vbsGrid.Children.Add(vbsDescription);
			vbsGrid.Children.Add(vbsControls);
			vbsRow.Child = vbsGrid;

			SettingsStack.Children.Add(vbsRow);
			SettingsStack.Children.Add(memoryRow);
		}

		private void MoveAdditionalSettingsToBottom()
		{
			var toggles = new ToggleButton[]
			{
				DisableTelemetryToggle,
				DisableAppDiagnosticsToggle,
				DisableActivityToggle,
				DisablePerformanceToggle,
				DisableKeystrokesToggle,
				DisableVoiceDataToggle,
				DisableStickyKeysToggle,
				DisableBingSearchToggle,
				DisableHibernationToggle,
				DisableSmartScreenToggle,
				DisableUacToggle,
				DisablePageFileToggle,
				DisableBitLockerAutoEncryptionToggle,
				_disableVbsToggle,
				DisableMemoryIntegrityToggle
			};
			var rows = toggles.Select(GetSettingRow).Where(row => row != null).Distinct().ToList();
			if (rows.Count == 0) return;

			foreach (var row in rows)
			{
				if (row.Parent is Panel parent) parent.Children.Remove(row);
				else SettingsStack.Children.Remove(row);
			}

			var startSearchRows = new List<UIElement>
			{
				CreateAdditionalToggleRow(DisableBingSearchToggle, "Отключить веб-поиск Windows", "Отключает Bing, веб-подсказки и веб-результаты поиска Windows"),
				CreateAdditionalToggleRow(CreateManagedToggle("SystemSuggestions"), "Отключить системные предложения", "Отключить встроенные рекомендации и предложения Windows"),
				CreateAdditionalToggleRow(CreateManagedToggle("DisableStartRecommended"), "Скрыть раздел «Рекомендуемое»", "Убрать блок рекомендуемых элементов из меню «Пуск»"),
				CreateAdditionalToggleRow(CreateManagedToggle("DisableSettings365Ads"), "Отключить рекламу Microsoft в настройках", "Убрать предложения Microsoft 365 и потребительский контент из «Параметров»"),
				CreateAdditionalToggleRow(CreateManagedToggle("DisablePreinstalledApps"), "Блокировать автоматическую установку предустановленных приложений", "Запретить Content Delivery Manager автоматически устанавливать рекламные и предустановленные приложения")
			};

			var gamingRows = new List<UIElement>
			{
				CreateAdditionalToggleRow(CreateGameModeToggle(), "Игровой режим", "Автоматическое включение режима Windows Game Mode для игр"),
				CreateHagsRow(),
				CreateAdditionalToggleRow(CreateManagedToggle("GameBar"), "Отключить Xbox Game Bar", "Отключить игровой оверлей Xbox Game Bar"),
				CreateAdditionalToggleRow(CreateManagedToggle("FullscreenOptimizations"), "Оптимизация для игр в оконном режиме", "Управлять настройкой Windows для оптимизации игр в оконном режиме")
			};

			var systemRows = new List<UIElement>
			{
				CreateAdditionalToggleRow(_darkThemeToggle, "Тёмная тема Windows", "Тёмная тема для приложений и системных элементов"),
				CreateAdditionalToggleRow(_clipboardToggle, "История буфера обмена", "Сохранять несколько последних скопированных элементов"),
				CreateAdditionalToggleRow(DisableStickyKeysToggle, "Отключить залипание клавиш", "Отключить Sticky Keys, Filter Keys и Toggle Keys"),
				CreateAdditionalToggleRow(_disableLockScreenBlurToggle, "Убрать размытие экрана входа", "Отключить acrylic/blur-эффект на экране входа Windows"),
				CreateAdditionalToggleRow(CreateManagedToggle("DeveloperMode"), "Режим разработчика", "Разрешить установку и разработку приложений без лицензии разработчика"),
				CreateAdditionalToggleRow(CreateManagedToggle("LongPathsEnabled"), "Поддержка длинных путей", "Разрешить длинные пути файловой системы"),
				CreateAdditionalToggleRow(CreateManagedToggle("NumLockOnBoot"), "NumLock при загрузке", "Включать NumLock на экране входа и для текущего пользователя")
			};

			var securityRows = new List<UIElement>
			{
				CreateAdditionalToggleRow(DisableSmartScreenToggle, "Отключить SmartScreen", "Отключить проверку приложений и загружаемых файлов"),
				CreateAdditionalToggleRow(DisableUacToggle, "UAC: никогда не уведомлять", "Не показывать запросы UAC при повышении прав"),
				CreateAdditionalToggleRow(_disableVbsToggle, "Отключить VBS", "Отключить Virtualization Based Security"),
				CreateAdditionalToggleRow(DisableMemoryIntegrityToggle, "Отключить целостность памяти (HVCI)", "Отключить Hypervisor-protected Code Integrity"),
				CreateAdditionalToggleRow(DisablePageFileToggle, "Отключить файл подкачки", "Отключить pagefile с сохранением текущей конфигурации"),
				CreateAdditionalToggleRow(DisableBitLockerAutoEncryptionToggle, "Отключить авто-шифрование BitLocker", "Запретить автоматическое шифрование устройства")
			};

			foreach (var tag in new[]
			{
				"DisableTelemetry",
				"DisableErrorReporting",
				"DisableAdvertisingAndSuggestions",
				"DisableActivityHistory",
				"DisableLocationAndSensors",
				"DisableCortana",
				"DisableCopilot",
				"DisableContentDeliveryManager",
				"DisableFindMyDevice",
				"DisableDeliveryOptimization"
			})
			{
				var title = tag switch
				{
					"DisableTelemetry" => "Отключить телеметрию Windows",
					"DisableAppDiagnostics" => "Отключить диагностику приложений",
					"DisableActivity" => "Отключить сбор данных об активности",
					"DisablePerformance" => "Отключить диагностику производительности",
					"DisableKeystrokes" => "Отключить сбор данных ввода",
					"DisableVoiceData" => "Отключить голосовые данные",
					"DisableErrorReporting" => "Отключить отчёты об ошибках Windows",
					"DisableAdvertisingAndSuggestions" => "Отключить рекламу и персонализированные предложения",
					"DisableNewsAndInterests" => "Отключить «Новости и интересы»",
					"HideMeetNowButton" => "Скрыть Meet Now",
					"DisableActivityHistory" => "Отключить историю активности",
					"DisableLocationAndSensors" => "Отключить геолокацию и датчики",
					"DisableAutoLogger" => "Отключить WMI AutoLogger",
					"DisableCortana" => "Отключить Cortana и облачный поиск",
					"DisableCopilot" => "Отключить Windows Copilot",
					"DisableContentDeliveryManager" => "Отключить доставку контента Windows",
					"DisableFindMyDevice" => "Отключить «Найти устройство»",
					_ => "Отключить Delivery Optimization"
				};
				securityRows.Add(CreateAdditionalToggleRow(CreateManagedToggle(tag), title, "Управляет соответствующими параметрами и политиками Windows"));
			}
			securityRows.Add(CreatePowerShellScriptsRow());

			var powerPanel = PowerSettingsPanel?.Child as StackPanel;
			if (powerPanel != null)
			{
				powerPanel.Children.Add(CreateAdditionalToggleRow(DisableHibernationToggle, "Отключить гибернацию и быстрый запуск", "Использовать powercfg /h off; при включении вернуть поддержку гибернации"));
				powerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("DisableUSBPowerSaving"), "Отключить энергосбережение USB", "Не переводить USB-устройства в энергосберегающий режим; исходные состояния сохраняются"));
				powerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("DisableSystemThrottling"), "Отключить системное дросселирование", "Отключить Power Throttling Windows"));
			}

			_startSearchSettingsPanel = CreateDynamicSettingsPanel("Пуск и поиск", "Поиск, рекомендации и потребительский контент Windows", startSearchRows);
			_gamingSettingsPanel = CreateDynamicSettingsPanel("Игры и производительность", "Игровые функции, DVR и полноэкранные оптимизации", gamingRows);
			_systemSettingsPanel = CreateDynamicSettingsPanel("Система", "Общие пользовательские и системные параметры Windows", systemRows);
			_securityBehaviorPanel = CreateDynamicSettingsPanel("Безопасность и конфиденциальность", "Защитные механизмы, диагностика, телеметрия и политики", securityRows);

			SettingsStack.Children.Add(_startSearchSettingsPanel);
			SettingsStack.Children.Add(_gamingSettingsPanel);
			SettingsStack.Children.Add(_systemSettingsPanel);
			SettingsStack.Children.Add(_securityBehaviorPanel);
		}

		private Border CreateDynamicSettingsPanel(string title, string description, IEnumerable<UIElement> rows)
		{
			var content = new StackPanel();
			content.Children.Add(CreateDynamicCategoryHeader(title));
			foreach (var row in rows)
				if (row != null) content.Children.Add(row);

			return new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(10, 29, 57)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(23, 58, 97)),
				BorderThickness = new Thickness(1, 0, 1, 0),
				CornerRadius = new CornerRadius(11),
				Padding = new Thickness(0, 4, 0, 4),
				Margin = new Thickness(0, 10, 0, 0),
				Child = content
			};
		}


		private Border CreateTaskbarSubsectionHeader(string text)
		{
			return new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(8, 24, 46)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(23, 58, 97)),
				BorderThickness = new Thickness(0, 1, 0, 1),
				Padding = new Thickness(16, 8, 16, 8),
				Margin = new Thickness(0, 8, 0, 2),
				Child = new TextBlock
				{
					Text = text,
					FontSize = 12,
					FontWeight = FontWeights.SemiBold,
					Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207))
				}
			};
		}

		private void RemoveObsoleteSettingRows()
		{
			foreach (var tag in new[]
			{
				"WindowShake",
				"ToastNotifications",
				"ShowDesktopIcons",
				"BackgroundRecording",
				"HideMeetNowButton",
				"DisableNewsAndInterests",
				"DisableAutoLogger"
			})
			{
				var toggle = GetAllToggleButtons().FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
				var row = toggle == null ? null : GetSettingRow(toggle);
				if (row?.Parent is Panel parent)
					parent.Children.Remove(row);
			}
		}

		private TextBlock CreateCategoryHeader(string text)
			{
				return new TextBlock
				{
					Text = text,
					FontSize = 15,
					FontWeight = FontWeights.SemiBold,
					Foreground = new SolidColorBrush(Color.FromRgb(214, 230, 250)),
					Margin = new Thickness(16, 14, 16, 4)
				};
			}

			private Border CreateDynamicCategoryHeader(string text)
		{
			return new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(13, 35, 64)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(33, 73, 111)),
				BorderThickness = new Thickness(0, 0, 0, 1),
				Padding = new Thickness(16, 11, 16, 11),
				Margin = new Thickness(0, 0, 0, 4),
				Child = new StackPanel
				{
					Children =
					{
						new TextBlock
						{
							Text = text,
							FontSize = 24,
							FontWeight = FontWeights.SemiBold,
							Foreground = new SolidColorBrush(Color.FromRgb(241, 246, 255))
						},
						new TextBlock
						{
							Text = "Для некоторых настроек требуются права администратора",
							Foreground = new SolidColorBrush(Color.FromRgb(255, 211, 78)),
							FontSize = 11,
							Margin = new Thickness(0, 3, 0, 0)
						}
					}
				}
			};
		}

		private StackPanel CreateNameTemplateControls()
			{
				var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
				var apply = new Button
				{
					Content = "Применить",
					Style = (Style)FindResource("GhostButton"),
					Padding = new Thickness(10, 5, 10, 5),
					Margin = new Thickness(8, 0, 0, 0)
				};
				var reset = new Button
				{
					Content = "По умолчанию",
					Style = (Style)FindResource("GhostButton"),
					Padding = new Thickness(10, 5, 10, 5),
					Margin = new Thickness(8, 0, 0, 0)
				};
				apply.Click += DefaultNameTemplateApply_Click;
				reset.Click += DefaultNameTemplateReset_Click;
				controls.Children.Add(_defaultNameTemplateBox);
				controls.Children.Add(apply);
				controls.Children.Add(reset);
				return controls;
			}

			private StackPanel CreateMouseSettingsControls()
			{
				var controls = new StackPanel
				{
					Orientation = Orientation.Vertical,
					HorizontalAlignment = HorizontalAlignment.Right,
					Width = 430
				};

				var speedPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
				speedPanel.Children.Add(new TextBlock
				{
					Text = "Скорость",
					Width = 70,
					VerticalAlignment = VerticalAlignment.Center
				});
				speedPanel.Children.Add(_mouseSpeedSlider);
				speedPanel.Children.Add(new TextBlock
				{
					Text = "1–20",
					Width = 45,
					Margin = new Thickness(8, 0, 0, 0),
					Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)),
					VerticalAlignment = VerticalAlignment.Center
				});

				var scrollPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
				scrollPanel.Children.Add(new TextBlock
				{
					Text = "Прокрутка",
					Width = 70,
					VerticalAlignment = VerticalAlignment.Center
				});
				scrollPanel.Children.Add(_mouseScrollSlider);
				scrollPanel.Children.Add(new TextBlock
				{
					Text = "строк",
					Width = 45,
					Margin = new Thickness(8, 0, 0, 0),
					Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)),
					VerticalAlignment = VerticalAlignment.Center
				});

				var accelPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
				accelPanel.Children.Add(new TextBlock
				{
					Text = "Ускорение",
					Width = 70,
					VerticalAlignment = VerticalAlignment.Center
				});
				_mouseAccelerationLabel = new TextBlock
				{
					Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)),
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 8, 0)
				};
				accelPanel.Children.Add(_mouseAccelerationLabel);
				accelPanel.Children.Add(_mouseAccelerationToggle);
				UpdateMouseAccelerationLabel();

				var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 7, 0, 0) };
				var apply = new Button
				{
					Content = "Применить",
					Style = (Style)FindResource("GhostButton"),
					Padding = new Thickness(10, 5, 10, 5)
				};
				var reset = new Button
				{
					Content = "По умолчанию",
					Style = (Style)FindResource("GhostButton"),
					Padding = new Thickness(10, 5, 10, 5),
					Margin = new Thickness(8, 0, 0, 0)
				};
				apply.Click += MouseSettingsApply_Click;
				reset.Click += MouseSpeedDefault_Click;
				buttons.Children.Add(apply);
				buttons.Children.Add(reset);

				controls.Children.Add(speedPanel);
				controls.Children.Add(scrollPanel);
				controls.Children.Add(accelPanel);
				controls.Children.Add(buttons);
				return controls;
			}

		private void EnsureRestartHints()
		{
			foreach (var row in FindVisualElements<Border>(SettingsStack))
			{
				if (row.Style != (Style)FindResource("SettingRow"))
					continue;

				var tag = FindVisualElements<ToggleButton>(row)
					.Select(toggle => toggle.Tag as string)
					.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

				var hint = GetRestartHintText(tag);
				if (string.IsNullOrWhiteSpace(hint))
				{
					if (ReferenceEquals(row, GetSettingRow(MouseAccelerationToggle)))
						hint = GetRestartHintText("MouseAcceleration");
					else if (FindVisualElements<ComboBox>(row).Any(combo => ReferenceEquals(combo, PowerSchemeComboBox)))
						hint = GetRestartHintText("PowerScheme");
					else if (FindVisualElements<Button>(row).Any(button =>
						ReferenceEquals(button, PauseWindowsUpdateButton) ||
						ReferenceEquals(button, StartWindowsUpdateButton) ||
						ReferenceEquals(button, ClearWindowsUpdateCacheButton)))
						hint = GetRestartHintText("WindowsUpdateAction");
				}

				if (string.IsNullOrWhiteSpace(hint))
					continue;

				if (!(row.Child is Grid grid))
				{
					if (row.Child is StackPanel stack)
						AddRestartHint(stack, hint);
					continue;
				}

				var textPanel = grid.Children.OfType<StackPanel>().FirstOrDefault(panel =>
					panel.Children.OfType<TextBlock>().Any());
				if (textPanel != null)
					AddRestartHint(textPanel, hint);
			}
		}

		private static void AddRestartHint(StackPanel panel, string hint)
		{
			if (panel == null || string.IsNullOrWhiteSpace(hint))
				return;

			var existing = panel.Children.OfType<TextBlock>()
				.FirstOrDefault(x => (x.Text ?? string.Empty).StartsWith("Для применения", StringComparison.OrdinalIgnoreCase));
			if (existing != null)
			{
				existing.Text = hint;
				existing.Foreground = new SolidColorBrush(Color.FromRgb(255, 211, 78));
				existing.FontSize = 10;
				existing.TextWrapping = TextWrapping.Wrap;
				existing.Margin = new Thickness(0, 3, 0, 0);
				return;
			}

			panel.Children.Add(new TextBlock
			{
				Text = hint,
				Foreground = new SolidColorBrush(Color.FromRgb(255, 211, 78)),
				FontSize = 10,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0, 3, 0, 0)
			});
		}

		private static string GetRestartHintText(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
				return null;

			switch (tag)
			{
				// Системная оболочка/Проводник: реестр меняется сразу,
				// а уже запущенный explorer.exe перечитывает эти параметры после перезапуска.
				case "HiddenFiles":
				case "FileExtensions":
				case "Gallery":
				case "OpenThisPc":
				case "ExplorerHome":
				case "ShowRecentFiles":
				case "ShowFrequentFolders":
				case "Network":
				case "HideDownloads":
				case "HideDocuments":
				case "HideVideos":
				case "HidePictures":
				case "HideMusic":
				case "HideDesktop":
				case "ShortcutSuffix":
				case "ThisPcIcon":
				case "RecycleBinIcon":
				case "ShowSecondsInSystemClock":
				case "HideUserFiles":
				case "HideNetworkIcon":
				case "HideControlPanel":
				case "ShortcutArrow":
				case "ExplorerSyncNotifications":
				case "ExplorerCompactMode":
				case "SnapAssistFlyout":
				case "ClassicContextMenu":
				case "ExplorerItemCheckboxes":
				case "SpeedUpExplorerAndMenus":
				case "SystemSuggestions":
				case "DisableStartMenuWebSearch":
				case "DisableStartRecommended":
				case "DisablePreinstalledApps":
				case "DisableContentDeliveryManager":
				case "DisableAdvertisingAndSuggestions":
				case "DisableNewsAndInterests":
				case "HideMeetNowButton":
				case "DisableCortana":
				case "DisableCopilot":
					return "Для применения требуется перезапуск Проводника.";

				// В текущей реализации сервис явно помечает эти операции как
				// требующие перезапуска Windows.
				case "LongPathsEnabled":
				case "DisableSettings365Ads":
				case "DisableWindowsUpdate":
				case "DisableSmartScreen":
				case "DisableMemoryIntegrity":
				case "DisableVbs":
				case "HardwareGpuScheduling":
				case "DisablePageFile":
				case "DisableBitLockerAutoEncryption":
				case "BackgroundRecording":
				case "UacNeverNotify":
					return "Для применения требуется перезапуск Windows.";

				// Остальные операции в текущем сервисном контракте не выставляют
				// RequiresRestart и применяются без перезапуска приложения/Windows.
				case "PowerScheme":
				case "MouseAcceleration":
				case "DisableDriverUpdates":
				case "DisableReservedStorage":
				case "DisableTelemetry":
				case "DisableAppDiagnostics":
				case "DisableActivity":
				case "DisablePerformance":
				case "DisableKeystrokes":
				case "DisableVoiceData":
				case "DisableErrorReporting":
				case "DisableLocationAndSensors":
				case "DisableAutoLogger":
				case "DisableFindMyDevice":
				case "DisableDeliveryOptimization":
				case "DisableStickyKeys":
				case "DisableBingSearch":
				case "GameBar":
				case "FullscreenOptimizations":
				case "AutoGameModeEnabled":
				case "DeveloperMode":
				case "NumLockOnBoot":
				case "DisableHibernation":
				case "DisableUSBPowerSaving":
				case "DisableSystemThrottling":
				case "DisableLockScreenBlur":
				case "EnableDarkTheme":
				case "EnableClipboard":
				case "ClipboardHistory":
				case "ReduceContextMenuDelay":
				case "DisableWindowsAds":
					return "Для применения перезапуск не требуется — настройка применяется сразу.";

				case "WindowsUpdateAction":
					return "Для применения перезапуск не требуется.";

				default:
					return "Для применения перезапуск не требуется — настройка применяется сразу.";
			}
		}
		private static void SetTaskbarComboSelection(ComboBox combo, string value, int fallbackIndex)
		{
			if (combo == null || combo.IsKeyboardFocusWithin) return;

			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
				&& index >= 0 && index < combo.Items.Count)
			{
				combo.SelectedIndex = index;
				return;
			}

			var item = combo.Items.Cast<object>()
				.FirstOrDefault(candidate => string.Equals(candidate?.ToString(), value, StringComparison.OrdinalIgnoreCase));
			if (item != null)
			{
				combo.SelectedItem = item;
				return;
			}

			if (fallbackIndex >= 0 && fallbackIndex < combo.Items.Count)
				combo.SelectedIndex = fallbackIndex;
		}

		private static bool IsUserSettingTag(string tag)
		{
			return tag == "OpenThisPc"
				 || tag == "HideUserFiles"
				|| tag == "HideNetworkIcon"
				|| tag == "HideControlPanel"
				|| tag == "ShowDesktopIcons"
				|| tag == "ToastNotifications"
				|| tag == "ClassicContextMenu"
				|| tag == "ExplorerItemCheckboxes"
				|| tag == "TaskbarWidgets"
				|| tag == "TaskbarTaskViewButton"
				|| tag == "TaskbarLastActiveClick"
				|| tag == "DisableStickyKeys"
				|| tag == "EnableTaskbarEndTask"
				|| tag == "TaskbarAutoHide"
				|| tag == "TaskbarBadges"
				|| tag == "TaskbarFlashing"
				|| tag == "TaskbarMultiMonitor"
				|| tag == "TaskbarShareWindow"
				|| tag == "TaskbarShowDesktop"
				|| tag == "ExplorerSyncNotifications"
				|| tag == "SystemSuggestions"
				|| tag == "ExplorerCompactMode"
				|| tag == "SnapAssistFlyout"
				|| tag == "ClipboardHistory"
				|| tag == "WindowShake"
				|| tag == "GameBar"
				|| tag == "AutoGameModeEnabled"
				|| tag == "FullscreenOptimizations"
				|| tag == "SpeedUpExplorerAndMenus"
				|| tag == "DisableStartMenuWebSearch"
				|| tag == "DisableStartRecommended"
				|| tag == "DisablePreinstalledApps"
				|| tag == "HideMeetNowButton"
				|| tag == "DisableContentDeliveryManager"
				|| tag == "EnableDarkTheme"
				|| tag == "ReduceContextMenuDelay"
				|| tag == "EnableClipboard"
				|| tag == "DisableWindowsAds";
		}

		private bool IsHardwareGpuSchedulingSupported() => _securitySettings.IsHardwareGpuSchedulingSupported();

		private bool IsHardwareGpuSchedulingEnabled() => _securitySettings.IsHardwareGpuSchedulingEnabled();

		private void SetHardwareGpuScheduling(bool enabled)
		{
			var result = _securitySettings.SetHardwareGpuScheduling(enabled);
			if (!result.Success) throw new InvalidOperationException(result.Error);
		}

    }
}
