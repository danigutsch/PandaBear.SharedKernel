# Vault export import

`SharedKernel.Vault.Import` detects a Keeper or Bitwarden export by content and calls the corresponding provider.
It works offline and returns the vault and import warnings through the engine's `IVaultExportReader` contract.
The importer depends on the Vault engine and the built-in providers. Hosts using a single provider may reference that provider directly.

Use `FormatDetector.Detect(path)` to classify the input, or `VaultFileReader.CheckInput(path)` to also reject
unsupported files and invalid password-protected envelopes before asking for a password.
`VaultFileReader.Instance.Read(path, passwordProvider)` loads the export and calls the supplied password callback only
for a password-protected file. The callback receives the file name. Consumers own prompting and output.
Read [vault migration](../../../docs/vault-migration.md) for the supported formats and operating limits.

The import project is available as source. Its package follows the vault engine's separate release decision.
