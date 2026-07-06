namespace NVConso.ViewModels
{
    public sealed class SelectionOption<T>
    {
        public SelectionOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public T Value { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}
