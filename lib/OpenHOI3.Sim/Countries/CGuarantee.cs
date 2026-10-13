namespace OpenHOI3.Sim.Countries;

/// <summary>
/// <see cref="Guarantor"/> guaranteeing <see cref="Guaranteed"/>'s independence, from the hour
/// <see cref="Start"/>: it ends twenty years after, and one with no start never ends
/// (<c>docs/mechanics/agreements.md</c>, section 6). What it does in a war is war and peace's; the
/// month's relation drift asks whether there is one either way (<c>docs/mechanics/diplomacy.md</c>,
/// section 3). <see cref="ViaFaction"/> marks the copies the Allies' rule makes for a guarantor's fellows
/// (<c>docs/mechanics/factions.md</c>, section 5); nothing treats them otherwise.
/// </summary>
public readonly record struct CGuarantee(CCountryTag Guarantor, CCountryTag Guaranteed, long? Start = null, bool ViaFaction = false)
{
	public bool Joins(CCountryTag a, CCountryTag b)
	{
		return (Guarantor == a && Guaranteed == b) || (Guarantor == b && Guaranteed == a);
	}
}
