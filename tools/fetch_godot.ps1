<#
  Fetches what a build needs of Godot on a machine that has none of it - the pipeline's (docs/launcher.md, section 7.1):
  the .NET editor of one version, unpacked into a folder, and that version's export templates for Windows, put where
  the editor looks for them. Both from Godot's own releases on GitHub. What is already there is left alone.

  Usage: tools/fetch_godot.ps1 -Into <folder> [-Version 4.7.1]
  Writes the editor's path to the pipeline: what tools/build_engine.ps1 takes as -Godot.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Into,
    [string] $Version = '4.7.1'
)
$ErrorActionPreference = 'Stop'
# The progress bar of a download makes it many times slower where nobody watches it.
$ProgressPreference = 'SilentlyContinue'

$releases = "https://github.com/godotengine/godot/releases/download/$Version-stable"
$name = "Godot_v$Version-stable_mono_win64"

New-Item -ItemType Directory -Force -Path $Into | Out-Null
$Into = (Resolve-Path $Into).Path

# The editor: an archive of one folder, the program and its GodotSharp beside it.
$editor = Join-Path $Into "$name\$name.exe"
if (-not (Test-Path $editor)) {
    $archive = Join-Path $Into "$name.zip"
    Write-Host "Fetching $releases/$name.zip"
    Invoke-WebRequest -Uri "$releases/$name.zip" -OutFile $archive -UseBasicParsing
    Expand-Archive -Path $archive -DestinationPath $Into -Force
    Remove-Item $archive
    if (-not (Test-Path $editor)) {
        throw "The archive held no $name\$name.exe."
    }
}

# The export templates: an archive of every platform's, of which a Windows export needs four and the note of the
# version. They go where the editor looks: %APPDATA%\Godot\export_templates\<version>.stable.mono.
$templates = Join-Path $env:APPDATA "Godot\export_templates\$Version.stable.mono"
$wanted = @('windows_release_x86_64.exe', 'windows_release_x86_64_console.exe', 'windows_debug_x86_64.exe',
    'windows_debug_x86_64_console.exe', 'version.txt')
$missing = @($wanted | Where-Object { -not (Test-Path (Join-Path $templates $_)) })
if ($missing.Count -gt 0) {
    $archive = Join-Path $Into "export_templates-$Version.tpz"
    $address = "$releases/Godot_v$Version-stable_mono_export_templates.tpz"
    Write-Host "Fetching $address"
    Invoke-WebRequest -Uri $address -OutFile $archive -UseBasicParsing
    New-Item -ItemType Directory -Force -Path $templates | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            # The archive holds one folder, templates/, with a file for each platform.
            if ($wanted -contains $entry.Name -and $entry.FullName -eq "templates/$($entry.Name)") {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $templates $entry.Name), $true)
            }
        }
    }
    finally {
        $zip.Dispose()
    }
    Remove-Item $archive
    $missing = @($wanted | Where-Object { -not (Test-Path (Join-Path $templates $_)) })
    if ($missing.Count -gt 0) {
        throw "The export templates' archive lacked: $($missing -join ', ')."
    }
}

Write-Host "Godot ${Version}: $editor"
Write-Host "Its Windows export templates: $templates"
return $editor
