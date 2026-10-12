using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// What one country holds towards another - every country holds one towards every country, itself
/// included. See <c>docs/mechanics/diplomacy.md</c>, section 1.
/// </summary>
public sealed class CDiplomacyStatus
{
	public CDiplomacyStatus(CCountryTag holder, CCountryTag other)
	{
		Holder = holder;
		Other = other;
	}

	/// <summary>The country that holds it.</summary>
	public CCountryTag Holder { get; }

	/// <summary>The country it is held towards.</summary>
	public CCountryTag Other { get; }

	/// <summary>Between -200 and 200; meant to be the same both ways, which <see cref="CRelations"/> keeps.</summary>
	public CFixedPoint Relation { get; set; }

	/// <summary>How much threat the holder feels from the other. Never below 0, never capped.</summary>
	public CFixedPoint Threat { get; set; }
}
