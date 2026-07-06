using System.Text;

namespace NVConso
{
    public static class WindowsCommandLine
    {
        public static string FormatArguments(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(QuoteArgument));
        }

        public static string FormatExecutableCommand(string executablePath, string arguments)
        {
            string command = QuoteArgument(executablePath);
            return string.IsNullOrWhiteSpace(arguments)
                ? command
                : $"{command} {arguments}";
        }

        public static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            bool requiresQuotes = argument.Any(static character => char.IsWhiteSpace(character) || character == '"');
            if (!requiresQuotes)
                return argument;

            var builder = new StringBuilder();
            builder.Append('"');

            int backslashCount = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', backslashCount * 2 + 1);
                    builder.Append('"');
                    backslashCount = 0;
                    continue;
                }

                builder.Append('\\', backslashCount);
                builder.Append(character);
                backslashCount = 0;
            }

            builder.Append('\\', backslashCount * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}
