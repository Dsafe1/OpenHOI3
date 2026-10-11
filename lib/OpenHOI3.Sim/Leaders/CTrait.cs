using System.Collections.Generic;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// A trait a leader may have, as <c>common/traits.txt</c> declares it: which kinds of
/// leader it is meant for and what it does. See <c>docs/mechanics/leaders.md</c>, section 3.
/// </summary>
public sealed class CTrait
{
	/// <summary>The original keeps sixteen effects to a trait and drops the rest in silence.</summary>
	public const int MostEffects = 16;

	public CTrait(string key)
	{
		Key = key;
	}

	/// <summary>Its name in the files, which is also its localisation key.</summary>
	public string Key { get; }

	/// <summary>
	/// Which kinds of leader it is meant for; all three where the file does not say. Only
	/// the traits a leader earns in battle keep to it: one given by a file or by script
	/// is his whatever this says.
	/// </summary>
	public bool AllowedLand { get; set; } = true;

	public bool AllowedSea { get; set; } = true;

	public bool AllowedAir { get; set; } = true;

	public List<CTraitEffect> Effects { get; } = new();

	public bool IsAllowed(ELeaderType type)
	{
		return type switch
		{
			ELeaderType.Land => AllowedLand,
			ELeaderType.Sea => AllowedSea,
			_ => AllowedAir,
		};
	}

	public bool Has(ETraitEffect kind)
	{
		foreach (CTraitEffect effect in Effects)
		{
			if (effect.Kind == kind)
			{
				return true;
			}
		}
		return false;
	}

	public override string ToString()
	{
		return Key;
	}
}
