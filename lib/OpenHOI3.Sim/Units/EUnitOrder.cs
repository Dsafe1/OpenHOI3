namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit has been told to do. The original has some forty orders; these are the
/// ones that only send a unit somewhere. See <c>docs/mechanics/fleetsandair.md</c>,
/// section 5.
/// </summary>
public enum EUnitOrder
{
	/// <summary>Nothing. A unit with a path and no order is simply on its way.</summary>
	None,

	/// <summary>Go to a province and stay.</summary>
	Move,

	/// <summary>A fleet: make a port its base and go there.</summary>
	Rebase,

	/// <summary>An air unit: make an airfield its base and go there.</summary>
	RebaseAir,
}
