param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$SkipTests
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"

Push-Location $repoRoot
try {
    Write-Host "Building NetVsMcp ($Configuration)..."
    & $dotnet build .\NetVsMcp.slnx --configuration $Configuration /p:UseSharedCompilation=false /nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipTests) {
        Write-Host "Testing NetVsMcp ($Configuration)..."
        & $dotnet test .\tests\NetVsMcp.Broker.Tests\NetVsMcp.Broker.Tests.csproj --configuration $Configuration --no-build /nodeReuse:false
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "Build completed ($Configuration)."
}
finally {
    Pop-Location
}
