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
using Nexora.Infrastructure.WindowsApis;

namespace Nexora.Pages
{
	public partial class WindowsSettingsPage : UserControl
	{
		private const string SearchRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search";
		private const string SearchRegistryValue = "BingSearchEnabled";
		private const string CortanaConsentValue = "CortanaConsent";
		private const string ConnectedSearchUseWebValue = "ConnectedSearchUseWeb";
		private const string ExplorerPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
		private const string ExplorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
		private const string SystemPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
		private const string ExplorerPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
		private const string AttachmentsPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Attachments";
		private const string SearchPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Search";
		private const string SearchExplorerPolicyPath = @"Software\Policies\Microsoft\Windows\Explorer";
		private const string UserWindowsPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
		private const string ExplorerMachinePolicyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
		private const string ThemePersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
		private const string LockScreenPolicyPath = @"Software\Policies\Microsoft\Windows\System";
		private const string DesktopSettingsPath = @"Control Panel\Desktop";
		private const string ClipboardPath = @"Software\Microsoft\Clipboard";
		private const string ContentDeliveryManagerPath = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
		private const string ColorsPath = @"Control Panel\Colors";
		private const string HideDesktopIconsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
		private const string GalleryPath = @"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
		private const string GalleryDesktopNamespacePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\";
		private const string Wow6432GalleryDesktopNamespacePath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\";
		private const string NetworkPath = @"Software\Classes\CLSID\{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";
		private const string KnownFoldersPath = @"Software\Classes\CLSID\";
		private const string FolderDescriptionsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions\";
		private const string Wow6432FolderDescriptionsPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions\";
		private const string MyComputerNameSpacePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\";
		private const string Wow6432MyComputerNameSpacePath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\";
		private const string ThisPcId = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
		private const string RecycleBinId = "{645FF040-5081-101B-9F08-00AA002F954E}";
		private const string NetworkId = "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";
		private const string DownloadsId = "{088e3905-0323-4b02-9826-5d99428e115f}";
		private const string DocumentsId = "{d3162b92-9365-467a-956b-92703aca08af}";
		private const string VideosId = "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}";
		private const string PicturesId = "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}";
		private const string MusicId = "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}";
		private const string DesktopId = "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}";
		private const string GalleryId = "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
		private static readonly IReadOnlyDictionary<string, string> FolderDescriptionIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[DownloadsId] = "{7d83ee9b-2244-4e70-b1f5-5393042af1e4}",
			[DocumentsId] = "{f42ee2d3-909f-4907-8871-4c22fc0bf756}",
			[VideosId] = "{35286a68-3c57-41a1-bbb1-0eae73d76c95}",
			[PicturesId] = "{0ddd015d-b06c-45d5-8c4c-f59713854639}",
			[MusicId] = "{a0c69a99-21c8-4671-8703-7934162fcf1d}",
			[DesktopId] = "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"
		};
		private const string NamingTemplatesPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates";
		private const string DefaultFolderNameTemplate = "Новая папка";
		private const string DefaultFileNameTemplate = "Новый файл";
		private static readonly char[] InvalidNamingTemplateCharacters = { '\\', '/', '?', ':', '*', '"', '>', '<', '|' };
		private const string RenameNameTemplateBackupName = "ExplorerRenameNameTemplate";
		private const string RenameFileTemplateBackupName = "ExplorerRenameFileTemplate";
		private const string WindowsUpdatePolicyPath = @"Software\Policies\Microsoft\Windows\WindowsUpdate";
		private const string WindowsUpdateDriverPath = @"Software\Policies\Microsoft\Windows\WindowsUpdate";
		private const string WindowsUpdateSettingsPath = @"Software\Microsoft\WindowsUpdate\UX\Settings";
		private const string ReserveManagerPath = @"Software\Microsoft\Windows\CurrentVersion\ReserveManager";
		private const string WindowsUpdateBackupPath = @"Software\Nexora\WindowsUpdateBackup";
		private const string DataCollectionPolicyPath = @"Software\Policies\Microsoft\Windows\DataCollection";
		private const string LegacyDataCollectionPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
		private const string LegacyDataCollectionPolicy32Path = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
		private const string AppCompatPolicyPath = @"Software\Policies\Microsoft\Windows\AppCompat";
		private const string AppPrivacyPolicyPath = @"Software\Policies\Microsoft\Windows\AppPrivacy";
		private const string AppDiagnosticsConsentPath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics";
		private const string ActivityHistoryPath = @"Software\Policies\Microsoft\Windows\System";
		private const string InputPersonalizationPath = @"Software\Policies\Microsoft\InputPersonalization";
		private const string LegacyInputPersonalizationPath = @"Software\Microsoft\InputPersonalization";
		private const string SpeechPolicyPath = @"Software\Policies\Microsoft\Speech";
		private const string VoiceDataPath = @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy";
		private const string DeviceGuardPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
		private const string GraphicsDriversPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
		private const string HvcIPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios";
		private const string HvcISettingsPath = HvcIPath + @"\HypervisorEnforcedCodeIntegrity";
		private const string PowerPolicyPath = @"SYSTEM\CurrentControlSet\Control\Power";
		private const string MemoryManagementPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
		private const string BitLockerPath = @"SYSTEM\CurrentControlSet\Control\BitLocker";
		private const string AccessibilityStickyKeysPath = @"Control Panel\Accessibility\StickyKeys";
		private const string AccessibilityKeyboardResponsePath = @"Control Panel\Accessibility\Keyboard Response";
		private const string AccessibilityToggleKeysPath = @"Control Panel\Accessibility\ToggleKeys";
		private const string UserSettingsBackupPath = @"Software\Nexora\WindowsSettingsBackup";
		private const string MachineSettingsBackupPath = @"Software\Nexora\WindowsSettingsBackup";
		private static readonly string TelemetryLogPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "Logs", "WindowsSettings.log");
		private bool _loadingExplorerSettings;
		private ToggleButton _disableVbsToggle;
		private TextBlock _disableVbsLabel;
		private ToggleButton _taskbarEndTaskToggle;
		private ToggleButton _taskbarAutoHideToggle;
		private ToggleButton _taskbarBadgesToggle;
		private ToggleButton _taskbarFlashingToggle;
		private ToggleButton _taskbarMultiMonitorToggle;
		private ToggleButton _taskbarShareWindowToggle;
		private ToggleButton _taskbarShowDesktopToggle;
		private ComboBox _taskbarAlignmentCombo;
		private ComboBox _taskbarMultiMonitorModeCombo;
		private ComboBox _taskbarGlomCombo;
		private ComboBox _taskbarMultiMonitorGlomCombo;
		private ToggleButton _gameModeToggle;
		private ToggleButton _hagsToggle;
		private ToggleButton _disableLockScreenBlurToggle;
		private ToggleButton _darkThemeToggle;
		private ToggleButton _contextMenuDelayToggle;
		private ToggleButton _clipboardToggle;
		private ToggleButton _windowsAdsToggle;
		private ToggleButton _powerShellScriptsToggle;
		private TextBlock _powerShellScriptsLabel;
		private Border _startSearchSettingsPanel;
		private Border _gamingSettingsPanel;
		private Border _systemSettingsPanel;
		private Border _securityBehaviorPanel;
		private readonly LibraryInstallationService _libraryInstallation = new LibraryInstallationService();
		private readonly UwpPackageService _uwpPackageService = new UwpPackageService();
		private readonly RegistrySettingsStore _registryStore = new RegistrySettingsStore();
		private bool _gameBarAvailable = true;
		private readonly SettingBackupService _settingBackup;
		private readonly TaskbarSettingsService _taskbarSettings;
		private readonly MouseSettingsService _mouseSettings;
		private readonly PowerSettingsService _powerSettings;
		private readonly WindowsUpdateService _windowsUpdate;
		private readonly SecuritySettingsService _securitySettings;
		private readonly PrivacySettingsService _privacySettings;
		private readonly ExplorerSettingsService _explorerSettings;
		private readonly SystemProcessRunner _processRunner;
		private readonly ExplorerController _explorerController;
		private readonly WindowsInterfaceSettingsService _interfaceSettings;
		private readonly Dictionary<string, ToggleButton> _managedToggles = new Dictionary<string, ToggleButton>(StringComparer.Ordinal);
		private readonly Dictionary<string, ComboBox> _managedCombos = new Dictionary<string, ComboBox>(StringComparer.Ordinal);
		private bool _powerShellScriptsBusy;
		private bool _settingsApplyBusy;
		private ComboBox _highlightColorCombo;
		private Button _highlightColorApplyButton;
		private TextBox _defaultNameTemplateBox;
		private Slider _mouseSpeedSlider;
		private Slider _mouseScrollSlider;
		private ToggleButton _mouseAccelerationToggle;
		private TextBlock _mouseAccelerationLabel;
		private readonly Dictionary<string, TextBlock> _additionalStatusLabels = new Dictionary<string, TextBlock>(StringComparer.Ordinal);
		private readonly DispatcherTimer _toastTimer;
		private Window _hostWindow;
		private readonly MainWindow _main;

		public WindowsSettingsPage(MainWindow main)
		{
			_main = main;
			_settingBackup = new SettingBackupService(_registryStore);
			_taskbarSettings = new TaskbarSettingsService(_registryStore, _settingBackup);
			_mouseSettings = new MouseSettingsService(_registryStore, _settingBackup);
			_processRunner = new SystemProcessRunner();
			_explorerController = new ExplorerController(_processRunner);
			_interfaceSettings = new WindowsInterfaceSettingsService(_registryStore, _settingBackup);
			_powerSettings = new PowerSettingsService(_processRunner, _registryStore, _settingBackup);
			_windowsUpdate = new WindowsUpdateService(_registryStore, _settingBackup, null, _processRunner);
			_securitySettings = new SecuritySettingsService(_registryStore, _settingBackup);
			_privacySettings = new PrivacySettingsService(_registryStore, _settingBackup);
			_explorerSettings = new ExplorerSettingsService(_registryStore, _settingBackup);
			InitializeComponent();
			_toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
			_toastTimer.Tick += ToastTimer_Tick;
			Loaded += WindowsSettingsPage_Loaded;
			Unloaded += WindowsSettingsPage_Unloaded;
			SettingsStack.Children.Remove(WindowsSettingsBanner);
			SettingsStack.Children.Insert(0, WindowsSettingsBanner);
			MoveSecuritySettingsToBottom();
			CreateInterfaceSettings();
			MoveTaskbarSettings();
			AttachMovedSettingsRows();
			MoveAdditionalSettingsToBottom();
			RemoveObsoleteSettingRows();
			EnsureRestartHints();
			LoadExplorerSettings();
			ApplyAdminToggleLockState();
		}


	}
}