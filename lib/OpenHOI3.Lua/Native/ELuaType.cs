namespace OpenHOI3.Lua.Native;

/// <summary>A Lua value's type, as <c>lua_type</c> answers it.</summary>
public enum ELuaType
{
	None = -1,
	Nil = 0,
	Boolean = 1,
	LightUserdata = 2,
	Number = 3,
	String = 4,
	Table = 5,
	Function = 6,
	Userdata = 7,
	Thread = 8,
}
