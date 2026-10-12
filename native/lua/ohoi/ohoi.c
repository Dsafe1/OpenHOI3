/*
** OpenHOI3's own addition to the Lua library, built into the same DLL. Not part of Lua.
**
** A Lua error is a longjmp, and a longjmp through a .NET frame is undefined behaviour. So a
** function written in C# never raises one: it returns a count, or -1 with its message on the top
** of the stack, and the trampoline here - plain C, with no managed frame between it and the
** setjmp in lua_pcall - raises the error for it. The C# side must call into Lua only through
** lua_pcall, and only through API functions that cannot raise.
*/

/* Built inside the DLL, so the API is this module's own and not imported. */
#define LUA_LIB

#include "lua.h"
#include "lauxlib.h"

#if defined(_WIN32)
#define OHOI_API __declspec(dllexport)
#else
#define OHOI_API
#endif

typedef int (*ohoi_Function)(lua_State *L, void *context);

static int ohoi_trampoline(lua_State *L) {
  ohoi_Function f = (ohoi_Function)lua_touserdata(L, lua_upvalueindex(1));
  void *context = lua_touserdata(L, lua_upvalueindex(2));
  int results = f(L, context);
  if (results < 0)
    return lua_error(L);
  return results;
}

/*
** Pushes a Lua function that calls f with the context given here, which is how the C# side finds
** the object a function belongs to.
*/
OHOI_API void ohoi_pushfunction(lua_State *L, ohoi_Function f, void *context) {
  lua_pushlightuserdata(L, (void *)f);
  lua_pushlightuserdata(L, context);
  lua_pushcclosure(L, ohoi_trampoline, 2);
}
