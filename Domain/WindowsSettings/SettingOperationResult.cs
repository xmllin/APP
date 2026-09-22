namespace Nexora.Domain.WindowsSettings
{
    public sealed class SettingOperationResult
    {
        public bool Success { get; set; }
        public bool RequiresRestart { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }

        public static SettingOperationResult Ok(string message, bool requiresRestart = false)
        {
            return new SettingOperationResult { Success = true, Message = message, RequiresRestart = requiresRestart };
        }

        public static SettingOperationResult Fail(string error)
        {
            return new SettingOperationResult { Success = false, Error = error };
        }
    }
}
