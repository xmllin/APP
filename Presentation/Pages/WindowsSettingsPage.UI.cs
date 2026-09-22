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
using WpfApp1.Services;
using WpfApp1.Services.Libraries;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using WpfApp1.Infrastructure.Registry;
using WpfApp1.Infrastructure.Processes;
using WpfApp1.Services.WindowsSettings;
using WpfApp1.Domain.WindowsSettings;

namespace WpfApp1.Pages
{
    public partial class WindowsSettingsPage : UserControl
    {
		private ComboBox CreateManagedCombo(string tag, IEnumerable<string> items, double width)
		{
			var combo = CreateTaskbarCombo(tag, items, width);
			_managedCombos[tag] = combo;
			return combo;
		}

		private void SettingsCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SettingsCategoryComboBox == null || !(SettingsCategoryComboBox.SelectedItem is ComboBoxItem item))
				return;

			string category = item.Tag as string;
			SetSettingsCategoryVisibility(category);
		}

		private void SetSettingsCategoryVisibility(string category)
		{
			bool showAll = string.Equals(category, "Все", StringComparison.OrdinalIgnoreCase);
			SetPanelVisibility(ExplorerSettingsPanel, showAll || category == "Проводник");
			SetPanelVisibility(DesktopSettingsPanel, showAll || category == "Рабочий стол");
			SetPanelVisibility(TaskbarSettingsPanel, showAll || category == "Панель задач");
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
			if (SettingsCategoryComboBox != null && SettingsCategoryComboBox.SelectedItem is ComboBoxItem categoryItem)
				SetSettingsCategoryVisibility(categoryItem.Tag as string);
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
			var descriptionBlock = new TextBlock
			{
				Text = description,
				Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)),
				FontSize = 12,
				Margin = new Thickness(16, 2, 16, 7),
				TextWrapping = TextWrapping.Wrap
			};
			content.Children.Add(descriptionBlock);
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


		private void MoveTaskbarSettings()
		{
			var taskbarPanel = TaskbarSettingsPanel?.Child as StackPanel;
			if (taskbarPanel == null) return;

			var taskbarWidgets = CreateManagedToggle("TaskbarWidgets");
			var taskbarTaskView = CreateManagedToggle("TaskbarTaskViewButton");
			var taskbarLastActive = CreateManagedToggle("TaskbarLastActiveClick");
			var searchBoxMode = CreateManagedCombo(
				"SearchBoxTaskbarMode",
				Environment.OSVersion.Version.Build >= 22000
					? new[] { "Скрыто", "Только значок", "Значок и подпись", "Поле поиска" }
					: new[] { "Скрыто", "Только значок", "Поле поиска" },
				250);

			var secondsRow = GetSettingRow(ShowSecondsInSystemClockToggle);
			if (secondsRow != null && secondsRow.Parent is Panel desktopPanel)
				desktopPanel.Children.Remove(secondsRow);

			var mainRows = new List<UIElement>
			{
				CreateAdditionalValueRow("Выравнивание панели задач", "Расположение значков панели задач: слева или по центру", _taskbarAlignmentCombo),
				CreateAdditionalValueRow("Поиск на панели задач", "Выбрать способ отображения поиска на панели задач", searchBoxMode),
				CreateAdditionalToggleRow(taskbarWidgets, "Показывать виджеты", "Показывать кнопку и панель виджетов Windows"),
				CreateAdditionalToggleRow(taskbarTaskView, "Показывать представление задач", "Показывать кнопку Task View на панели задач"),
				CreateAdditionalToggleRow(_taskbarAutoHideToggle, "Автоматически скрывать панель задач", "Скрывать панель задач до наведения курсора к краю экрана"),
				CreateAdditionalToggleRow(_taskbarShowDesktopToggle, "Показывать рабочий стол в дальнем углу", "Щёлкните в дальнем углу панели задач, чтобы показать рабочий стол")
			};
			if (secondsRow != null)
				mainRows.Add(secondsRow);

			var behaviorRows = new List<UIElement>
			{
				CreateAdditionalValueRow("Объединение кнопок панели задач", "Всегда, при заполнении панели задач или никогда", _taskbarGlomCombo),
				CreateAdditionalToggleRow(_taskbarBadgesToggle, "Показывать значки на кнопках приложений", "Отображать счётчики и другие индикаторы на значках приложений"),
				CreateAdditionalToggleRow(_taskbarFlashingToggle, "Разрешить мигание значков", "Разрешать значку приложения мигать при требовании внимания"),
				CreateAdditionalToggleRow(taskbarLastActive, "Переключаться на последнее окно", "Щелчок по сгруппированной кнопке открывает последнее активное окно"),
				CreateAdditionalToggleRow(_taskbarShareWindowToggle, "Предоставление доступа к окну", "Показывать команду предоставления доступа к окну в меню панели задач"),
				CreateAdditionalToggleRow(_taskbarEndTaskToggle, "Завершать задачи с панели задач", "Добавить команду завершения приложения в меню панели задач")
			};

			var multiMonitorRows = new List<UIElement>
			{
				CreateAdditionalToggleRow(_taskbarMultiMonitorToggle, "Показывать панель задач на всех дисплеях", "Отображать панели задач на дополнительных мониторах"),
				CreateAdditionalValueRow("Приложения на дополнительных панелях задач", "Выбрать, на каких панелях задач отображать кнопки открытых приложений", _taskbarMultiMonitorModeCombo),
				CreateAdditionalValueRow("Объединение кнопок на других панелях задач", "Правило группировки кнопок на дополнительных мониторах", _taskbarMultiMonitorGlomCombo)
			};

			taskbarPanel.Children.Add(CreateTaskbarSubsectionHeader("Основные"));
			foreach (var row in mainRows)
				taskbarPanel.Children.Add(row);

			taskbarPanel.Children.Add(CreateTaskbarSubsectionHeader("Поведение"));
			foreach (var row in behaviorRows)
				taskbarPanel.Children.Add(row);

			taskbarPanel.Children.Add(CreateTaskbarSubsectionHeader("Несколько дисплеев"));
			foreach (var row in multiMonitorRows)
				taskbarPanel.Children.Add(row);
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
				if (row.Style != (Style)FindResource("SettingRow")) continue;
				if (!(row.Child is Grid grid)) continue;
				var textPanel = grid.Children.OfType<StackPanel>().FirstOrDefault();
				if (textPanel == null) continue;
				if (textPanel.Children.OfType<TextBlock>().Any(x => x.Text == "Для применения требуется перезапуск Проводника либо перезапуск Windows.")) continue;
				textPanel.Children.Add(new TextBlock
				{
					Text = "Для применения требуется перезапуск Проводника либо перезапуск Windows.",
					Foreground = new SolidColorBrush(Color.FromRgb(105, 137, 176)),
					FontSize = 10,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0, 3, 0, 0)
				});
			}
		}

		private static IEnumerable<T> FindVisualElements<T>(DependencyObject root) where T : DependencyObject
		{
			if (root == null) yield break;
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				var child = VisualTreeHelper.GetChild(root, i);
				if (child is T match) yield return match;
				foreach (var nested in FindVisualElements<T>(child)) yield return nested;
			}
		}

		private static Border GetSettingRow(ToggleButton toggle)
		{
			return toggle?.Parent is StackPanel controls && controls.Parent is Grid grid && grid.Parent is Border row
				? row
				: null;
		}

		private void AttachMovedSettingsRows()
		{
			if (ExplorerSettingsPanel != null && ExplorerSettingsPanel.Child is StackPanel explorerPanel)
			{
				explorerPanel.Children.Add(CreateAdditionalValueRow("Названия новых файлов и папок", "Шаблон Windows для новых объектов Проводника", CreateNameTemplateControls()));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("ExplorerItemCheckboxes"), "Флажки элементов в Проводнике", "Показывать флажки выбора у файлов и папок"));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("ExplorerSyncNotifications"), "Уведомления поставщиков синхронизации", "Показывать уведомления OneDrive и других поставщиков файлов"));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("ExplorerCompactMode"), "Компактный режим Проводника", "Уменьшить интервалы между элементами Проводника"));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("SnapAssistFlyout"), "Подсказки Snap", "Показывать раскладку Snap при наведении на кнопку разворачивания"));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("ClassicContextMenu"), "Классическое контекстное меню", "Использовать классическое меню Windows 10 вместо компактного меню Windows 11"));
				explorerPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("SpeedUpExplorerAndMenus"), "Ускорить Проводник и меню", "Уменьшить задержку запуска Проводника и отображения меню Windows"));
			}
			if (DesktopSettingsPanel != null && DesktopSettingsPanel.Child is StackPanel desktopPanel)
			{
				desktopPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("HideUserFiles"), "Скрыть файлы пользователя", "Скрыть папку пользователя на рабочем столе"));
				desktopPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("HideNetworkIcon"), "Скрыть сеть", "Скрыть значок «Сеть» на рабочем столе"));
				desktopPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("HideControlPanel"), "Скрыть панель управления", "Скрыть классический значок панели управления на рабочем столе"));
				desktopPanel.Children.Add(CreateAdditionalToggleRow(CreateManagedToggle("ShortcutArrow"), "Скрывать стрелки ярлыков", "Убирать стрелку с ярлыков рабочего стола"));
				desktopPanel.Children.Add(CreateAdditionalValueRow("Цвет выделения", "Цвет выделения текста и элементов интерфейса Windows", CreateHighlightColorControls()));
				desktopPanel.Children.Add(CreateAdditionalToggleRow(_contextMenuDelayToggle, "Убрать задержку контекстного меню", "Установить минимальную задержку открытия меню"));
			}
		}


		private void CreateInterfaceSettings()
		{
			_taskbarEndTaskToggle = CreateAdditionalToggle("EnableTaskbarEndTask");
			_taskbarAutoHideToggle = CreateAdditionalToggle("TaskbarAutoHide");
			_taskbarBadgesToggle = CreateAdditionalToggle("TaskbarBadges");
			_taskbarFlashingToggle = CreateAdditionalToggle("TaskbarFlashing");
			_taskbarMultiMonitorToggle = CreateAdditionalToggle("TaskbarMultiMonitor");
			_taskbarShareWindowToggle = CreateAdditionalToggle("TaskbarShareWindow");
			_taskbarShowDesktopToggle = CreateAdditionalToggle("TaskbarShowDesktop");

			_taskbarAlignmentCombo = CreateTaskbarCombo("TaskbarAlignment", new[] { "Слева", "По центру" }, 230);
			_taskbarMultiMonitorModeCombo = CreateTaskbarCombo("TaskbarMultiMonitorMode", new[] { "Все панели задач", "Основная и панель с открытым окном", "Только панель с открытым окном" }, 285);
			_taskbarGlomCombo = CreateTaskbarCombo("TaskbarGlomLevel", new[] { "Всегда", "Когда панель задач заполнена", "Никогда" }, 260);
			_taskbarMultiMonitorGlomCombo = CreateTaskbarCombo("TaskbarMultiMonitorGlomLevel", new[] { "Всегда", "Когда панель задач заполнена", "Никогда" }, 260);
			_disableLockScreenBlurToggle = CreateAdditionalToggle("DisableLockScreenBlur");
			_darkThemeToggle = CreateAdditionalToggle("EnableDarkTheme");
			_contextMenuDelayToggle = CreateAdditionalToggle("ReduceContextMenuDelay");
			_clipboardToggle = CreateAdditionalToggle("EnableClipboard");
			_windowsAdsToggle = CreateAdditionalToggle("DisableWindowsAds");

			// Use the controls declared in XAML. The previous implementation created
			// detached mouse controls here, so dragging the visible sliders could
			// write the values from the hidden controls instead of the UI values.
			_mouseSpeedSlider = MouseSpeedSlider;
			_mouseScrollSlider = MouseScrollSlider;
			_mouseAccelerationToggle = MouseAccelerationToggle;
			_mouseSpeedSlider.IsSnapToTickEnabled = false;
			_mouseScrollSlider.IsSnapToTickEnabled = false;
			_mouseSpeedSlider.SmallChange = 1;
			_mouseSpeedSlider.LargeChange = 1;
			_mouseScrollSlider.SmallChange = 1;
			_mouseScrollSlider.LargeChange = 1;
			_mouseAccelerationToggle.Checked += MouseAccelerationToggle_Changed;
			_mouseAccelerationToggle.Unchecked += MouseAccelerationToggle_Changed;

			_highlightColorCombo = new ComboBox
			{
				Width = 0,
				MinWidth = 0,
				Style = (Style)FindResource("DarkComboBoxStyle"),
				ItemContainerStyle = (Style)FindResource("DarkComboBoxItemStyle")
			};
			_highlightColorCombo.Items.Add("Синий");
			_highlightColorCombo.Items.Add("Бирюзовый");
			_highlightColorCombo.Items.Add("Фиолетовый");
			_highlightColorCombo.Items.Add("Зелёный");
			_highlightColorCombo.Items.Add("Оранжевый");
			_highlightColorCombo.Items.Add("Красный");
			_highlightColorCombo.Items.Add("Тёмно-синий");
			_highlightColorCombo.Items.Add("Черный");
			_highlightColorCombo.Items.Add("Серый");
			_highlightColorCombo.Width = CalculateTightComboBoxWidth(_highlightColorCombo.Items.Cast<object>().Select(x => x?.ToString() ?? string.Empty));

			_highlightColorApplyButton = new Button
			{
				Content = "Применить",
				Style = (Style)FindResource("GhostButton"),
				Padding = new Thickness(10, 5, 10, 5),
				Margin = new Thickness(8, 0, 0, 0)
			};
			_highlightColorApplyButton.Click += (sender, args) =>
			{
				try { ApplyHighlightColor(); }
				catch (Exception exception) { ShowToast("Не удалось применить цвет выделения: " + exception.Message, true); }
			};

			_defaultNameTemplateBox = new TextBox
			{
				Width = 180,
				Padding = new Thickness(8, 5, 8, 5),
				Text = ReadUserString(NamingTemplatesPath, "RenameNameTemplate", string.Empty),
				Background = new SolidColorBrush(Color.FromRgb(16, 38, 63)),
				Foreground = new SolidColorBrush(Color.FromRgb(241, 246, 255)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(47, 92, 140)),
				BorderThickness = new Thickness(1)
			};

			if (PowerSchemeComboBox != null)
			{
				PowerSchemeComboBox.Items.Add("Сбалансированная");
				PowerSchemeComboBox.Items.Add("Высокая производительность");
				PowerSchemeComboBox.Items.Add("Экономия энергии");
				PowerSchemeComboBox.Width = CalculateTightComboBoxWidth(PowerSchemeComboBox.Items.Cast<object>().Select(x => x?.ToString() ?? string.Empty)) + 18;
				PowerSchemeComboBox.SelectedIndex = 0;
			}
		}

		private Border CreatePowerShellScriptsRow()
		{
			_powerShellScriptsToggle = CreateAdditionalToggle("PowerShellScripts");
			_powerShellScriptsToggle.Checked -= WindowsFeatureToggle_Changed;
			_powerShellScriptsToggle.Unchecked -= WindowsFeatureToggle_Changed;
			_powerShellScriptsToggle.Checked += PowerShellScriptsToggle_Changed;
			_powerShellScriptsToggle.Unchecked += PowerShellScriptsToggle_Changed;
			var row = CreateAdditionalToggleRow(_powerShellScriptsToggle, "Разрешить сторонние скрипты PowerShell", "Изменяет ExecutionPolicy для текущего пользователя Windows; требуются права администратора");
			_powerShellScriptsLabel = _additionalStatusLabels["PowerShellScripts"];
			return row;
		}

		private Border CreateAdditionalValueRow(string title, string description, UIElement valueControl)
		{
			var row = new Border { Style = (Style)FindResource("SettingRow") };
			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
			text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
			text.Children.Add(new TextBlock { Text = description, Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)), FontSize = 11 });
			if (ReferenceEquals(valueControl, _highlightColorCombo) && _highlightColorApplyButton != null)
			{
				var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
				controls.Children.Add(_highlightColorCombo);
				controls.Children.Add(_highlightColorApplyButton);
				valueControl = controls;
			}
			Grid.SetColumn(text, 0);
			Grid.SetColumn(valueControl, 1);
			grid.Children.Add(text);
			grid.Children.Add(valueControl);
			row.Child = grid;
			return row;
		}

		private static int? ReadUserDwordOptional(string path, string name)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path))
			{
				var value = key?.GetValue(name);
				return value == null ? null : Convert.ToInt32(value);
			}
		}

		private static bool AreTelemetrySettingsDisabled()
		{
			return ReadMachineDword(DataCollectionPolicyPath, "AllowTelemetry", 1) == 0
				&& ReadMachineDword(DataCollectionPolicyPath, "MaxTelemetryAllowed", 1) == 0
				&& ReadMachineDword(DataCollectionPolicyPath, "AllowDeviceNameInDiagnosticData", 1) == 0
				&& ReadMachineDword(DataCollectionPolicyPath, "AllowWAPPReports", 1) == 0
				&& ReadMachineDword(DataCollectionPolicyPath, "DoNotShowFeedbackNotifications", 0) == 1
				&& ReadMachineDword(DataCollectionPolicyPath, "DisableDiagnosticDataViewer", 0) == 1
				&& ReadMachineDword(AppCompatPolicyPath, "AITEnable", 1) == 0
				&& ReadMachineDword(AppCompatPolicyPath, "AllowTelemetry", 1) == 0
				&& ReadMachineDword(AppCompatPolicyPath, "DisableEngine", 0) == 1
				&& ReadMachineDword(AppCompatPolicyPath, "DisableInventory", 0) == 1
				&& ReadMachineDword(AppCompatPolicyPath, "DisablePCA", 0) == 1
				&& ReadMachineDword(AppCompatPolicyPath, "DisableUAR", 0) == 1;
		}

		private async void LoadExplorerSettings()
		{
			_loadingExplorerSettings = true;
			try
			{
				SetToggle(ShowHiddenFilesToggle, ReadDword(ExplorerAdvancedPath, "Hidden", 2) == 1);
				SetToggle(ShowFileExtensionsToggle, ReadDword(ExplorerAdvancedPath, "HideFileExt", 1) == 0);
				SetToggle(OpenThisPcToggle, _explorerSettings.IsLaunchToThisPc());
				SetToggle(ExplorerHomeToggle, !_explorerSettings.IsHomeVisible());
				SetToggle(ShowRecentFilesToggle, !IsRecentFilesEnabled());
				SetToggle(ShowFrequentFoldersToggle, !IsFrequentFoldersEnabled());
				SetToggle(ShowGalleryToggle, !IsGalleryVisible());
				SetToggle(RemoveShortcutSuffixToggle, ReadString(NamingTemplatesPath, "ShortcutNameTemplate", null) == "%s");
				SetToggle(ShowThisPcToggle, ReadDword(HideDesktopIconsPath, ThisPcId, 1) != 0);
				SetToggle(ShowRecycleBinToggle, ReadDword(HideDesktopIconsPath, RecycleBinId, 1) != 0);
				SetToggle(ShowSecondsInSystemClockToggle, ReadDword(ExplorerAdvancedPath, "ShowSecondsInSystemClock", 0) == 1);
				SetToggle(HideNetworkToggle, ReadDword(NetworkPath, "System.IsPinnedToNameSpaceTree", 1) == 0);
				SetToggle(HideDownloadsToggle, !IsNamespaceItemVisible(DownloadsId));
				SetToggle(HideDocumentsToggle, !IsNamespaceItemVisible(DocumentsId));
				SetToggle(HideVideosToggle, !IsNamespaceItemVisible(VideosId));
				SetToggle(HidePicturesToggle, !IsNamespaceItemVisible(PicturesId));
				SetToggle(HideMusicToggle, !IsNamespaceItemVisible(MusicId));
				SetToggle(HideDesktopToggle, !IsNamespaceItemVisible(DesktopId));
				SetToggle(DisableWindowsUpdateToggle, ReadMachineDword(WindowsUpdatePolicyPath, "NoAutoUpdate", 0) == 1);
				SetToggle(DisableDriverUpdatesToggle, ReadMachineDword(WindowsUpdateDriverPath, "ExcludeWUDriversInQualityUpdate", 0) == 1);
				SetToggle(DisableReservedStorageToggle, ReadMachineDword(ReserveManagerPath, "ShippedWithReserves", 1) == 0);
				DisableReservedStorageToggle.IsEnabled = IsReservedStorageSupported();
				if (!DisableReservedStorageToggle.IsEnabled) DisableReservedStorageLabel.Text = "Не поддерживается";
				UpdatePauseStatus();
				SetToggle(DisableTelemetryToggle, _privacySettings.IsDisabled("DisableTelemetry"));
				SetToggle(DisableAppDiagnosticsToggle, _privacySettings.IsDisabled("DisableAppDiagnostics"));
				SetToggle(DisableActivityToggle, _privacySettings.IsDisabled("DisableActivity"));
				SetToggle(DisablePerformanceToggle, _privacySettings.IsDisabled("DisablePerformance"));
				SetToggle(DisableKeystrokesToggle, _privacySettings.IsDisabled("DisableKeystrokes"));
				SetToggle(DisableVoiceDataToggle, _privacySettings.IsDisabled("DisableVoiceData"));
				SetToggle(DisableStickyKeysToggle, AreStickyKeysDisabled());
				SetToggle(DisableBingSearchToggle, IsBingSearchDisabled());
				SetToggle(DisableHibernationToggle, _powerSettings.IsHibernationDisabled());
				SetToggle(DisableSmartScreenToggle,
					ReadMachineDword(SystemPolicyPath, "EnableSmartScreen", 1) == 0 ||
					ReadString(ExplorerPolicyPath, "SmartScreenEnabled", "Warn").Equals("Off", StringComparison.OrdinalIgnoreCase));
				SetToggle(DisableMemoryIntegrityToggle,
					ReadMachineDword(HvcISettingsPath, "Enabled", 1) == 0);
				SetToggle(_disableVbsToggle, ReadMachineDword(DeviceGuardPath, "EnableVirtualizationBasedSecurity", 1) == 0);
				var uacNeverNotify = IsUacNeverNotifyEnabled();
				SetToggle(DisableUacToggle, uacNeverNotify);
				SetStatusLabel(DisableUacLabel, uacNeverNotify ? "Включено" : "Отключено", uacNeverNotify);
				SetToggle(DisablePageFileToggle, IsPageFileDisabled());
				SetToggle(DisableBitLockerAutoEncryptionToggle, ReadMachineDword(BitLockerPath, "PreventDeviceEncryption", 0) == 1);
				SetManagedToggleState("HideUserFiles");
				SetManagedToggleState("HideNetworkIcon");
				SetManagedToggleState("HideControlPanel");
				SetManagedToggleState("ShortcutArrow");
				SetManagedToggleState("ClassicContextMenu");
				SetManagedToggleState("ExplorerItemCheckboxes");
				SetManagedToggleState("ExplorerSyncNotifications");
				SetManagedToggleState("SystemSuggestions");
				SetManagedToggleState("ExplorerCompactMode");
				SetManagedToggleState("SnapAssistFlyout");
				SetManagedToggleState("GameBar");
				SetManagedToggleState("FullscreenOptimizations");
				SetManagedToggleState("DeveloperMode");
				SetManagedToggleState("LongPathsEnabled");
				SetManagedToggleState("NumLockOnBoot");
				SetManagedToggleState("SpeedUpExplorerAndMenus");
				SetManagedToggleState("DisableStartMenuWebSearch");
				SetManagedToggleState("DisableStartRecommended");
				SetManagedToggleState("DisableSettings365Ads");
				SetManagedToggleState("DisablePreinstalledApps");
				SetManagedToggleState("DisableErrorReporting");
				SetManagedToggleState("DisableAdvertisingAndSuggestions");
				SetManagedToggleState("DisableActivityHistory");
				SetManagedToggleState("DisableLocationAndSensors");
				if (!IsAdministrator())
					SetAdditionalStatusLabel("DisableLocationAndSensors", "Требуются права администратора", false);
				SetManagedToggleState("DisableCortana");
				SetManagedToggleState("DisableCopilot");
				SetManagedToggleState("DisableContentDeliveryManager");
				SetManagedToggleState("DisableFindMyDevice");
				SetManagedToggleState("DisableDeliveryOptimization");
				SetManagedToggle("DisableSystemThrottling", _powerSettings.IsSystemPowerThrottlingDisabled());
				SetManagedToggle("DisableUSBPowerSaving", await _powerSettings.IsUsbPowerSavingDisabledAsync(CancellationToken.None));
				var taskbar = _taskbarSettings.ReadState();
				SetToggle(_taskbarEndTaskToggle, taskbar.EndTask);
				SetToggle(_taskbarAutoHideToggle, taskbar.AutoHide);
				SetToggle(_taskbarBadgesToggle, taskbar.Badges);
				SetToggle(_taskbarFlashingToggle, taskbar.Flashing);
				SetToggle(_taskbarMultiMonitorToggle, taskbar.MultiMonitor);
				SetToggle(_taskbarShareWindowToggle, taskbar.ShareWindow);
				SetToggle(_taskbarShowDesktopToggle, taskbar.ShowDesktop);
				SetManagedToggle("TaskbarWidgets", taskbar.Widgets);
				SetManagedToggle("TaskbarTaskViewButton", taskbar.TaskViewButton);
				SetManagedToggle("TaskbarLastActiveClick", taskbar.LastActiveClick);
				SetManagedComboIndex("SearchBoxTaskbarMode", taskbar.SearchBoxTaskbarMode, 3);

				SetTaskbarComboSelection(_taskbarAlignmentCombo, taskbar.Alignment.ToString(CultureInfo.InvariantCulture), 2);
				SetTaskbarComboSelection(_taskbarMultiMonitorModeCombo, taskbar.MultiMonitorMode.ToString(CultureInfo.InvariantCulture), 3);
				SetTaskbarComboSelection(_taskbarGlomCombo, taskbar.GroupingMode.ToString(CultureInfo.InvariantCulture), 3);
				SetTaskbarComboSelection(_taskbarMultiMonitorGlomCombo, taskbar.MultiMonitorGroupingMode.ToString(CultureInfo.InvariantCulture), 3);
				SetToggle(_disableLockScreenBlurToggle, ReadMachineDword(LockScreenPolicyPath, "DisableAcrylicBackgroundOnLogon", 0) == 1);
				SetToggle(_darkThemeToggle,
					ReadDword(ThemePersonalizePath, "AppsUseLightTheme", 1) == 0 &&
					ReadDword(ThemePersonalizePath, "SystemUsesLightTheme", 1) == 0);
				SetToggle(_contextMenuDelayToggle, ReadUserString(DesktopSettingsPath, "MenuShowDelay", "400") == "50");
				SetToggle(_clipboardToggle, ReadDword(ClipboardPath, "EnableClipboardHistory", 0) == 1);
				SetToggle(_windowsAdsToggle, IsWindowsAdsDisabled());
				if (_gameModeToggle != null) SetToggle(_gameModeToggle, IsGameModeEnabled());
				if (_hagsToggle != null)
				{
					var hagsSupported = _securitySettings.IsHardwareGpuSchedulingSupported();
					_hagsToggle.IsEnabled = hagsSupported && IsAdministrator();
					SetToggle(_hagsToggle, hagsSupported && _securitySettings.IsHardwareGpuSchedulingEnabled());
				}
				if (PowerSchemeComboBox != null && !PowerSchemeComboBox.IsDropDownOpen && !PowerSchemeComboBox.IsKeyboardFocusWithin)
				{
					var guid = await _powerSettings.GetActiveSchemeGuidAsync(CancellationToken.None);
					PowerSchemeComboBox.SelectedIndex = PowerSettingsService.GetSchemeIndex(guid);
				}
				if (!_highlightColorCombo.IsKeyboardFocusWithin) _highlightColorCombo.SelectedIndex = ReadHighlightColorIndex();
				if (!_defaultNameTemplateBox.IsKeyboardFocusWithin) _defaultNameTemplateBox.Text = ReadUserString(NamingTemplatesPath, "RenameNameTemplate", string.Empty);
			}
			finally
			{
				_loadingExplorerSettings = false;
			}
			ApplyAdminToggleLockState();
			EnsureAdminWarnings();
		}

		private void EnsureAdminWarnings()
		{
			UpdateCategoryAdminHints();
			var warningBlocks = SettingsStack.Children.OfType<Border>()
				.SelectMany(border => (border.Child as StackPanel)?.Children.OfType<Border>() ?? Enumerable.Empty<Border>())
				.Where(border => border.Tag is string tag && tag == "AdminWarning")
				.ToList();
			foreach (var warning in warningBlocks)
			{
				warning.Visibility = IsAdministrator() ? Visibility.Collapsed : Visibility.Visible;
			}
			if (IsAdministrator()) return;

			foreach (var section in SettingsStack.Children.OfType<Border>())
			{
				var content = section.Child as StackPanel;
				if (content == null) continue;

				var containsAdminOnlySetting = content.Children.OfType<FrameworkElement>()
					.Any(element => element is ToggleButton toggle && RequiresAdministratorAccess(toggle.Tag as string));
				if (!containsAdminOnlySetting) continue;

				var headerIndex = -1;
				for (var i = 0; i < content.Children.Count; i++)
				{
					if (content.Children[i] is TextBlock direct && !string.IsNullOrWhiteSpace(direct.Text))
					{
						headerIndex = i;
						break;
					}
					if (content.Children[i] is Border headerBorder && headerBorder.Child is TextBlock headerText && !string.IsNullOrWhiteSpace(headerText.Text))
					{
						headerIndex = i;
						break;
					}
				}
				if (headerIndex < 0) continue;
				if (content.Children.OfType<Border>().Any(x => x.Tag is string tag && tag == "AdminWarning")) continue;

				var warning = new Border
				{
					Tag = "AdminWarning",
					Background = new SolidColorBrush(Color.FromRgb(34, 16, 16)),
					BorderBrush = new SolidColorBrush(Color.FromRgb(199, 74, 74)),
					BorderThickness = new Thickness(1),
					CornerRadius = new CornerRadius(8),
					Padding = new Thickness(12, 8, 12, 8),
					Margin = new Thickness(16, 0, 16, 10),
					Child = new TextBlock
					{
						Text = "Для некоторых настроек потребуются права администратора.",
						Foreground = new SolidColorBrush(Color.FromRgb(255, 206, 206)),
						FontSize = 11,
						FontWeight = FontWeights.SemiBold,
						TextWrapping = TextWrapping.Wrap
					}
				};
				content.Children.Insert(headerIndex + 1, warning);
			}
		}

		private void UpdateCategoryAdminHints()
		{
			const string hint = "Для некоторых настроек требуются права администратора";
			var visibility = IsAdministrator() ? Visibility.Collapsed : Visibility.Visible;
			foreach (var textBlock in FindTextBlocks(SettingsStack))
			{
				if (string.Equals(textBlock.Text, hint, StringComparison.Ordinal))
					textBlock.Visibility = visibility;
			}
		}

		private static IEnumerable<TextBlock> FindTextBlocks(DependencyObject root)
		{
			if (root == null) yield break;
			if (root is TextBlock textBlock) yield return textBlock;

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				foreach (var nested in FindTextBlocks(VisualTreeHelper.GetChild(root, i)))
					yield return nested;
			}
		}

				private void ApplyAdminToggleLockState()
		{
			var isAdmin = IsAdministrator();
			foreach (var toggle in GetAllToggleButtons())
			{
				var tag = toggle.Tag as string;

				if (tag == "GameBar" && !_gameBarAvailable)
				{
					toggle.IsEnabled = false;
					toggle.IsHitTestVisible = false;
					toggle.Opacity = 0.5;
					toggle.Cursor = Cursors.Arrow;
					SetAdditionalStatusLabel("GameBar", "Недоступно: Xbox Game Bar не установлен", false);
					continue;
				}

				if ((tag == "DisableCortana" || tag == "DisableCopilot") && !_privacySettings.IsFeatureSupported(tag))
				{
					toggle.IsEnabled = false;
					toggle.IsHitTestVisible = false;
					toggle.Opacity = 0.5;
					toggle.Cursor = Cursors.Arrow;
					SetAdditionalStatusLabel(
						tag,
						tag == "DisableCortana" ? "Недоступно: только Windows 10" : "Недоступно: только Windows 11",
						false);
					continue;
				}

				if (!RequiresAdministratorAccess(tag)) continue;
				var allowed = isAdmin && !(tag == "PowerShellScripts" && _powerShellScriptsBusy);
				toggle.IsEnabled = allowed;
				toggle.IsHitTestVisible = allowed;
				toggle.Opacity = isAdmin ? 1.0 : 0.55;
				toggle.Cursor = isAdmin ? Cursors.Hand : Cursors.Arrow;
			}
			ApplyPowerShellScriptsAdminState();
		}

		private IEnumerable<ToggleButton> GetAllToggleButtons()
		{
			return GetAllToggleButtons(this);
		}

		private static IEnumerable<ToggleButton> GetAllToggleButtons(DependencyObject root)
		{
			if (root == null) yield break;
			if (root is ToggleButton toggle) yield return toggle;

			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				foreach (var nested in GetAllToggleButtons(VisualTreeHelper.GetChild(root, i)))
					yield return nested;
			}
		}

		private void SetManagedToggle(string tag, bool value)
		{
			if (_managedToggles.TryGetValue(tag, out var toggle))
				SetToggle(toggle, value);
		}

		private void SetManagedToggleState(string tag)
		{
			if (_managedToggles.TryGetValue(tag, out var toggle))
			{
				bool value;
				if (tag == "DisableSystemThrottling")
					value = _powerSettings.IsSystemPowerThrottlingDisabled();
				else if (tag == "DisableUSBPowerSaving")
					value = false;
				else if (tag == "DisableErrorReporting"
					|| tag == "DisableAdvertisingAndSuggestions"
					|| tag == "DisableNewsAndInterests"
					|| tag == "DisableActivityHistory"
					|| tag == "HideMeetNowButton"
					|| tag == "DisableLocationAndSensors"
					|| tag == "DisableAutoLogger"
					|| tag == "DisableCortana"
					|| tag == "DisableCopilot"
					|| tag == "DisableContentDeliveryManager"
					|| tag == "DisableFindMyDevice"
					|| tag == "DisableDeliveryOptimization")
					value = _privacySettings.IsDisabled(tag);
				else
					value = _interfaceSettings.IsEnabled(tag);
				SetToggle(toggle, value);
			}
		}

		private void SetManagedComboIndex(string tag, int value, int fallback)
		{
			if (!_managedCombos.TryGetValue(tag, out var combo) || combo.IsKeyboardFocusWithin)
				return;
			if (value >= 0 && value < combo.Items.Count) combo.SelectedIndex = value;
			else if (fallback >= 0 && fallback < combo.Items.Count) combo.SelectedIndex = fallback;
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
				|| tag == "ShowUserFiles"
				|| tag == "ShowNetworkIcon"
				|| tag == "ShowControlPanel"
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
