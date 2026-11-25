using System.CommandLine;
using SharedKernel.VaultManager.Tool;

RootCommand rootCommand = VaultCommandLine.Create(VaultCommandEnvironment.FromConsole());
return await VaultCommandLine.Invoke(rootCommand, args, new InvocationConfiguration(), CancellationToken.None).ConfigureAwait(false);
