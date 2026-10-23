namespace PandaBear.VaultManager.Tool.Tests;

internal static class ConsoleKeys
{
    /// <summary>Returns a key reader that types each line and presses Enter. A backspace character presses Backspace.</summary>
    public static Func<ConsoleKeyInfo> Typing(params string[] lines)
    {
        var queue = new Queue<ConsoleKeyInfo>();
        foreach (var line in lines)
        {
            foreach (var character in line)
            {
                queue.Enqueue(character == '\b'
                    ? new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false)
                    : new ConsoleKeyInfo(character, ConsoleKey.A, false, false, false));
            }

            queue.Enqueue(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        }

        return queue.Dequeue;
    }
}
