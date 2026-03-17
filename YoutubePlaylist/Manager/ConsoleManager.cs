namespace YoutubePlaylistManager.Cli.Manager;
public class ConsoleManager
{

    public static string ReadInputWithDefault(string defaultValue, string caret = "> ")
    {
        Console.WriteLine();

        List<char> buffer = [.. defaultValue.ToCharArray().Take(Console.WindowWidth - caret.Length - 1)];
        Console.Write(caret);
        Console.Write([.. buffer]);
        Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
        while (keyInfo.Key != ConsoleKey.Enter)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.LeftArrow:
                    Console.SetCursorPosition(Math.Max(Console.CursorLeft - 1, caret.Length), Console.CursorTop);
                    break;
                case ConsoleKey.RightArrow:
                    Console.SetCursorPosition(Math.Min(Console.CursorLeft + 1, caret.Length + buffer.Count), Console.CursorTop);
                    break;
                case ConsoleKey.Home:
                    Console.SetCursorPosition(caret.Length, Console.CursorTop);
                    break;
                case ConsoleKey.End:
                    Console.SetCursorPosition(caret.Length + buffer.Count, Console.CursorTop);
                    break;
                case ConsoleKey.Backspace:
                    if (Console.CursorLeft <= caret.Length)
                    {
                        break;
                    }
                    var cursorColumnAfterBackspace = Math.Max(Console.CursorLeft - 1, caret.Length);
                    buffer.RemoveAt(Console.CursorLeft - caret.Length - 1);
                    RewriteLine(caret, buffer);
                    Console.SetCursorPosition(cursorColumnAfterBackspace, Console.CursorTop);
                    break;
                case ConsoleKey.Delete:
                    if (Console.CursorLeft >= caret.Length + buffer.Count)
                    {
                        break;
                    }
                    var cursorColumnAfterDelete = Console.CursorLeft;
                    buffer.RemoveAt(Console.CursorLeft - caret.Length);
                    RewriteLine(caret, buffer);
                    Console.SetCursorPosition(cursorColumnAfterDelete, Console.CursorTop);
                    break;
                default:
                    var character = keyInfo.KeyChar;
                    if (character < 32) // not printable chars
                        break;
                    var cursorAfterNewChar = Console.CursorLeft + 1;
                    if (cursorAfterNewChar > Console.WindowWidth || caret.Length + buffer.Count >= Console.WindowWidth - 1)
                    {
                        break; // currently only one line of input is supported
                    }
                    buffer.Insert(Console.CursorLeft - caret.Length, character);
                    RewriteLine(caret, buffer);
                    Console.SetCursorPosition(cursorAfterNewChar, Console.CursorTop);
                    break;
            }
            keyInfo = Console.ReadKey(true);
        }
        Console.Write(Environment.NewLine);

        return new string([.. buffer]);
    }

    public static void ShowProgressBarWhileTasksRunning(List<Task> tasks)
    {
        while (!tasks.All(t => t.IsCompleted))
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, Console.CursorTop);

            Thread.Sleep(300);
            for (int i = 0; i < 6; i++)
            {
                Console.Write(":");
                Thread.Sleep(100);
            }
            Thread.Sleep(600);
        }
    }

    public static Task ShowProgressBarWhileTasksRunningAsync(Task[] tasks)
    {
        return Task.Run(() =>
        {
            int counter = 0;

            while (!tasks.All(t => t.IsCompleted))
            {
                Console.Write($"\rLetöltés {new string('.', counter % 4)}   ");
                counter++;

                Thread.Sleep(200);
            }

            Console.WriteLine("\rKész!                         ");
        });
    }

    private static void RewriteLine(string caret, List<char> buffer)
    {
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(caret);
        Console.Write([.. buffer]);
    }
}
