using System.ComponentModel;
using System.Windows.Input;

namespace NVConso
{
    public sealed class TrayMenuActionItem : INotifyPropertyChanged
    {
        private string _text;
        private string _detailText;
        private string _icon;
        private string _toolTipText;
        private bool _isEnabled = true;
        private bool _isVisible = true;
        private bool _isChecked;

        public TrayMenuActionItem(string text, string icon = null)
        {
            _text = text ?? string.Empty;
            _icon = icon ?? string.Empty;
            Command = new TrayMenuCommand(Invoke);
        }

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value ?? string.Empty, nameof(Text));
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value ?? string.Empty, nameof(Icon));
        }

        public string DetailText
        {
            get => _detailText;
            set => SetProperty(ref _detailText, value ?? string.Empty, nameof(DetailText));
        }

        public string ToolTipText
        {
            get => _toolTipText;
            set => SetProperty(ref _toolTipText, value, nameof(ToolTipText));
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value, nameof(IsEnabled));
        }

        public bool Enabled
        {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value, nameof(IsVisible)))
                    OnPropertyChanged(nameof(Available));
            }
        }

        public bool Available
        {
            get => IsVisible;
            set => IsVisible = value;
        }

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value, nameof(IsChecked));
        }

        public bool Checked
        {
            get => IsChecked;
            set => IsChecked = value;
        }

        public ICommand Command { get; }

        public event EventHandler Click;
        public event PropertyChangedEventHandler PropertyChanged;

        public void Invoke()
        {
            if (!IsEnabled || !IsVisible)
                return;

            Click?.Invoke(this, EventArgs.Empty);
        }

        private bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class TrayMenuCommand : ICommand
        {
            private readonly Action _execute;

            public TrayMenuCommand(Action execute)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                _execute();
            }
        }
    }
}
