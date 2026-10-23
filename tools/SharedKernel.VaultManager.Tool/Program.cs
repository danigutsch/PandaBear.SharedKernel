using System.CommandLine;
using PandaBear.VaultManager.Tool;
using PandaBear.VaultManager.Tool.Pipeline;

RootCommand rootCommand = VaultCommandLine.Create(VaultCommandEnvironment.FromConsole());
return await VaultCommandLine.Invoke(rootCommand, args, new InvocationConfiguration(), CancellationToken.None).ConfigureAwait(false);
