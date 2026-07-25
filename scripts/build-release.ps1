param(
    [switch]$SkipTests
)

& (Join-Path $PSScriptRoot "build.ps1") -Configuration Release -SkipTests:$SkipTests
