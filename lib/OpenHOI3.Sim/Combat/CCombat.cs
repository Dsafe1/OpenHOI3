using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One battle on land: a province being fought over, the side that came to take it and
/// the side that was there. See <c>docs/mechanics/landcombat.md</c>.
/// </summary>
public sealed class CCombat
{
	/// <summary>What <see cref="Hour"/> is before the first round.</summary>
	public const int NotStarted = -1;

	public CCombat(int province)
	{
		Province = province;
		Attacker = new CCombatSide(this, isAttacker: true);
		Defender = new CCombatSide(this, isAttacker: false);
	}

	/// <summary>The id of the province fought over. The defenders stand in it.</summary>
	public int Province { get; }

	public CCombatSide Attacker { get; }

	public CCombatSide Defender { get; }

	/// <summary>How many rounds have been fought.</summary>
	public int Duration { get; set; }

	/// <summary>
	/// The hours since the first round, which is hour 0. The original saves it as
	/// <c>day</c>. Nobody comes up from reserve until it is above 0.
	/// </summary>
	public int Hour { get; set; } = NotStarted;

	/// <summary>Whether its sides have had their fronts set up, which the first hour does.</summary>
	public bool HasBegun { get; set; }

	/// <summary>Whether it has been decided. A battle is decided once.</summary>
	public bool IsConcluded { get; set; }

	/// <summary>Whether the attacker lost, once it is decided.</summary>
	public bool AttackerLost { get; set; }

	/// <summary>
	/// What the two sides' tactics add to the battle's width, to the damage of each
	/// side's hits, and to how fast an attacker's delay runs down. Nothing picks a
	/// tactic yet, so all four are nothing.
	/// </summary>
	public CFixedPoint TacticWidth { get; set; }

	public CFixedPoint TacticAttacker { get; set; }

	public CFixedPoint TacticDefender { get; set; }

	public CFixedPoint TacticSpeed { get; set; }

	/// <summary>The side a unit is on, or null where it is on neither.</summary>
	public CCombatSide? SideOf(CUnit unit)
	{
		if (Attacker.Units.Contains(unit))
		{
			return Attacker;
		}
		return Defender.Units.Contains(unit) ? Defender : null;
	}

	public CCombatSide Other(CCombatSide side)
	{
		return side.IsAttacker ? Defender : Attacker;
	}

	public override string ToString()
	{
		return $"battle of {Province}: {Attacker.Units.Count} against {Defender.Units.Count}, hour {Hour}";
	}
}
