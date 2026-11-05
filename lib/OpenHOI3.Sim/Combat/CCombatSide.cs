using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One side of a battle: every unit on it, and of those the ones on the front, who
/// shoot and are shot at, and the reserves, who wait for room. See
/// <c>docs/mechanics/landcombat.md</c>, section 3.
/// </summary>
public sealed class CCombatSide
{
	public CCombatSide(CCombat combat, bool isAttacker)
	{
		Combat = combat;
		IsAttacker = isAttacker;
	}

	public CCombat Combat { get; }

	public bool IsAttacker { get; }

	/// <summary>Every unit on this side, in the order they joined.</summary>
	public List<CUnit> Units { get; } = new();

	/// <summary>The units that fight this hour.</summary>
	public List<CUnit> Front { get; } = new();

	/// <summary>The units in the battle and not on the front.</summary>
	public List<CUnit> Reserves { get; } = new();

	/// <summary>
	/// Whether the front has been set up. Until it is, a unit that joins is only on the
	/// side; after, it joins the reserves.
	/// </summary>
	public bool IsFrontSetUp { get; set; }

	/// <summary>The tactic this side fights by, or null where it has none. See <see cref="CCombatTactics"/>.</summary>
	public CCombatTactic? Tactic { get; set; }

	/// <summary>This hour's roll, from 0 to 9. Rolled every hour; nothing built reads it.</summary>
	public int Dice { get; set; }

	/// <summary>The men this side has lost in the battle so far.</summary>
	public CFixedPoint Losses { get; set; }

	/// <summary>
	/// At sea: how well the side's ships are placed, which is what gets a ship a target
	/// and keeps it. Rolled when the battle begins and crept up after. See
	/// <c>docs/mechanics/navalcombat.md</c>, section 2.
	/// </summary>
	public CFixedPoint Positioning { get; set; }

	/// <summary>The roll that went into <see cref="Positioning"/>, kept for when a fleet joins.</summary>
	public CFixedPoint PositioningRoll { get; set; }

	/// <summary>Whether the side's submarines had their surprise in the round before this one.</summary>
	public bool HadSurprise { get; set; }

	/// <summary>Bombers of ships: whether they had their surprise in the round before this one.</summary>
	public bool HadAirSurprise { get; set; }

	/// <summary>
	/// Bombers: the strength damage their hits have put on their targets over the bombing, counted as it lands and
	/// before it is settled, so a little above the other side's losses. A save keeps it as <c>strength</c>.
	/// </summary>
	public CFixedPoint StrengthDealt { get; set; }

	/// <summary>Bombers: the levels they have destroyed of each building, by its key. A save keeps it as <c>building</c>.</summary>
	public Dictionary<string, CFixedPoint> BuildingsDestroyed { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// What has joined this side, by the key of each brigade's, ship's or wing's type: on land its men - its strength a
	/// hundred times over - and a whole one for a ship or a wing. Counted again whenever a unit joins, never taken from,
	/// so a unit that leaves and joins again counts twice; and a bombing's sides count nothing. A save keeps it as
	/// <c>size</c>. See <c>docs/mechanics/savegame.md</c>, section 7.
	/// </summary>
	public Dictionary<string, CFixedPoint> Size { get; } = new(StringComparer.Ordinal);

	/// <summary>A land brigade's men for each point of its strength, which is a constant of the original's.</summary>
	public const long MenPerStrength = 100 * CFixedPoint.Scale;

	/// <summary>Bombers: what they have destroyed of the province's stockpile.</summary>
	public Economy.CGoodsPool StockpileDestroyed { get; } = new();

	/// <summary>Bombers: what they have taken off the province's daily production.</summary>
	public Economy.CGoodsPool ProductionDestroyed { get; } = new();

	/// <summary>The ships this side has had sunk in the battle, in the order they went down.</summary>
	public List<CSubUnit> Sunk { get; } = new();

	/// <summary>The enemy ships the ship now choosing a target can reach. Filled for each choice.</summary>
	public List<CSubUnit> Targets { get; } = new();

	/// <summary>
	/// The countries this side fought as, in the order they joined, and kept when its
	/// units are gone.
	/// </summary>
	public List<CCountryTag> Countries { get; } = new();

	/// <summary>
	/// The countries still in the fight on this side: a unit's owner is put on it as the
	/// unit joins, and it is made again from the units left whenever one is taken off. So a
	/// beaten side's is empty by the battle's end, and its first country is what the list of
	/// finished battles names the side by. See <c>docs/mechanics/airmap.md</c>, section 2.
	/// </summary>
	public List<CCountryTag> Standing { get; } = new();

	/// <summary>Puts a country on both lists of this side's countries, where it is not there yet.</summary>
	public void AddCountry(CCountryTag country)
	{
		if (!Countries.Contains(country))
		{
			Countries.Add(country);
		}
		if (!Standing.Contains(country))
		{
			Standing.Add(country);
		}
	}

	/// <summary>Takes a unit onto this side. One that joins a front already set up waits in reserve.</summary>
	public void Add(CUnit unit)
	{
		if (!Units.Contains(unit))
		{
			Units.Add(unit);
		}
		AddCountry(unit.Owner);
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long joined = Combat.Kind == ECombatKind.Land ? subUnit.Strength.Raw * MenPerStrength / CFixedPoint.Scale : CFixedPoint.Scale;
			Size[subUnit.Type.Key] = new CFixedPoint(Size.GetValueOrDefault(subUnit.Type.Key).Raw + joined);
		}
		if (!unit.Combats.Contains(Combat))
		{
			unit.Combats.Add(Combat);
		}
		if (IsFrontSetUp && !Reserves.Contains(unit) && !Front.Contains(unit))
		{
			Reserves.Add(unit);
		}
	}

	/// <summary>
	/// Takes a unit off this side, wherever on it it was, and makes the countries still in
	/// the fight again from the units left. First every leader of the side has his banked
	/// hours counted towards the traits this battle earns.
	/// </summary>
	public void Remove(CGameState state, CUnit unit)
	{
		CTraitGain.Count(state, Combat, this);
		Units.Remove(unit);
		Front.Remove(unit);
		Reserves.Remove(unit);
		Standing.Clear();
		foreach (CUnit left in Units)
		{
			if (!Standing.Contains(left.Owner))
			{
				Standing.Add(left.Owner);
			}
		}
	}
}
