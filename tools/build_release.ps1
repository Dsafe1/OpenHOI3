<#
  Builds a release of the engine (docs/launcher.md, section 7): export/release/, which holds

    OpenHOI3-engine-<x>.zip     the engine alone, engine.txt at its root: what a mod made for it puts in its engine folder
    OpenHOI3-launcher-<x>.zip   the launcher alone, with no engine of its own
    OpenHOI3-<x>.zip            the launcher with the engine as its own: what a player of the base game is given
    SHA256SUMS.txt              each archive's SHA-256, which a mod's packaging checks what it fetched against
    release.md                  the release's notes: the version a mod names, and where the source is

  <x> is the tag without its v: the tree must be clean and its commit carry a tag v<x>, so that the engine.txt in the
  archives says openhoi3-<x> (tools/engine_version.ps1).

  Nothing is published. The script ends by printing the command that would publish the archives as the tag's release
  on GitHub; running it is its user's step. The tag's page there is also the offer of source the GPL asks of whoever
  ships the engine (docs/launcher.md, section 10).

  Usage: tools/build_release.ps1 [-Godot <path to the Godot .NET editor>] [-Runtime win-x64] [-AllowUntagged] [-Latest]
    -AllowUntagged  builds the archives from a tree that is no release, under its date-and-commit version: a trial of
                    the packaging, never to be published.
    -Latest         the rolling build of the main branch (docs/launcher.md, section 7.1): a tree that is no release,
                    its archives named OpenHOI3-engine-latest.zip and so on whatever its version, so that they are
                    found at one address. The engine.txt inside says the date-and-commit version it is. The pipeline
                    publishes them as the release of the tag latest, made again at every change.
#>
param(
    [string] $Godot,
    [string] $Runtime = 'win-x64',
    [switch] $AllowUntagged,
    [switch] $Latest
)
$ErrorActionPreference = 'Stop'
# The rolling build is of a tree that is no release.
if ($Latest) { $AllowUntagged = $true }

$tools = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $tools
$exportRoot = Join-Path $root 'export'

$version = & (Join-Path $tools 'engine_version.ps1') -RequireRelease:(-not $AllowUntagged)
$name = if ($Latest) { 'latest' } else { $version.Substring('openhoi3-'.Length) }

$buildArguments = @{ Runtime = $Runtime }
if ($Godot) { $buildArguments['Godot'] = $Godot }
if (-not $AllowUntagged) { $buildArguments['Release'] = $true }
& (Join-Path $tools 'build_both.ps1') @buildArguments

# The engine as it was built says the version the archives are named by, or something moved under the build.
$engineFolder = Join-Path $exportRoot 'OpenHOI3'
$launcherFolder = Join-Path $exportRoot 'Launcher'
$built = Select-String -Path (Join-Path $engineFolder 'engine.txt') -Pattern '^version = "([^"]+)"' | Select-Object -First 1
if (-not $built -or $built.Matches[0].Groups[1].Value -ne $version) {
    throw "The engine built says '$($built.Matches[0].Groups[1].Value)' where the release is $version."
}

$out = Join-Path $exportRoot 'release'
if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}
New-Item -ItemType Directory -Force -Path $out | Out-Null

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$level = [System.IO.Compression.CompressionLevel]::Optimal

# A folder's files as an archive, each under its path from the folder with forward slashes: Windows PowerShell's own
# packing of a folder writes backslashes, which only Windows' tools read as folders.
function Compress-Folder([string] $folder, [string] $archive) {
    $base = (Resolve-Path $folder).Path.TrimEnd('\') + '\'
    $zip = [System.IO.Compression.ZipFile]::Open($archive, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -Path $folder -Recurse -File) {
            $entry = $file.FullName.Substring($base.Length).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, $level) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }
}

$engineZip = Join-Path $out "OpenHOI3-engine-$name.zip"
$launcherZip = Join-Path $out "OpenHOI3-launcher-$name.zip"
$bothZip = Join-Path $out "OpenHOI3-$name.zip"

Write-Host "Packing $engineZip"
Compress-Folder $engineFolder $engineZip

# The launcher with its engine is export/Launcher as build_both.ps1 leaves it; the launcher alone is that without the
# engine folder, packed from a copy so that the folder a player may be trying stays whole.
Write-Host "Packing $bothZip"
Compress-Folder $launcherFolder $bothZip

