using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One technology, as a file under <c>technologies/</c> declares it. Each level a
/// country has of it is worth the same again of every effect. What researching it takes
/// is in <c>docs/mechanics/research.md</c>, section 1.
/// </summary>
public sealed class CTechnology
{
	public CTechnology(string name, int maxLevel, CModifier effectPerLevel)
	{
		Name = name;
		MaxLevel = maxLevel;
		EffectPerLevel = effectPerLevel;
	}

	public string Name { get; }

	/// <summary>The highest level the file allows, or 0 when it does not say.</summary>
	public int MaxLevel { get; }

	public CModifier EffectPerLevel { get; }

	/// <summary>A whole number that what a level costs comes from.</summary>
	public int Difficulty { get; init; }

	/// <summary>The year level 1 is due.</summary>
	public int StartYear { get; init; }

	/// <summary>The year level 2 is due, whatever the key is called.</summary>
	public int FirstOffset { get; init; }

	/// <summary>The years added for each level past 2. Nothing makes it a single-level technology.</summary>
	public int AdditionalOffset { get; init; }

	/// <summary>Whether the country's modifier for nuclear research counts towards its bonus.</summary>
	public bool IsNuclear { get; init; }

	/// <summary>The categories whose ability makes it cheaper, each with its weight.</summary>
	public IReadOnlyList<(string Category, CFixedPoint Weight)> ResearchBonusFrom { get; init; } =
		Array.Empty<(string, CFixedPoint)>();

	/// <summary>The category that gains when a level is finished, or an empty string.</summary>
	public string OnCompletion { get; init; } = "";

	/// <summary>What a level of it adds to a category's monthly decay modifier, by category.</summary>
	public IReadOnlyList<(string Category, CFixedPoint Amount)> Decay { get; init; } =
		Array.Empty<(string, CFixedPoint)>();

	/// <summary>What must hold for it to be researched at all, or null for nothing.</summary>
	public CResearchCondition? Allow { get; init; }

	/// <summary>The folder of the technology screen it is shown in, or an empty string.</summary>
	public string Folder { get; init; } = "";

	public bool IsSingleLevel => AdditionalOffset == 0;

	/// <summary>The highest level there is to research: what the file says, or one when it does not.</summary>
	public int HighestLevel => MaxLevel > 0 ? MaxLevel : 1;

	public override string ToString()
	{
		return Name;
	}
}
