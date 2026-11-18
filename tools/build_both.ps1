<#
  Builds what a player is given (docs/launcher.md, section 7): the engine (tools/build_engine.ps1) and the launcher
  (tools/build_launcher.ps1), and puts the engine in the launcher's engine folder, as its own. The result is
  export/Launcher/, about 245 MB, to be put anywhere; export/OpenHOI3/, the engine on its own, is left beside it.

  Usage: tools/build_both.ps1 [-Godot <path to the Godot .NET editor>] [-Debug] [-Runtime win-x64]
#>
param(
    [string] $Godot,
    [switch] $Debug,
    [string] $Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'

$tools = Split-Path -Parent $MyInvocation.MyCommand.Path
$exportRoot = Join-Path (Split-Path -Parent $tools) 'export'

$engineArguments = @{}
if ($Godot) { $engineArguments['Godot'] = $Godot }
if ($Debug) { $engineArguments['Debug'] = $true }
& (Join-Path $tools 'build_engine.ps1') @engineArguments
& (Join-Path $tools 'build_launcher.ps1') -Runtime $Runtime

$engine = Join-Path $exportRoot 'Launcher\engine'
Copy-Item -Recurse (Join-Path $exportRoot 'OpenHOI3') $engine
Write-Host "Built the launcher with its engine in $(Join-Path $exportRoot 'Launcher')"