$staging = Join-Path $out 'launcher-alone'
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Get-ChildItem -Path $launcherFolder | Where-Object { $_.Name -ne 'engine' } | Copy-Item -Recurse -Destination $staging
Write-Host "Packing $launcherZip"
Compress-Folder $staging $launcherZip
Remove-Item -Recurse -Force $staging

$archives = @($engineZip, $launcherZip, $bothZip)
$sums = foreach ($archive in $archives) {
    # The form sha256sum writes and checks: the hash in lower case, two spaces, the file's name.
    "$((Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant())  $(Split-Path -Leaf $archive)"
}
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText((Join-Path $out 'SHA256SUMS.txt'), (($sums -join "`n") + "`n"), $utf8)

$contract = (Select-String -Path (Join-Path $engineFolder 'engine.txt') -Pattern '^contract = (\d+)' | Select-Object -First 1).Matches[0].Groups[1].Value
$tag = if ($AllowUntagged) { '(no tag: a trial of the packaging)' } else { "v$name" }
$commit = (& git -C $root rev-parse HEAD 2>$null)
$latestNotes = @"
OpenHOI3 latest: $version

The newest build of the main branch, made again at every change to it - this one of commit $commit. It is no
release: it is for trying what is not released yet, and for a mod's test builds. A mod's own release names a
numbered release of the engine, which stays as it is.

The engine says it is ``$version`` in its ``engine.txt``; a mod played on it names that version. The launcher's
command line is contract $contract.

- ``OpenHOI3-latest.zip`` - the launcher with the engine: unpack it anywhere and start ``OpenHOI3.Launcher.exe``.
- ``OpenHOI3-engine-latest.zip`` - the engine alone, for a mod's test build.
- ``OpenHOI3-launcher-latest.zip`` - the launcher alone, for a mod's test build.
- ``SHA256SUMS.txt`` - the archives' SHA-256.

You need your own copy of Hearts of Iron 3: Their Finest Hour; nothing of the game is in these archives.

OpenHOI3 is under the GNU General Public License, version 3. The source of this build is this commit:
https://github.com/Dsafe1/OpenHOI3/tree/$commit
"@
$notes = @"
OpenHOI3 $name

The engine's version is ``$version``: a mod made for it says ``engine = "$version"`` in its .mod file and ships
the contents of ``OpenHOI3-engine-$name.zip`` in its ``engine`` folder. The launcher's command line is contract $contract.

- ``OpenHOI3-$name.zip`` - the launcher with the engine: unpack it anywhere and start ``OpenHOI3.Launcher.exe``.
- ``OpenHOI3-engine-$name.zip`` - the engine alone, for a mod's release.
- ``OpenHOI3-launcher-$name.zip`` - the launcher alone, for a mod's release.
- ``SHA256SUMS.txt`` - the archives' SHA-256.

You need your own copy of Hearts of Iron 3: Their Finest Hour; nothing of the game is in these archives.

OpenHOI3 is under the GNU General Public License, version 3. The source of this release is this tag:
https://github.com/Dsafe1/OpenHOI3/tree/$tag
"@
if ($Latest) { $notes = $latestNotes }
[System.IO.File]::WriteAllText((Join-Path $out 'release.md'), ($notes -replace "`r`n", "`n") + "`n", $utf8)

Write-Host ''
Write-Host "Release $version in $out"
foreach ($archive in $archives) {
    Write-Host ("  {0,-40} {1,6:N0} MB" -f (Split-Path -Leaf $archive), ((Get-Item $archive).Length / 1MB))
}
if ($Latest) {
    Write-Host 'The rolling build: the pipeline publishes it as the release of the tag latest.'
}
elseif ($AllowUntagged) {
    Write-Host 'A trial of the packaging from a tree that is no release: not to be published.'
}
else {
    Write-Host ''
    Write-Host 'Nothing was published. To publish, push the tag and make its release:'
    Write-Host "  git push origin v$name"
    Write-Host "  gh release create v$name --title `"OpenHOI3 $name`" --notes-file `"$(Join-Path $out 'release.md')`" ``"
    Write-Host "    `"$engineZip`" `"$launcherZip`" `"$bothZip`" `"$(Join-Path $out 'SHA256SUMS.txt')`""
}
