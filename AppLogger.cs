using System.Globalization;
using System.Text.RegularExpressions;

namespace SimpleBCDownloader;

public static class AppLogger
{
    private enum Level
    {
        IntermediateSuccess,
        Success,
        Warning,
        Error
    }


    // tried to repoduce ILogger format 

    public static void IntermediateSuccess(string msg, params object[] args)
        => Log(Level.IntermediateSuccess, msg, args);


    public static void Success(string msg, params object[] args)
        => Log(Level.Success, msg, args);


    public static void Warning(string msg, params object[] args)
        => Log(Level.Warning, msg, args);

    public static void Error(string msg, params object[] args)
        => Log(Level.Error, msg, args);


    private static void Log(Level level, string messageTemplate, params object[] args) {
        string message = RenderMessage(messageTemplate, args);
        ConsoleColor oldConsoleColor = Console.ForegroundColor;
        Console.ForegroundColor = GetColor(level);
        Console.Write($"[{level.ToString()}] ");
        Console.WriteLine(message);
        Console.ForegroundColor = oldConsoleColor;
    }

    private static ConsoleColor GetColor(Level level) => level switch {
        Level.IntermediateSuccess => ConsoleColor.Cyan,
        Level.Success => ConsoleColor.Green,
        Level.Warning => ConsoleColor.Yellow,
        Level.Error => ConsoleColor.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };

    private static readonly Regex HoleRegex = new(@"\{[^{}]+\}", RegexOptions.Compiled);
    const string OpenBraceTempPlug = "\uFFF0";
    const string CloseBraceTempPlug = "\uFFF1";

    private static string UnescapeDoubleBraces(string s)
        => s.Replace("{{", "{").Replace("}}", "}");

    private static string RenderMessage(string template, object[] args) {
        if (args is null || args.Length == 0) {
            return UnescapeDoubleBraces(template);
        }


        string str = template
            .Replace("{{", OpenBraceTempPlug)
            .Replace("}}", CloseBraceTempPlug);

        int index = 0;
        str = HoleRegex.Replace(str, _ => {
            var i = index++;
            return "{" + i.ToString(CultureInfo.InvariantCulture) + "}";
        });

        str = str
            .Replace(OpenBraceTempPlug, "{")
            .Replace(CloseBraceTempPlug, "}");

        try {
            return string.Format(CultureInfo.InvariantCulture, str, args);
        }
        catch {
            //if there are more placeholders than args
            return UnescapeDoubleBraces(template);
        }
    }
}