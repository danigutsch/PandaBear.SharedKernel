#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
dotnet restore SharedKernel.slnx --locked-mode
dotnet build SharedKernel.slnx -c Release --no-restore
