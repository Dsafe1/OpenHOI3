using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Research;

/// <summary>
/// One technology, as a file under <c>technologies/</c> declares it. Each level a
/// country has of it is worth the same again of every effect. What researching it takes
/// is in <c>docs/mechanics/research.md</c>, section 1.
/// </summary>
public sealed class CTechnology
{
	/// <summary>The effect that lets a country build convoy escorts, kept as one per level.</summary>
	public const string AllowEscorts = "allow_escorts";

	/// <summary>The effect that gives a country listening stations, kept the same way. Nothing here uses it yet.</summary>
	public const string ListeningStation = "listening_station";

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

	/// <summary>
	/// Its <c>allow</c> block - what must hold for it to be researched at all, asked of the country alone - or null
	/// for none. It is read as script once everything it may name is loaded, as the original reads it with the
	/// whole condition language (<c>docs/mechanics/research.md</c>, section 5).
	/// </summary>
	public CTrigger? Allow { get; set; }

	/// <summary>
	/// Whether its <c>allow</c> block holds anything at all, understood here or not.
	/// A default historical model leaves out a one-level technology that has one.
	/// </summary>
	public bool HasPrecondition { get; init; }

	/// <summary>The folder of the technology screen it is shown in, or an empty string.</summary>
	public string Folder { get; init; } = "";

	/// <summary>
	/// Whether a brigade has to be re-equipped to gain a level of it. A doctrine says
	/// no, and reaches every brigade as it is.
	/// </summary>
	public bool Change { get; init; } = true;

	/// <summary>
	/// Whether a brigade that is behind in it is ever brought up. One that says no is
	/// only ever had by a brigade built with it.
	/// </summary>
	public bool CanUpgrade { get; init; } = true;

	/// <summary>Whether spies may steal it: unless its file says <c>stealable = no</c> (<c>espionage.md</c>, section 8).</summary>
	public bool Stealable { get; init; } = true;

	/// <summary>
	/// The earlier of two declarations of one name: a technology of its own in the original, whose name finds the later, so
	/// reached only through the unit types it names and saved under the name at nought (<c>research.md</c>, section 1).
	/// Held off <see cref="CGameState.Technologies"/> and <see cref="CGameState.TechnologiesInOrder"/>.
	/// </summary>
	public bool IsShadowed { get; init; }

	/// <summary>
	/// The unit types a level of it gives the country the use of, by key: each
	/// <c>activate_unit</c> of its block.
	/// </summary>
	public IReadOnlyList<string> ActivatesUnits { get; init; } = Array.Empty<string>();

	/// <summary>
	/// The building a level of it allows, by key - its <c>activate_building</c> - or empty. The
	/// original keeps one; where a block names more, the last is kept.
	/// </summary>
	public string ActivatesBuilding { get; init; } = "";

	/// <summary>What a level of it adds to each unit type, by the type's key.</summary>
	public Dictionary<string, CSubUnitStats> UnitEffects { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What a level of it adds to a mission's efficiency, by the mission's name: the
	/// <c>efficiency</c> of a block named after the mission. See
	/// <c>docs/mechanics/navalmissions.md</c>, section 10.
	/// </summary>
	public Dictionary<string, CFixedPoint> MissionEfficiency { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The <c>reduction_modifier</c> of the same blocks. The original reads it in one
	/// place and throws the figure away, so it is kept here and does nothing.
	/// </summary>
	public Dictionary<string, CFixedPoint> MissionReduction { get; } = new(StringComparer.OrdinalIgnoreCase);

	public bool IsSingleLevel => AdditionalOffset == 0;

	/// <summary>The highest level there is to research: what the file says, or one when it does not.</summary>
	public int HighestLevel => MaxLevel > 0 ? MaxLevel : 1;

	public override string ToString()
	{
		return Name;
	}
}
