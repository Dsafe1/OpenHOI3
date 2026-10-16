namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The four load and unload buttons of a selected unit's strip: which shows and which may be pressed, the fleet and the
/// air unit the two load buttons would put the unit aboard, and whether its brigades are too worn for planes. See
/// <c>docs/mechanics/transport.md</c>, sections 12 and 13.6.
/// </summary>
public sealed record CCarryButtons(bool LoadShown, bool LoadEnabled, bool UnloadShown, bool UnloadEnabled,
	bool PlaneLoadShown, bool PlaneLoadEnabled, bool PlaneUnloadShown, bool PlaneUnloadEnabled, int Fleet, int Plane,
	bool LowOrganisation)
{
	/// <summary>A fleet, an air unit, or anything with no buttons of the kind: all four hidden.</summary>
	public static readonly CCarryButtons None = new(false, false, false, false, false, false, false, false, 0, 0, false);
}
