using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ZKTecoManager
{
    public class NodeViewModel : INotifyPropertyChanged
    {
        private bool? _isChecked = false;
        private NodeViewModel _parent;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NodeViewModel(string name)
        {
            Name = name;
            Children = new ObservableCollection<NodeViewModel>();
        }

        public string Name { get; }
        public ObservableCollection<NodeViewModel> Children { get; }
        public NodeViewModel Parent
        {
            get => _parent;
            set => _parent = value;
        }

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;
            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetIsChecked(_isChecked, true, false);
                }
            }

            if (updateParent && _parent != null)
            {
                _parent.VerifyCheckState();
            }

            OnPropertyChanged(nameof(IsChecked));
        }

        void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            SetIsChecked(state, false, true);
        }
    }

    public class DepartmentNodeViewModel : NodeViewModel
    {
        public DepartmentNodeViewModel(string name) : base(name) { }

        public void AddChildren(IEnumerable<User> users)
        {
            foreach (var user in users)
            {
                Children.Add(new EmployeeNodeViewModel(user.UserId, user.Name, this));
            }
        }
    }

    public class EmployeeNodeViewModel : NodeViewModel
    {
        public int UserId { get; }
        public EmployeeNodeViewModel(int userId, string name, NodeViewModel parent) : base(name)
        {
            UserId = userId;
            Parent = parent;
        }
    }
}
