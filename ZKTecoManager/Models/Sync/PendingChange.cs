using System.ComponentModel;

namespace ZKTecoManager.Models.Sync
{
    /// <summary>
    /// Represents a pending change from a remote location that needs review.
    /// </summary>
    public class PendingChange : INotifyPropertyChanged
    {
        public string TableName { get; set; }
        public string RecordKey { get; set; }
        public string RecordDescription { get; set; }
        public ChangeType ChangeType { get; set; }
        public string RemoteValue { get; set; }
        public string LocalValue { get; set; }
        public object RemoteRecord { get; set; }
        public object LocalRecord { get; set; }

        private bool _isApproved = true;
        public bool IsApproved
        {
            get => _isApproved;
            set
            {
                if (_isApproved != value)
                {
                    _isApproved = value;
                    OnPropertyChanged(nameof(IsApproved));
                }
            }
        }

        public string ChangeTypeDisplay
        {
            get
            {
                switch (ChangeType)
                {
                    case ChangeType.New: return "جديد";
                    case ChangeType.Updated: return "تحديث";
                    case ChangeType.Conflict: return "تعارض";
                    default: return "غير معروف";
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ChangeType
    {
        New,
        Updated,
        Conflict,
        Unchanged
    }
}
