using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp1.Models
{
    public sealed class UwpPackageInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isInstalled;

        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string PackageFullName { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string InstallLocation { get; set; }
        public bool InstalledForCurrentUser { get; set; }
        public bool InstalledForAllUsers { get; set; }
        public bool IsProvisioned { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsInstalled
        {
            get { return _isInstalled; }
            set
            {
                if (_isInstalled == value) return;
                _isInstalled = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
