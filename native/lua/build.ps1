<#
  Builds openhoi3_lua.dll - Lua 5.1.4 and OpenHOI3's trampoline (ohoi/ohoi.c) - for 64-bit Windows,
  with the Visual Studio C++ tools found through vswhere. The CRT is linked statically (/MT), so
  the DLL needs nothing installed beside it.

  Usage: build.ps1 -OutDir <folder>     (lib/OpenHOI3.Lua runs it when the DLL is missing or older
                                         than its sources)
#>
param(
    [Parameter(Mandatory = $true)] [string] $OutDir
)
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "Building Lua needs the Visual Studio C++ tools, and vswhere.exe is not at $vswhere."
}
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) {
    throw 'Building Lua needs the Visual Studio C++ tools (the "Desktop development with C++" workload).'
}
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'

$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$objDir = Join-Path $OutDir 'obj'
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$sources = @(Get-ChildItem (Join-Path $here 'src\*.c')) + @(Get-ChildItem (Join-Path $here 'ohoi\*.c'))
$list = ($sources | ForEach-Object { '"' + $_.FullName + '"' }) -join ' '
$include = Join-Path $here 'src'
$dll = Join-Path $OutDir 'openhoi3_lua.dll'

# vcvars64.bat looks vswhere.exe up on the PATH itself.
$env:PATH = (Split-Path -Parent $vswhere) + ';' + $env:PATH

# C4244, C4267 and C4334 are the 64-bit compiler noticing that lua_Integer is 32 bits wide, which is
# deliberate (luaconf.h, LUA_INTEGER): it is as wide as in the original's 32-bit build.
$cl = "cl /nologo /O2 /MT /W3 /wd4244 /wd4267 /wd4334 /DLUA_BUILD_AS_DLL /D_CRT_SECURE_NO_WARNINGS /I`"$include`" /LD $list /Fo`"$objDir\\`" /Fe`"$dll`" /link /NOLOGO"
cmd /c "call `"$vcvars`" >nul && $cl"
if ($LASTEXITCODE -ne 0) {
    throw "cl failed with $LASTEXITCODE."
}
Write-Host "Built $dll"
