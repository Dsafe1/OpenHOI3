<#
  Builds openhoi3_lua.dll - LuaJIT and OpenHOI3's trampoline (native/lua/ohoi/ohoi.c) - for 64-bit Windows, with the
  Visual Studio C++ tools found through vswhere. The steps are those of LuaJIT's own src/msvcbuild.bat for an
  amalgamated build, run on a copy of the source in the output folder so that what they generate stays out of the
  repository, with the C runtime linked statically (/MT) and the DLL under OpenHOI3's name.

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
    throw "Building LuaJIT needs the Visual Studio C++ tools, and vswhere.exe is not at $vswhere."
}
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) {
    throw 'Building LuaJIT needs the Visual Studio C++ tools (the "Desktop development with C++" workload).'
}
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'

$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$work = Join-Path $OutDir 'luajit'
if (Test-Path $work) {
    Remove-Item -Recurse -Force $work
}
New-Item -ItemType Directory -Force -Path $work | Out-Null
Copy-Item -Recurse (Join-Path $here 'src') (Join-Path $work 'src')
Copy-Item -Recurse (Join-Path $here 'dynasm') (Join-Path $work 'dynasm')
Copy-Item (Join-Path $here '..\lua\ohoi\ohoi.c') (Join-Path $work 'src\ohoi.c')
# The version LuaJIT's genversion.lua writes into luajit.h, from the commit's time (.relver).
Copy-Item (Join-Path $here '.relver') (Join-Path $work 'src\luajit_relver.txt')
$dll = Join-Path $OutDir 'openhoi3_lua.dll'

# Every randomisation LuaJIT's security options make is off, so that a table's order and everything else is the same
# on every run: a game here is replayed from its seed and its commands (docs/replay.md).
$secure = '/DLUAJIT_SECURITY_PRNG=0 /DLUAJIT_SECURITY_STRHASH=0 /DLUAJIT_SECURITY_STRID=0 /DLUAJIT_SECURITY_MCODE=0'
$compile = 'cl /nologo /c /O2 /W3 /D_CRT_SECURE_NO_DEPRECATE /D_CRT_STDIO_INLINE=__declspec(dllexport)__inline'
$link = 'link /nologo'
$libs = 'lib_base.c lib_math.c lib_bit.c lib_string.c lib_table.c lib_io.c lib_os.c lib_package.c lib_debug.c lib_jit.c lib_ffi.c lib_buffer.c'
# A batch file, so that every step runs in the environment vcvars64.bat makes; cmd is told to look in the current
# folder for the tools the build makes there.
$bat = Join-Path $work 'build.bat'
Set-Content -Path $bat -Encoding ascii -Value @(
    '@set NoDefaultCurrentDirectoryInExePath=',
    "@call `"$vcvars`" >nul || exit /b 1",
    "@cd /d `"$work\src`" || exit /b 1",
    "$compile host\minilua.c || exit /b 1",
    "$link /out:minilua.exe minilua.obj || exit /b 1",
    'minilua ..\dynasm\dynasm.lua -LN -D WIN -D JIT -D FFI -D ENDIAN_LE -D FPU -D P64 -o host\buildvm_arch.h vm_x64.dasc || exit /b 1',
    'minilua host\genversion.lua || exit /b 1',
    "$compile /I . /I ..\dynasm host\buildvm*.c || exit /b 1",
    "$link /out:buildvm.exe buildvm*.obj || exit /b 1",
    'buildvm -m peobj -o lj_vm.obj || exit /b 1',
    "buildvm -m bcdef -o lj_bcdef.h $libs || exit /b 1",
    "buildvm -m ffdef -o lj_ffdef.h $libs || exit /b 1",
    "buildvm -m libdef -o lj_libdef.h $libs || exit /b 1",
    "buildvm -m recdef -o lj_recdef.h $libs || exit /b 1",
    "buildvm -m vmdef -o jit\vmdef.lua $libs || exit /b 1",
    'buildvm -m folddef -o lj_folddef.h lj_opt_fold.c || exit /b 1',
    # C4244 is MSVC on LuaJIT's own narrowing, and on lua_Integer being 32 bits wide, which is deliberate (luaconf.h).
    "$compile /MT /wd4244 /DLUA_BUILD_AS_DLL $secure ljamalg.c || exit /b 1",
    "$compile /MT /DLUA_BUILD_AS_DLL /I . ohoi.c || exit /b 1",
    "$link /DLL /OPT:REF /OPT:ICF /INCREMENTAL:NO /OUT:`"$dll`" ljamalg.obj lj_vm.obj ohoi.obj || exit /b 1"
)
$env:PATH = (Split-Path -Parent $vswhere) + ';' + $env:PATH
cmd /c "`"$bat`""
if ($LASTEXITCODE -ne 0) {
    throw "Building LuaJIT failed with $LASTEXITCODE."
}
Write-Host "Built $dll"
