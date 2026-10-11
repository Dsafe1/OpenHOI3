using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// A general, an admiral or an air marshal, as <c>history/leaders</c> declares him and
/// as the game has made of him since. See <c>docs/mechanics/leaders.md</c>.
/// </summary>
public sealed class CLeader
{
	public CLeader(int id)
	{
		Id = id;
	}

	/// <summary>The number his file gives him, which is what an order of battle names him by.</summary>
	public int Id { get; }

	public string Name { get; set; } = "";

	/// <summary>The country whose leader he is, which his file says.</summary>
	public CCountryTag? Country { get; set; }

	public ELeaderType Type { get; set; }

	/// <summary>
	/// 0 for a leader not yet in service, then 1 to 4. Which units he may command depends
	/// on it, and nothing else about him.
	/// </summary>
	public int Rank { get; set; }

	public int Skill { get; set; }

	/// <summary>
	/// Not a cap: how fast he learns, which is faster the further below it he is and a
	/// third as fast at or above it. His file's figure for life.
	/// </summary>
	public int MaxSkill { get; set; }

	/// <summary>The skill his file gave him, or -1 where it gave none.</summary>
	public int StartingSkill { get; set; } = -1;

	/// <summary>The experience his file gave him, in whole points; almost always nothing.</summary>
	public int StartingExperience { get; set; }

	/// <summary>
	/// What he has learned, in whole points with 15 binary places as the original keeps
	/// it. Never less than his skill's floor.
	/// </summary>
	public CFixed15 Experience { get; set; }

	public CFixedPoint Loyalty { get; set; }

	/// <summary>The name of his portrait, without the folder or the extension.</summary>
	public string Picture { get; set; } = "";

	/// <summary>His traits, in the order he gained them. One may be there twice.</summary>
	public List<CTrait> Traits { get; } = new();

	/// <summary>
	/// The day his history first gives him a rank, on which a day's pass puts him in
	/// service if he is not already; null where it never does, and then he never is.
	/// </summary>
	public CDate? FirstRankDate { get; set; }

	/// <summary>The unit he commands, or null.</summary>
	public CUnit? Unit { get; set; }

	public bool HasTrait(ETraitEffect kind)
	{
		foreach (CTrait trait in Traits)
		{
			if (trait.Has(kind))
			{
				return true;
			}
		}
		return false;
	}

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
