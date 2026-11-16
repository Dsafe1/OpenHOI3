namespace OpenHOI3.Lua.BiceLib;

/// <summary>What a BiceLib function comes to in this engine (<c>docs/mechanics/bicelib.md</c>, section 2).</summary>
public enum EBiceLibVerdict
{
	/// <summary>About the injection, a console, the DLL's own registry or a workbench probe: a plain no-op.</summary>
	NothingToDo,

	/// <summary>A fix this engine already makes for every game: a no-op, with the citation in the spec.</summary>
	AlreadySo,

	/// <summary>A rule of the simulation.</summary>
	Simulation,

	/// <summary>The interface or a tooltip.</summary>
	Interface,

	/// <summary>A figure read for BiceLib's overlay pages and the old utility's.</summary>
	Getter,

	/// <summary>The AI's, which another piece of work has: not built here.</summary>
	Ai,
}
