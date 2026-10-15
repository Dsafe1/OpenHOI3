using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Occupation;

/// <summary>
/// One policy of <c>common/occupation_policies.txt</c>, which is a modifier a country puts on the land of another it
/// holds, with the condition it may be chosen under and its number - from 1 in the file's order, 0 being the game's own
/// <c>no_occupation</c>. See <c>docs/mechanics/occupation.md</c>, section 1.
/// </summary>
public sealed class COccupationPolicy
{
	public COccupationPolicy(CModifier modifier, int index)
	{
		Modifier = modifier;
		Index = index;
	}

	public string Name => Modifier.Name;

	public CModifier Modifier { get; }

	/// <summary>Its number in the list of policies; 0 is <c>no_occupation</c>.</summary>
	public int Index { get; }

	/// <summary>Whether it is one of the file's, and not <c>no_occupation</c>.</summary>
	public bool IsReal => Index > 0;

	/// <summary>Its <c>allow</c> block, asked of the country choosing with the occupied country as FROM; none answers yes.</summary>
	public Script.CTrigger? Allow { get; set; }

	public override string ToString()
	{
		return Name;
	}
}
