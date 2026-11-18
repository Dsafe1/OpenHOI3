<#
  Builds the launcher (docs/launcher.md): export/Launcher/, which holds OpenHOI3.Launcher.exe - self-contained, one
  file, .NET and Avalonia inside it - and licences/ with the licences of what it ships. It has no engine of its own:
  tools/build_both.ps1 builds the engine as well and puts it in export/Launcher/engine/, the launcher's own engine -
  the one the base game and every mod naming no engine is played on. Without it, start the launcher with
  --engine=<folder of an exported engine>.

  The folder may go anywhere: the launcher finds the install itself, or asks for it. Nothing is written to an install.

  Usage: tools/build_launcher.ps1 [-Runtime win-x64]
#>
param(
    [string] $Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exportRoot = Join-Path $root 'export'
$out = Join-Path $exportRoot 'Launcher'
if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}
New-Item -ItemType Directory -Force -Path $out | Out-Null
# Keep the editor from importing what an export leaves here.
New-Item -ItemType File -Force -Path (Join-Path $exportRoot '.gdignore') | Out-Null

$project = Join-Path $root 'launcher\OpenHOI3.Launcher\OpenHOI3.Launcher.csproj'
& dotnet publish $project -c Release -r $Runtime --self-contained true -o $out `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true -p:DebugType=none | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}
# SkiaSharp's and HarfBuzzSharp's packages bring their native symbols along, a hundred megabytes nobody plays with.
Get-ChildItem -Path $out -Filter '*.pdb' | Remove-Item

# What is shipped is shipped with its licence: ours, the typeface's, and Avalonia's terms (MIT, as its packages declare).
$licences = Join-Path $out 'licences'
New-Item -ItemType Directory -Force -Path $licences | Out-Null
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $licences 'OpenHOI3-GPL-3.0.txt')
Copy-Item (Join-Path $root 'fonts\OFL-Liberation.txt') $licences
Set-Content -Path (Join-Path $licences 'third-party.txt') -Encoding utf8 -Value @'
The launcher is built on these, each under its own licence:

- .NET, MIT licence - https://github.com/dotnet/runtime
- Avalonia, MIT licence - https://github.com/AvaloniaUI/Avalonia
- SkiaSharp and HarfBuzzSharp, MIT licence - https://github.com/mono/SkiaSharp
- Liberation Sans, SIL Open Font License 1.1 - OFL-Liberation.txt beside this file

OpenHOI3 itself is under the GNU General Public License, version 3 - OpenHOI3-GPL-3.0.txt beside this file. Its source
is at https://github.com/Dsafe1/OpenHOI3.
'@

Write-Host "Built the launcher in $out"
