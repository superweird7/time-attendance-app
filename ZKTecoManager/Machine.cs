using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZKTecoManager
{
    public class Machine : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string MachineAlias { get; set; }
        public string IpAddress { get; set; }
        public string SerialNumber { get; set; }
        public string Location { get; set; }  // Device group/location/building

        // New properties for real-time status
        private string _status = "Checking...";
        private int _userCount = 0;
        private int _fingerprintCount = 0;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public int UserCount
        {
            get => _userCount;
            set { _userCount = value; OnPropertyChanged(); }
        }

        public int FingerprintCount
        {
            get => _fingerprintCount;
            set { _fingerprintCount = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}