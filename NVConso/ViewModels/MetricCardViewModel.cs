namespace NVConso.ViewModels
{
    public sealed class MetricCardViewModel : ObservableObject
    {
        private string _value = "--";
        private string _subtitle;
        private string _tooltip;
        private double? _gaugeValue;
        private DashboardMetricState _state = DashboardMetricState.Unknown;

        public MetricCardViewModel(string title, string subtitle = null)
        {
            Title = title;
            _subtitle = subtitle;
        }

        public string Title { get; }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, string.IsNullOrWhiteSpace(value) ? "--" : value);
        }

        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public double? GaugeValue
        {
            get => _gaugeValue;
            set => SetProperty(ref _gaugeValue, value.HasValue ? Math.Clamp(value.Value, 0, 1) : null);
        }

        public DashboardMetricState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public bool HasGauge => GaugeValue.HasValue;

        public void Update(string value, DashboardMetricState state = DashboardMetricState.Normal, double? gaugeValue = null, string subtitle = null)
        {
            Value = value;
            State = state;
            GaugeValue = gaugeValue;
            Subtitle = subtitle;
            OnPropertyChanged(nameof(HasGauge));
        }
    }
}
