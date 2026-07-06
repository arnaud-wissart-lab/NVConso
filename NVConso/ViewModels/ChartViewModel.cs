namespace NVConso.ViewModels
{
    public sealed class ChartViewModel : ObservableObject
    {
        private string _emptyMessage = "Aucune donnée.";
        private string _summary = "--";
        private string _unit;

        public ChartViewModel(string title, string unit, double? fixedMaximumY = null)
        {
            Title = title;
            _unit = unit;
            FixedMaximumY = fixedMaximumY;
        }

        public string Title { get; }
        public double? FixedMaximumY { get; }
        public List<ChartSeriesViewModel> Series { get; } = [];

        public string Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        public string EmptyMessage
        {
            get => _emptyMessage;
            set => SetProperty(ref _emptyMessage, string.IsNullOrWhiteSpace(value) ? "Aucune donnée." : value);
        }

        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, string.IsNullOrWhiteSpace(value) ? "--" : value);
        }

        public bool HasData => Series.Any(series => series.Values.Any(value => value.HasValue));

        public void NotifyDataChanged()
        {
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(Series));
        }
    }
}
