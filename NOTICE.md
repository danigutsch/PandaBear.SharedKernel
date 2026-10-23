# Attribution and provenance

Copyright (c) 2025-2026 Dani Giurizatto Utsch. Repository-owned code is provided under the MIT License.

Retain the MIT copyright and permission notice in `LICENSE.txt`. Revision history and retained provenance records document code ownership and development history; documentation updates do not rewrite that history or change license obligations.

NuGet dependencies retain their own licenses and notices. Package and dependency review must preserve applicable obligations; this notice does not replace upstream license terms. Credentials, private traces, local feeds, and customer data are not distributable repository content.

## Third-party material

`tools/SharedKernel.VaultManager.Tool` contains:

- The BIP39 English wordlist (`Secrets/bip39-english.txt`) from [bitcoin/bips](https://github.com/bitcoin/bips/blob/ce1862ac6bcffa1dd20aad858380e51e66e949ea/bip-0039/english.txt)
  (SHA-256 `2f5eed53a4727b4bf8880d8f3f199efc90e58503646d9ff8eff3a2ed3b24dbda`). BIP-0039 authors: Marek Palatinus,
  Pavol Rusnak, Aaron Voisine, Sean Bowe. MIT License.
- Secret detection rules in `Secrets/SecretRuleCatalog.cs` and `Secrets/SecretPatterns.cs`, partly adapted from
  [gitleaks](https://github.com/gitleaks/gitleaks/blob/09242ce9c8a60d9b051fc2d166f9e849b88c7ac0/config/gitleaks.toml)
  (Copyright (c) 2019 Zachary Rice, MIT License) and
  [Betterleaks](https://github.com/betterleaks/betterleaks/blob/c613797a684337a014168063577bb6fa6e3b6296/config/betterleaks.toml)
  (Copyright (c) 2026 Zachary Rice, MIT License), rewritten in .NET regular expression syntax. Each rule records its
  upstream rule ID.
