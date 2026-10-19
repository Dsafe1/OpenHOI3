using System.Collections.Generic;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A unit's battle plan, which the AI keeps on each theatre's headquarters: whether it is on, three stances, the AI's
/// power beside the enemy's, an area of operations, the aircraft it wants and its objectives. See
/// <c>docs/mechanics/agents.md</c>, section 7.
/// </summary>
public sealed class CUnitPlan
{
	/// <summary>Withdraw, prepare, defensive, attacking, blitzing: the ground stances.</summary>
	public const int Withdraw = 0;

	public const int Prepare = 1;

	public const int Defensive = 2;

	public const int Attacking = 3;

	public const int Blitzing = 4;

	/// <summary>The air and naval stance a new plan has: defensive.</summary>
	public const int DefensiveAirOrNaval = 1;

	/// <summary>The air and naval stance the AI gives every plan: offensive.</summary>
	public const int Offensive = 2;

	public bool Active { get; set; }

	/// <summary>The ground stance, 0 to 4.</summary>
	public int Stance { get; set; } = Defensive;

	/// <summary>The air and naval stances, 0 prepare, 1 defensive, 2 offensive.</summary>
	public int AirStance { get; set; } = DefensiveAirOrNaval;

	public int NavalStance { get; set; } = DefensiveAirOrNaval;

	/// <summary>The AI's power in the theatre and the enemy's facing it, in thousandths.</summary>
	public long OurPower { get; set; }

	public long TheirPower { get; set; }

	/// <summary>The port divisions go aboard and the one they land at, or 0.</summary>
	public int Embark { get; set; }

	public int Debark { get; set; }

	/// <summary>Whether the AI may reorganise the headquarters' subordinates.</summary>
	public bool AllowHqReorg { get; set; }

	/// <summary>The area of operations, in the order it was taken.</summary>
	public List<int> Area { get; } = new();

	/// <summary>How many of each unit type the plan wants: the null type first, then the unit files' types in order.</summary>
	public List<int> Min { get; } = new();

	public List<CObjective> Objectives { get; } = new();

	/// <summary>Its axis: the way its attack runs, from beside its area to an objective (<c>docs/mechanics/moving.md</c>, section 11.6).</summary>
	public List<int> Path { get; } = new();

	/// <summary>A second way, which the AI leaves empty.</summary>
	public List<int> Path2 { get; } = new();

	/// <summary>Off, with everything as a new plan has it.</summary>
	public void Reset()
	{
		Active = false;
		Stance = Defensive;
		AirStance = DefensiveAirOrNaval;
		NavalStance = DefensiveAirOrNaval;
		OurPower = 0;
		TheirPower = 0;
		Embark = 0;
		Debark = 0;
		Area.Clear();
		Min.Clear();
		Objectives.Clear();
		Path.Clear();
		Path2.Clear();
	}
}
