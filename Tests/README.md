# Automated tests

Тестовый проект рассчитан на Windows/.NET 8.

Запуск из корня репозитория:

    dotnet test Tests/Nexora.Tests.csproj --configuration Release

Что проверяется автоматически:
- состояния библиотек Missing / Installed / UpdateAvailable / Manual;
- нормализация и сравнение версий;
- проверка HTML/signature загруженных файлов;
- pagination;
- PauseController;
- общий AppIconResolver;
- Known Folder path для Downloads;
- базовый round-trip RegistrySettingsStore для временного HKCU-ключа.

Изменения, требующие реального Windows GUI/integration-теста (UAC, SmartScreen, мышь, UWP, Explorer restart и т. п.), вынесены в GUI-TEST-CHECKLIST.md.
