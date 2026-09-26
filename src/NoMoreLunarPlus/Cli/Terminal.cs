using System.Text;
using NoMoreLunarPlus.Patching;

namespace NoMoreLunarPlus.Cli;

internal static class Terminal
{
    private const int LabelWidth = 10;
    private const int ResultWidth = 44;

    public static void Banner()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = $"{AppInfo.Name} v{AppInfo.Version}";

        Console.WriteLine();
        Write($"  {AppInfo.Name}", ConsoleColor.Magenta);
        WriteLine($"  v{AppInfo.Version}", ConsoleColor.DarkGray);
        WriteLine("  " + new string('─', 52), ConsoleColor.DarkGray);
        Console.WriteLine();
    }

    public static void Field(string label, string value, ConsoleColor color = ConsoleColor.White)
    {
        Write($"  {label.PadRight(LabelWidth)}", ConsoleColor.DarkGray);
        WriteLine(value, color);
    }

    public static void Section(string title)
    {
        Console.WriteLine();
        WriteLine($"  {title}", ConsoleColor.White);
    }

    public static void Step(string text)
    {
        Write("  » ", ConsoleColor.Magenta);
        Write($"{text}... ", ConsoleColor.Gray);
    }

    public static void Done(string text = "done") => WriteLine(text, ConsoleColor.Green);

    public static void Note(string text) => WriteLine(text, ConsoleColor.DarkYellow);

    public static void Result(PatchResult result)
    {
        var (mark, status, color) = result.Outcome switch
        {
            PatchOutcome.Applied => ("+", "applied", ConsoleColor.Green),
            PatchOutcome.Ambiguous => ("!", $"skipped, {result.Matches} matches", ConsoleColor.Yellow),
            _ => ("-", "not found", ConsoleColor.DarkGray)
        };

        Write($"    [{mark}] ", color);
        Write(result.Patch.Title.PadRight(ResultWidth, ' '), ConsoleColor.Gray);
        WriteLine(status, color);
    }

    public static void Success(string text)
    {
        Console.WriteLine();
        WriteLine($"  {text}", ConsoleColor.Green);
    }

    public static void Warning(string text) => WriteLine($"  {text}", ConsoleColor.Yellow);

    public static void Error(string text)
    {
        Console.WriteLine();
        WriteLine($"  {text}", ConsoleColor.Red);
    }

    public static string Prompt(string question)
    {
        Console.WriteLine();
        Write($"  {question} ", ConsoleColor.White);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static char Choose(string question, params (char Key, string Label)[] options)
    {
        Console.WriteLine();
        WriteLine($"  {question}", ConsoleColor.White);

        foreach (var (key, label) in options)
        {
            Write($"    [{key}] ", ConsoleColor.Magenta);
            WriteLine(label, ConsoleColor.Gray);
        }

        while (true)
        {
            var key = char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar);

            if (options.Any(option => option.Key == key))
            {
                return key;
            }
        }
    }

    public static T Pick<T>(string question, IReadOnlyList<T> items, Func<T, string> describe)
    {
        Console.WriteLine();
        WriteLine($"  {question}", ConsoleColor.White);

        for (var i = 0; i < items.Count; i++)
        {
            Write($"    [{i + 1}] ", ConsoleColor.Magenta);
            WriteLine(describe(items[i]), ConsoleColor.Gray);
        }

        while (true)
        {
            var answer = Prompt($"Choose 1-{items.Count}:");

            if (int.TryParse(answer, out var index) && index >= 1 && index <= items.Count)
            {
                return items[index - 1];
            }
        }
    }

    public static void Pause()
    {
        Console.WriteLine();
        WriteLine("  Press any key to exit.", ConsoleColor.DarkGray);
        Console.ReadKey(intercept: true);
    }

    private static void Write(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        Write(text, color);
        Console.WriteLine();
    }
}
