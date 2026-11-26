<#
  Builds the standalone game: export/OpenHOI3/, which holds OpenHOI3.exe (the game's resources packed into it),
  its .NET folder data_OpenHOI3_windows_x86_64 with openhoi3_lua.dll and openhoi3_lua514.dll, and OpenHOI3_licences with the licences of
  what it ships.

  The build plays the install it is placed in - the folder holding the exe - so it is used by copying the whole of
  export/OpenHOI3/ beside hoi3_tfh.exe. That copy is the player's step; this script never writes to an install.
  Started by the launcher, it plays the install the launcher names wherever it stands: tools/build_both.ps1 puts it
  in the launcher's engine folder. Its engine.txt says what it is (docs/launcher.md, section 2).

  It needs the Godot 4.7.1 .NET export templates (the editor's Editor > Manage Export Templates). The Godot
  executable is taken from -Godot, else from the "godotTools.editorPath.godot4" setting in .vscode/settings.json.

  Usage: tools/build_engine.ps1 [-Godot <path to the Godot .NET editor>] [-Debug] [-Release]
    -Release  refuses a tree that is no release - changes not committed, or no tag v<x> on its commit - so that the
              engine.txt says openhoi3-<x> (tools/engine_version.ps1; tools/build_release.ps1 passes it).
#>
param(
    [string] $Godot,
    [switch] $Debug,
    [switch] $Release
)
$ErrorActionPreference = 'Stop'

# The version first: the build must not go on for minutes to find at its end that the tree is no release.
$version = & (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'engine_version.ps1') -RequireRelease:$Release

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (-not $Godot) {
    $settings = Join-Path $root '.vscode\settings.json'
    if (Test-Path $settings) {
        # The file is JSON with comments; the one key wanted is read by a pattern.
        $match = Select-String -Path $settings -Pattern '"godotTools\.editorPath\.godot4"\s*:\s*"([^"]+)"' | Select-Object -First 1
        if ($match) {
            $Godot = $match.Matches[0].Groups[1].Value -replace '\\\\', '\'
        }
    }
}
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw 'No Godot editor: pass -Godot, or set "godotTools.editorPath.godot4" in .vscode/settings.json.'
}

$exportRoot = Join-Path $root 'export'
$out = Join-Path $exportRoot 'OpenHOI3'
if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}
New-Item -ItemType Directory -Force -Path $out | Out-Null
# Keep the editor from importing what an export leaves here.
New-Item -ItemType File -Force -Path (Join-Path $exportRoot '.gdignore') | Out-Null

# A tree fresh from the repository has nothing imported, and an export of it finds no texture and no typeface: the
# editor imports it first, which a tree that has been opened in it has behind it. Start-Process, since the editor's
# binary called plainly has been seen to come back before the import was done.
if (-not (Test-Path (Join-Path $root '.godot\imported'))) {
    Write-Host 'Importing the project, which this tree has not been yet.'
    $import = Start-Process -FilePath $Godot -ArgumentList "--headless --path `"$root`" --import" -Wait -PassThru -NoNewWindow
    if (-not (Test-Path (Join-Path $root '.godot\imported'))) {
        throw "The import made no .godot\imported (the editor answered $($import.ExitCode))."
    }
}

$mode = if ($Debug) { '--export-debug' } else { '--export-release' }
$exe = Join-Path $out 'OpenHOI3.exe'
# The editor's binary is a GUI program; piping it through Out-Host makes PowerShell wait for it.
& $Godot --headless --path $root $mode 'Windows' $exe 2>&1 | Out-Host
if (-not (Test-Path $exe)) {
    throw "The export made no $exe. Are the Godot 4.7.1 .NET export templates installed?"
}

$data = Join-Path $out 'data_OpenHOI3_windows_x86_64'
# LuaJIT, which the install's Lua runs on, and the original's Lua 5.1.4 the --lua=5.1.4 switch chooses (docs/mechanics/lua.md).
foreach ($name in 'openhoi3_lua.dll', 'openhoi3_lua514.dll') {
    $lua = Get-ChildItem -Path $data -Recurse -Filter $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $lua) {
        throw "$name is not in $data; the install's Lua would not run."
    }
}

# What is shipped is shipped with its licence: the typefaces under the SIL Open Font License, LuaJIT's and Lua's MIT terms.
# Prefixed, as the .NET folder is, because the whole folder is copied into an install beside the original's own files.
$licences = Join-Path $out 'OpenHOI3_licences'
New-Item -ItemType Directory -Force -Path $licences | Out-Null
Copy-Item (Join-Path $root 'fonts\*.txt') $licences
Copy-Item (Join-Path $root 'fonts\readme.md') (Join-Path $licences 'fonts.md')
Copy-Item (Join-Path $root 'native\luajit\COPYRIGHT') (Join-Path $licences 'LuaJIT-COPYRIGHT.txt')
Copy-Item (Join-Path $root 'native\lua\COPYRIGHT') (Join-Path $licences 'Lua-COPYRIGHT.txt')
# Dear ImGui, which a mod's own windows are drawn with (docs/mechanics/modwindows.md): the Godot plugin that draws it,
# kept in addons/ with its licence, and the notes of the library and its .NET binding it rests on.
Copy-Item (Join-Path $root 'addons\imgui-godot\LICENSE.txt') (Join-Path $licences 'imgui-godot-LICENSE.txt')
Copy-Item (Join-Path $root 'addons\imgui-godot\third-party.txt') (Join-Path $licences 'imgui-third-party.txt')

# Breitkopf Fraktur is not in the repository, its licence forbidding that it be published there: each builder downloads it
# (fonts/readme.md). Without it the export draws the Axis names in Liberation Sans, and its licence does not go with it.
$fraktur = Join-Path $root 'fonts\BreitkopfFraktur.ttf'
if (-not (Test-Path $fraktur)) {
    Remove-Item (Join-Path $licences 'FFC-BreitkopfFraktur.txt') -ErrorAction SilentlyContinue
    Write-Warning 'fonts/BreitkopfFraktur.ttf is not downloaded, so this export draws the Axis names in Liberation Sans; fonts/readme.md says where to get it.'
}

# What the launcher reads to know this engine without starting it (docs/launcher.md, section 2): the engine's version -
# a release's by its tag, any other build's by the date and the commit (tools/engine_version.ps1) - and the version of
# the launcher's command line it understands, which is CLaunchContract.Version.
$contract = Select-String -Path (Join-Path $root 'lib\OpenHOI3.Content\Launch\CLaunchContract.cs') -Pattern 'const int Version = (\d+);' |
    Select-Object -First 1
if (-not $contract) {
    throw 'CLaunchContract.Version was not found, so engine.txt cannot say which command line this engine understands.'
}
$engineText = "# The engine in this folder, as the launcher reads it (docs/launcher.md).`n" +
    "version = `"$version`"`nexecutable = `"OpenHOI3.exe`"`ncontract = $($contract.Matches[0].Groups[1].Value)`n"
[System.IO.File]::WriteAllText((Join-Path $out 'engine.txt'), $engineText, (New-Object System.Text.UTF8Encoding $false))

Write-Host "Exported $out, engine $version"
Write-Host 'Copy the whole folder''s contents beside hoi3_tfh.exe to play.'
