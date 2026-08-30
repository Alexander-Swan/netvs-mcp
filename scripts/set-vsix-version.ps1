param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ManifestPath = "src/NetVsMcp.Vsix/source.extension.vsixmanifest",

    [string]$VsixPath
)

$ErrorActionPreference = "Stop"

$parsedVersion = $null
if (-not [version]::TryParse($Version, [ref]$parsedVersion)) {
    throw "VSIX manifest version '$Version' is not a valid numeric version."
}

$resolvedManifestPath = Resolve-Path $ManifestPath
[xml]$manifest = Get-Content $resolvedManifestPath.Path
$identity = $manifest.PackageManifest.Metadata.Identity
if (-not $identity) {
    throw "Unable to find VSIX manifest Identity element in '$ManifestPath'."
}

if ($identity.Version -ne $Version) {
    $content = Get-Content $resolvedManifestPath.Path -Raw
    $updatedContent = [regex]::Replace(
        $content,
        '(<Identity\b[^>]*\bVersion=)(["''])([^"'']+)(\2)',
        "`${1}`${2}$Version`${2}",
        1)
    if ($updatedContent -eq $content) {
        throw "Unable to update VSIX manifest Identity Version attribute in '$ManifestPath'."
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($resolvedManifestPath.Path, $updatedContent, $utf8NoBom)
}
Write-Host "Set VSIX manifest version to $Version in $ManifestPath."

if (-not $VsixPath) {
    return
}

$resolvedVsixPath = Resolve-Path $VsixPath
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($resolvedVsixPath.Path)
try {
    $entry = $zip.Entries |
        Where-Object { $_.FullName -eq "extension.vsixmanifest" -or $_.FullName -eq "source.extension.vsixmanifest" } |
        Select-Object -First 1
    if (-not $entry) {
        throw "Unable to find extension.vsixmanifest inside '$VsixPath'."
    }

    $stream = $entry.Open()
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            [xml]$packagedManifest = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    $packagedIdentity = $packagedManifest.PackageManifest.Metadata.Identity
    if (-not $packagedIdentity) {
        throw "Unable to find packaged VSIX Identity element in '$VsixPath'."
    }

    if ($packagedIdentity.Version -ne $Version) {
        throw "Packaged VSIX manifest version '$($packagedIdentity.Version)' does not match expected version '$Version'."
    }

    Write-Host "Verified packaged VSIX manifest version $Version in $VsixPath."
}
finally {
    $zip.Dispose()
}
