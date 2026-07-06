namespace NVConso.ViewModels
{
    public sealed class ChartSeriesViewModel : ObservableObject
    {
        private IReadOnlyList<double?> _values = [];

        public ChartSeriesViewModel(string name, string color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; }
        public string Color { get; }

        public IReadOnlyList<double?> Values
        {
            get => _values;
            private set => SetProperty(ref _values, value ?? []);
        }

        public void SetValues(IEnumerable<double?> values)
        {
            Values = values?.ToArray() ?? [];
        }
    }
}
