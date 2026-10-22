namespace PandaBear.VaultManager.Tool;

internal static class Program
{
    public static int Main(string[] args) => VaultManagerCommand.Create().Parse(args).Invoke();
}
