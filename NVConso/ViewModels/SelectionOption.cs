namespace NVConso.ViewModels
{
    public sealed class SelectionOption<T>
    {
        public SelectionOption(string label, T value, string iconGlyph = null, string toolTip = null)
        {
            Label = label;
            Value = value;
            IconGlyph = iconGlyph;
            ToolTip = toolTip;
        }

        public string Label { get; }
        public T Value { get; }
        public string IconGlyph { get; }
        public string ToolTip { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}
