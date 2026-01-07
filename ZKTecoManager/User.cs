using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ZKTecoManager
{
    public class User : INotifyPropertyChanged
    {
        public int UserId { get; set; }
        public string BadgeNumber { get; set; }
        public string Name { get; set; }
        public int DefaultDeptId { get; set; }
        public string Departments { get; set; }
        public int? ShiftId { get; set; }
        public string ShiftName { get; set; }

        // NEW PROPERTIES FOR LOGIN SYSTEM
        public string Password { get; set; }
        public string Role { get; set; }

        public bool CanEditTimes { get; set; }

        // System access type: "full_access" or "leave_only"
        public string SystemAccessType { get; set; }


        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

