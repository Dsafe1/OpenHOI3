using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// One block of <c>common/gainable_traits.txt</c>: a trait a leader earns by hours in one
/// kind of battle, where its condition holds as each stretch of them is counted. See
/// <c>docs/mechanics/leaders.md</c>, section 11.
/// </summary>
public sealed class CGainableTrait
{
	public CGainableTrait(ECombatKind combat, CTrait trait)
	{
		Combat = combat;
		Trait = trait;
	}

	/// <summary>The kind of battle the hours are counted in.</summary>
	public ECombatKind Combat { get; }

	public CTrait Trait { get; }

	/// <summary>The hours that earn the trait whole. Nothing or less earns it at the first count.</summary>
	public int HoursRequired { get; init; }

	/// <summary>What has to hold for a stretch of hours to count; none where the block has no <c>trigger</c>.</summary>
	public CCombatCondition? Condition { get; init; }
}
