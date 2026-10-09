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
	/// The countries this side fought as, in the order they joined, and kept when its
	/// units are gone.
	/// </summary>
	public List<CCountryTag> Countries { get; } = new();

	/// <summary>Takes a unit onto this side. One that joins a front already set up waits in reserve.</summary>
	public void Add(CUnit unit)
	{
		if (!Units.Contains(unit))
		{
			Units.Add(unit);
		}
		if (!Countries.Contains(unit.Owner))
		{
			Countries.Add(unit.Owner);
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

	/// <summary>Takes a unit off this side, wherever on it it was.</summary>
	public void Remove(CUnit unit)
	{
		Units.Remove(unit);
		Front.Remove(unit);
		Reserves.Remove(unit);
	}
}
