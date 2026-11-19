# LuaJIT

The interpreter the install's Lua runs on by default: LuaJIT's, taking about a fifth less time than Lua 5.1.4 on
the AI's scripts. Chosen on 2026-11-19, on a measurement of both (`docs/performance.md`, section 2):
"Since the pairs order is unspecified, no code should rely on it." The original's own Lua 5.1.4 is kept
beside it in `native/lua`, behind a switch (`docs/mechanics/lua.md`, section 1).

**Where it came from**: `https://github.com/LuaJIT/LuaJIT`, the `v2.1` branch at commit
`63c8f1a9b479ba5518cf6a170ff7729178bc3cfd` of 11 November 2026, fetched 2026-11-19. `src/` and `dynasm/` are the
repository's folders as they were, `COPYRIGHT` its licence, MIT, and `README` its readme; its `doc/`, `etc/` and
top-level `Makefile` are left out. `.relver` holds the commit's time, which git's `export-subst` writes into an
archive and the build turns into the version.

**What is changed**, each marked `OpenHOI3:`, so that it answers the C interface and the scripts as `native/lua`'s
Lua 5.1.4 does:

- `src/luaconf.h`: `LUA_INTEGER` is `int`, 32 bits, as in the original's 32-bit build and in `native/lua`.
- `src/lj_api.c` (`lua_tointeger`) and `src/lj_lib.c` (`lj_lib_checkint`, which the C library's integer arguments
  are read through): **a number turns into an integer to the nearest, ties to even**, as the original's x87 `fistp`
  converts; LuaJIT truncates. The interpreter's own fast paths for a few library functions - `string.sub`,
  `string.byte` and the like - are written in its assembler and still truncate, so a fraction given to one of them
  is cut where 5.1.4 rounds it. No script of the base game's or BlackICE's was found to give one.
- `src/lib_math.c` and `src/lib_string.c`: `math.mod` and `string.gfind`, Lua 5.1.4's compatibility names for
  `math.fmod` and `string.gmatch` (`LUA_COMPAT_MOD`, `LUA_COMPAT_GFIND`), which LuaJIT dropped and the base game's
  scripts call.
- `src/lj_str.c` (`lj_str_alloc`): **the ID a table places a string key by is Lua 5.1.4's hash of the string's
  content** (`lstring.c`, `luaS_newlstr`). LuaJIT numbers its strings in the order they are made, so where a key
  stood in a table hung on what the state had run before - and the trading runs the countries on eight states
  side by side, each taking whichever country is next, so BlackICE's game came out otherwise from one run to the
  next (2026-11-19). With the content's hash it does not, and a string key's first place in a table is 5.1.4's.

**What the build sets** (`build.ps1`): every security randomisation off - `LUAJIT_SECURITY_PRNG`, `_STRHASH`,
`_STRID` and `_MCODE` all 0 - so that a table's order, and everything else, is the same on every run, as a replay
needs (`docs/replay.md`). **The compiler is never switched on**: it is built in, but only `luaopen_jit` turns it on
and the game never opens the `jit` library. It was measured slower than the interpreter alone here, because the
scripts call into the game's classes all the time and each call ends what it was compiling.

**Where it differs from 5.1.4, as far as is known**: the order `pairs` and `next` walk a table in, which follows from
how LuaJIT hashes and lays out its tables - so a script that keeps the first of several equal choices may make
another (`docs/compatibility.md`, entry 30) - and the fast paths' truncation above. Both play the same game every
run; they play different games from the same seed.

**Building**: `build.ps1 -OutDir <folder>` copies `src/` and `dynasm/` into `<folder>/luajit` and runs there the
steps of LuaJIT's own `src/msvcbuild.bat` for an amalgamated build - the `minilua` and `buildvm` host tools, the
generated headers and `lj_vm.obj` - then compiles `ljamalg.c` and `native/lua/ohoi/ohoi.c`, the trampoline both
interpreters share, into `openhoi3_lua.dll`, with the Visual Studio C++ tools found through `vswhere` and the C
runtime linked in. `lib/OpenHOI3.Lua` runs it whenever the DLL is missing or older than a source here, so
`dotnet build` is enough; the DLL is built output and is not committed. Other platforms are not built yet.
