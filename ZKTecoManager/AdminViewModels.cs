using System.ComponentModel;

namespace ZKTecoManager
{
    // A simple class to represent an item with a checkbox
    public class SelectableItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public int Id { get; set; }
        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
