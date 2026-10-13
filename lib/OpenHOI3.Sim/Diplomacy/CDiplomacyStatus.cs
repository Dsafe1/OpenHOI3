using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

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

	/// <summary>Whether the holder's units may enter the other's land: one direction (<c>agreements.md</c>, section 5).</summary>
	public bool HasMilitaryAccess { get; set; }

	/// <summary>Whether the holder may run a debt with the other while at war (<c>aid.md</c>, section 1).</summary>
	public bool AllowDebts { get; set; }

	/// <summary>What the holder owes the other, in thousandths of money; never lowered (<c>aid.md</c>, section 1).</summary>
	public long Debt { get; set; }

	/// <summary>
	/// What the holder repays the other each day, in thousandths of money: worked out from the debt where it
	/// is nought or less, and set to nought on a day nothing is paid (<c>aid.md</c>, section 1).
	/// </summary>
	public long DailyRepay { get; set; }

	/// <summary>
	/// The last hour the holder's diplomat is on his way to the other, after an offer: until then its
	/// buttons towards the other are greyed (<c>agreements.md</c>, section 1).
	/// </summary>
	public long DiplomatUntil { get; set; } = long.MinValue;

	/// <summary>
	/// The hour the two were last brought up to date while at war, the original's <c>last_war</c> - stamped
	/// when a war's membership changes and as a game is loaded, never hourly. None where never at war. A
	/// truce counts from it (<c>war.md</c>, section 4).
	/// </summary>
	public long? LastWar { get; set; }

	/// <summary>The hour before which the holder may add no war goal against the other (<c>war.md</c>, section 3).</summary>
	public long NextWarGoal { get; set; } = long.MinValue;

	/// <summary>The casus belli the holder holds on the other, in the order given (<c>war.md</c>, section 2).</summary>
	public List<CCasusBelli> CasusBelli { get; } = new();
}
