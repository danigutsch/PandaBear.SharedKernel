using SharedKernel.Versioning.Tool;

return await VersioningCommandLine.Create(Console.In, TimeProvider.System).Parse(args).InvokeAsync().ConfigureAwait(false);
