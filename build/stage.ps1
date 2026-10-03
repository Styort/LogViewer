<#
.SYNOPSIS
    Copies the Release build into a folder that can be zipped and handed to users.

.DESCRIPTION
    Used by both .github/workflows/build.yml (CI artifact) and release.yml (GitHub release zip),
    so the two packages cannot drift apart.

    The whole output folder is taken, subfolders included: English UI strings are a satellite
    assembly (en/LogViewer.resources.dll), and without it the app silently falls back to Russian.
    Symbols, library XML docs, stray zips and the app's own log folder are dropped.

    Prints the version of the packaged LogViewer.exe (FileVersion) as the only output.
#>
param(
    [string]$Source = 'src/bin/Release',
    [string]$Output = 'publish/LogViewer'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $Source 'LogViewer.exe'))) {
    throw "No build found in '$Source'. Build the solution in Release first."
}

if (Test-Path $Output) {
    Remove-Item $Output -Recurse -Force
}
New-Item -ItemType Directory -Force $Output | Out-Null
Copy-Item (Join-Path $Source '*') $Output -Recurse

Get-ChildItem $Output -Recurse -Include *.pdb, *.zip | Remove-Item -Force
Remove-Item (Join-Path $Output 'logs') -Recurse -Force -ErrorAction SilentlyContinue
# XML docs of referenced libraries sit next to their dll (X.xml + X.dll); ReleaseNotes.xml has no dll.
Get-ChildItem $Output -Recurse -Filter *.xml |
    Where-Object { Test-Path ([IO.Path]::ChangeExtension($_.FullName, '.dll')) } |
    Remove-Item -Force

foreach ($required in 'LogViewer.exe', 'LogViewer.exe.config', 'ReleaseNotes.xml', 'en/LogViewer.resources.dll') {
    if (-not (Test-Path (Join-Path $Output $required))) {
        throw "Missing in the package: $required"
    }
}

(Get-Item (Join-Path $Output 'LogViewer.exe')).VersionInfo.FileVersion
