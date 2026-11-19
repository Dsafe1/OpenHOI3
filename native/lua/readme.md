# Lua 5.1.4

The interpreter the original runs its scripts on, built from its own source - chosen
on 2026-10-12, over a C# interpreter of our own and over MoonSharp: "Real Lua 5.1.4,
native". `docs/mechanics/lua.md` says how the game uses it.

**Since 2026-11-19 it is the switch's, not the default**: the scripts run on LuaJIT (`native/luajit`), taking about a fifth less time,
and this is kept for what wants a table's `pairs` in the original's order - a comparison with a run of the
original's, above all - chosen with `OPENHOI3_LUA=5.1.4`, the client's `--lua=5.1.4` or the savegame tool's
`lua=5.1.4` (`docs/mechanics/lua.md`, section 1; `docs/compatibility.md`, entry 30).

**Where it came from**: `https://www.lua.org/ftp/lua-5.1.4.tar.gz`, fetched 2026-10-12, md5
`d0870f2de55d59c1c8419f36e8fac150`, sha256
`b038e225eaf2a5b57c9bcc35cd13aa8c6c8288ef493d52970c9545074098af3a`. `src/` is the tarball's `src/`
without `lua.c`, `luac.c` and `print.c`, the stand-alone programs. `COPYRIGHT` is its licence, MIT.

**Why 5.1.4 and not 5.1.5**: it is the version the original ships - `lua5.1.dll` in an install says
"Lua 5.1.4 Copyright (C) 1994-2008" - and 5.1.5 mends bugs a script could notice.

**What is changed**, all in `src/luaconf.h` and each marked `OpenHOI3:`:

- `lua_number2int` and `lua_number2integer` round to the nearest, ties to even, on 64-bit MSVC. The
  original's 32-bit build converts with the x87's `fistp`, which rounds so (its `lua_tointeger` was
  read off the DLL: `fld` / `fistp`); a 64-bit build would otherwise truncate.
- `LUA_INTEGER` is `int`, 32 bits as `ptrdiff_t` is in the original's build, where a 64-bit build
  would make it 64.

`ohoi/ohoi.c` is ours: a trampoline, so that a function written in C# never raises a Lua error
itself - a Lua error is a `longjmp`, and a `longjmp` through a .NET frame is undefined. LuaJIT's build
compiles the same file.

**Building**: `build.ps1 -OutDir <folder>` builds `openhoi3_lua514.dll` - a name no other Lua on a machine has - for 64-bit Windows with the Visual
Studio C++ tools, found through `vswhere`, the C runtime linked in. `lib/OpenHOI3.Lua` runs it
whenever the DLL is missing or older than a source here, so `dotnet build` is enough; the DLL is
built output and is not committed. Other platforms are not built yet.
