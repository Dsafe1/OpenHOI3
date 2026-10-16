namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The four load and unload buttons of a selected unit's strip: which shows and which may be pressed, the fleet the load
/// button would put the unit aboard, and whether its brigades are too worn for planes. See
/// <c>docs/mechanics/transport.md</c>, section 12.
/// </summary>
public sealed record CCarryButtons(bool LoadShown, bool LoadEnabled, bool UnloadShown, bool UnloadEnabled,
	bool PlaneLoadShown, bool PlaneUnloadShown, int Fleet, bool LowOrganisation)
{
	/// <summary>A fleet, an air unit, or anything with no buttons of the kind: all four hidden.</summary>
	public static readonly CCarryButtons None = new(false, false, false, false, false, false, 0, false);
}
