$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore SharedKernel.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build SharedKernel.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
