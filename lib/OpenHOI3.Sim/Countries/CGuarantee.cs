namespace OpenHOI3.Sim.Countries;

/// <summary>
/// <see cref="Guarantor"/> guaranteeing <see cref="Guaranteed"/>'s independence. What it does in
/// the field is the agreements' piece; the month's relation drift asks whether there is one either
/// way (<c>docs/mechanics/diplomacy.md</c>, section 3).
/// </summary>
public readonly record struct CGuarantee(CCountryTag Guarantor, CCountryTag Guaranteed)
{
	public bool Joins(CCountryTag a, CCountryTag b)
	{
		return (Guarantor == a && Guaranteed == b) || (Guarantor == b && Guaranteed == a);
	}
}
