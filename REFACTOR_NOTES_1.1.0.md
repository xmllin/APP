# Refactor 1.1.0 — audit fixes

## Architecture

- `Presentation/` — MainWindow, Pages, ViewModels, Controls.
- `Application/` — navigation, commands, notifications, download infrastructure.
- `Domain/` — application/download/library/wallpaper/Windows settings models.
- `Infrastructure/` — registry, PowerShell, processes, HTTP, file system and Windows API adapters.
- `Services/WindowsSettings/` — Explorer, Taskbar, Privacy, Security, Windows Update, Power and Mouse services.

## Fixed areas

1. Taskbar settings are isolated in `TaskbarSettingsService` and use the current Windows 11 `SystemSettings_*` registry values documented by Microsoft. Writes are verified and return `RequiresRestart`; Explorer is no longer restarted automatically after every taskbar change.
2. UWP discovery now keeps current-user, all-users and provisioned scope separately. Removal is followed by an elevated re-check and a metadata backup is written before removal.
3. Synchronous process waits were removed from the Windows settings page. Process and PowerShell execution goes through async infrastructure. The remaining application-level remover was also converted to async operations.
4. Registry backup stores exact existence, type and value, including the ability to restore a value that was originally absent.
5. `AppRepository` no longer caches a permanently faulted `Task`; it tracks lifecycle state and supports reload/retry.
6. `OfficialPageDownloadProvider` was split into resolver-specific partial files; HTTP download I/O is centralized in `HttpDownloadClient` and reused by library downloads and the main download engine.
7. `apps.json` is the single icon source of truth. The large hard-coded icon map was removed and all 67 catalog entries now resolve to existing files.
8. `WallpaperCacheService` no longer deletes its image cache in the constructor. Cleanup now removes only files not referenced by the current catalog.
9. MainWindow page construction/navigation is delegated to `NavigationService`.
10. `WindowsSettingsPage` code-behind was split into focused partial files, while system changes are delegated to services.
11. Added shared `ViewModelBase`, `RelayCommand`, registry store/backup, process runner, PowerShell runner, HTTP client and Explorer controller.

## Verification performed in this environment

- Confirmed all 67 `apps.json` icon paths resolve to physical files.
- Confirmed no synchronous `WaitForExit(...)` calls remain in the project.
- Confirmed obsolete taskbar helper writes and the old `AppRepository` faulted-task cache are no longer present.
- Confirmed the old monolithic `WindowsSettingsPage.xaml.cs` was split into focused partial files.

A Windows/.NET build was not executed in this environment because the .NET SDK/MSBuild toolchain is not installed here. The project should therefore be compiled and tested on Windows before release.
