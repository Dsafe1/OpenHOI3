namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One tactic a side of a land battle can fight by, as a block of
/// <c>common/combat_tactics.txt</c> declares it. See
/// <c>docs/mechanics/landcombat.md</c>, section 11.
/// </summary>
public sealed class CCombatTactic
{
	public CCombatTactic(string name, int number)
	{
		Name = name;
		Number = number;
	}

	/// <summary>The block's name: <c>tactic_assault</c>. A technology adds to its weight under this name.</summary>
	public string Name { get; }

	/// <summary>Its place in the file, the first being 1. 0 is no tactic.</summary>
	public int Number { get; }

	/// <summary>The picture under <c>gfx/pictures/tactics</c> it is shown with, without the extension.</summary>
	public string Picture { get; init; } = "";

	/// <summary>The name of the tactic that counters it, or an empty string.</summary>
	public string CounteredBy { get; init; } = "";

	/// <summary>The number of the tactic that counters it, or 0. Set once the whole file is read.</summary>
	public int CounteredByNumber { get; set; }

	/// <summary>Its weight before anything is added.</summary>
	public CFixedPoint Base { get; init; }

	/// <summary>How aggressive a leader it suits, from -1 to 1.</summary>
	public CFixedPoint Aggressiveness { get; init; }

	/// <summary>What it adds to the battle's width.</summary>
	public CFixedPoint Width { get; init; }

	/// <summary>What it adds to the damage of the attacker's hits.</summary>
	public CFixedPoint Attacker { get; init; }

	/// <summary>What it adds to the damage of the defender's hits.</summary>
	public CFixedPoint Defender { get; init; }

	/// <summary>What it adds to how fast the attackers close and their delay runs down.</summary>
	public CFixedPoint Speed { get; init; }

	/// <summary>What must hold of a side for it to pick this, or null for nothing.</summary>
	public CCombatCondition? Condition { get; init; }

	public override string ToString()
	{
		return Name;
	}
}
