<#
.SYNOPSIS
    Writes the GitHub release description for one version from src/ReleaseNotes.xml.

.DESCRIPTION
    ReleaseNotes.xml is the single source of release notes: the app shows it in its Release Notes
    window, and the release description is generated from the same entry, so the two never differ.

    Fails when the version has no entry: a release must not go out with an empty description.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Path = 'src/ReleaseNotes.xml',
    [string]$Language = 'en',
    [string]$OutFile = 'release-notes.md'
)

$ErrorActionPreference = 'Stop'

[xml]$xml = Get-Content -Path $Path -Raw -Encoding UTF8

# <Version> is "1.2.8.9 (03.10.2026)": match the number before the date.
$entry = $xml.ArrayOfReleaseNotes.ReleaseNotes |
    Where-Object { ($_.Version -split ' ')[0] -eq $Version } |
    Select-Object -First 1
if ($null -eq $entry) {
    throw "src/ReleaseNotes.xml has no entry for version $Version. Add it before tagging the release."
}

function Get-ItemText($item) {
    # Current entries are <Item><ru/><en/></Item>; the oldest ones are plain <string>.
    if ($item.LocalName -eq 'string') { return $item.InnerText.Trim() }
    $text = $item.SelectSingleNode($Language)
    if ($null -eq $text -or [string]::IsNullOrWhiteSpace($text.InnerText)) { $text = $item.SelectSingleNode('en') }
    if ($null -eq $text) { $text = $item.FirstChild }
    return $text.InnerText.Trim()
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("## LogViewer $Version")
$lines.Add('')
$lines.Add("Download ``LogViewer-$Version.zip`` below, extract it to any folder you can write to and run ``LogViewer.exe``. " +
    'Requires .NET Framework 4.8 (included in Windows 10 1903+ and Windows 11). Settings in `Documents\LogViewer` are kept.')
$lines.Add('')
$lines.Add('> The executable is not code-signed, so Windows SmartScreen may warn on the first start: choose **More info -> Run anyway**.')

$sections = [ordered]@{ NewFeatures = 'New'; ChangedFeatures = 'Changed'; FixedBugs = 'Fixed' }
$itemCount = 0
foreach ($name in $sections.Keys) {
    $node = $entry.SelectSingleNode($name)
    if ($null -eq $node) { continue }
    $items = @($node.ChildNodes | Where-Object { $_.NodeType -eq 'Element' } | ForEach-Object { Get-ItemText $_ } | Where-Object { $_ })
    if ($items.Count -eq 0) { continue }
    $lines.Add('')
    $lines.Add("### $($sections[$name])")
    $lines.Add('')
    foreach ($text in $items) { $lines.Add("- $text") }
    $itemCount += $items.Count
}

if ($itemCount -eq 0) {
    throw "The entry for version $Version in src/ReleaseNotes.xml has no items."
}

# UTF-8 without BOM, the same on Windows PowerShell 5.1 and PowerShell 7.
[IO.File]::WriteAllLines((Join-Path (Get-Location) $OutFile), $lines, (New-Object System.Text.UTF8Encoding $false))
