# Historical extraction tools

This archive records the one-time extraction workflow and its subsequent helper fixes before ongoing automation moved to C#. It is retained for provenance only; current CI and development use the C# tools under `tools/`. Reproducing the original extraction requires the application revision recorded in `../source.json`. Do not use these scripts to validate current development.

`../source.json` records hashes from the original extraction at commit `8014d52`; those hashes identify that historical state, not necessarily every later archived helper version. In particular, `compatibility.py` includes the legitimate pre-C# fixes from `8fb40db` and `b74ac1e`. Commit `4e3eb73` moved that latest helper into this archive without changing it. Preserve the original provenance hashes and use Git history to inspect the corresponding versions.
