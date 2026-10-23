using PandaBear.VaultManager.Tool;
using PandaBear.VaultManager.Tool.Pipeline;

return await VaultCommandLine.Create(VaultCommandEnvironment.FromConsole()).Parse(args).InvokeAsync().ConfigureAwait(false);
