namespace P4G.SaveTool.WinUI;

internal static class LaunchArgumentParser
{
    internal static string? GetOpenPath(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        string trimmedArguments = arguments.Trim();
        if (trimmedArguments.Length == 0)
        {
            return null;
        }

        if (trimmedArguments[0] == '"')
        {
            int closingQuoteIndex = trimmedArguments.IndexOf('"', 1);
            if (closingQuoteIndex > 1)
            {
                return trimmedArguments[1..closingQuoteIndex];
            }
        }

        int separatorIndex = trimmedArguments.IndexOfAny([' ', '\t']);
        return separatorIndex < 0
            ? trimmedArguments
            : trimmedArguments[..separatorIndex];
    }

    internal static string? GetOpenPath(string? arguments, string[]? commandLineArguments)
    {
        string? openPath = GetOpenPath(arguments);
        if (openPath is not null)
        {
            return openPath;
        }

        if (commandLineArguments is null || commandLineArguments.Length < 2)
        {
            return null;
        }

        for (int index = 1; index < commandLineArguments.Length; index++)
        {
            string commandLineArgument = commandLineArguments[index];
            if (!string.IsNullOrWhiteSpace(commandLineArgument))
            {
                return commandLineArgument.Trim();
            }
        }

        return null;
    }
}
