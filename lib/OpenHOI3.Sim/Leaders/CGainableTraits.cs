using System.Collections.Generic;
using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// The gainable traits of <c>common/gainable_traits.txt</c>, in the order of the file. A
/// trait is earnable by each kind of leader its <c>allowed_leader</c> lets have it, and by
/// no other - the one place that setting is kept to. See <c>docs/mechanics/leaders.md</c>,
/// section 11.
/// </summary>
public sealed class CGainableTraits
{
	public static readonly CGainableTraits Empty = new(new List<CGainableTrait>());

	private readonly List<CGainableTrait> _all;

	public CGainableTraits(List<CGainableTrait> all)
	{
		_all = all;
	}

	public IReadOnlyList<CGainableTrait> All => _all;

	/// <summary>What a kind of leader may earn in a kind of battle, in the order of the file.</summary>
	public IEnumerable<CGainableTrait> For(ECombatKind combat, ELeaderType type)
	{
		foreach (CGainableTrait gainable in _all)
		{
			if (gainable.Combat == combat && gainable.Trait.IsAllowed(type))
			{
				yield return gainable;
			}
		}
	}

	/// <summary>Whether a kind of leader may earn anything at all.</summary>
	public bool HasAny(ELeaderType type)
	{
		foreach (CGainableTrait gainable in _all)
		{
			if (gainable.Trait.IsAllowed(type))
			{
				return true;
			}
		}
		return false;
	}
}
