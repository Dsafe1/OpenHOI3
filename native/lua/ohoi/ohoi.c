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
  if (results < 0) {
    /* As luaL_error does: the message after where the calling script stands, "file:line:". */
    if (lua_isstring(L, -1)) {
      luaL_where(L, 1);
      lua_insert(L, -2);
      lua_concat(L, 2);
    }
    return lua_error(L);
  }
  return results;
}

/* How many calls a traceback names before it stops. */
#define OHOI_TRACEBACK_LEVELS 16

/*
** The message handler of the C# side's protected call: a string error with the stack it was raised
** in under it, a call a line, as Lua 5.1's debug.traceback writes them. Anything else is left as it is.
*/
static int ohoi_traceback(lua_State *L) {
  lua_Debug ar;
  luaL_Buffer b;
  int level = 1;
  if (!lua_isstring(L, 1))
    return 1;
  luaL_buffinit(L, &b);
  lua_pushvalue(L, 1);
  luaL_addvalue(&b);
  luaL_addstring(&b, "\nstack traceback:");
  while (lua_getstack(L, level, &ar)) {
    if (level > OHOI_TRACEBACK_LEVELS) {
      luaL_addstring(&b, "\n\t...");
      break;
    }
    level++;
    lua_getinfo(L, "Snl", &ar);
    luaL_addstring(&b, "\n\t");
    luaL_addstring(&b, ar.short_src);
    luaL_addchar(&b, ':');
    if (ar.currentline > 0) {
      lua_pushfstring(L, "%d:", ar.currentline);
      luaL_addvalue(&b);
    }
    if (*ar.namewhat != '\0')
      lua_pushfstring(L, " in function '%s'", ar.name);
    else if (*ar.what == 'm')
      lua_pushliteral(L, " in main chunk");
    else if (*ar.what == 'C' || *ar.what == 't')
      lua_pushliteral(L, " ?");
    else
      lua_pushfstring(L, " in function <%s:%d>", ar.short_src, ar.linedefined);
    luaL_addvalue(&b);
  }
  luaL_pushresult(&b);
  return 1;
}

/* Pushes the message handler the C# side hands lua_pcall. */
OHOI_API void ohoi_pushtraceback(lua_State *L) {
  lua_pushcfunction(L, ohoi_traceback);
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

static int ohoi_iterator_next(lua_State *L) {
  int i = (int)lua_tointeger(L, lua_upvalueindex(2)) + 1;
  lua_pushinteger(L, i);
  lua_replace(L, lua_upvalueindex(2));
  lua_rawgeti(L, lua_upvalueindex(1), i);
  return 1;
}

/*
** Replaces the table on the top of the stack with a function that answers its elements 1, 2, ... in
** turn and then nil: what a generic for walks. An element that is nil ends the walk there, as the
** original's iterator over an array ends at a null element.
*/
OHOI_API void ohoi_pushiterator(lua_State *L) {
  lua_pushinteger(L, 0);
  lua_pushcclosure(L, ohoi_iterator_next, 2);
}
