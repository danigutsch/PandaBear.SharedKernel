using SharedKernel.Testing.CodeFixRunner;

var options = CodeFixRunnerOptions.Parse(args);
if (options is null)
{
    await Console.Error.WriteLineAsync(CodeFixRunnerOptions.Usage).ConfigureAwait(false);
    return 2;
}

var fixedCount = await CodeFixRunEngine.Run(options, Console.Error).ConfigureAwait(false);
await Console.Out.WriteLineAsync($"Fixed {fixedCount} {options.DiagnosticId} diagnostic(s).").ConfigureAwait(false);
return 0;
