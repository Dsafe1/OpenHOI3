using System.Collections.Generic;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A casus belli type of <c>common/cb_types.txt</c>: its number - from one, in the file's order, nought
/// being the null type, which no test sees as real - its blocks of conditions and effects, and the keys
/// the original reads. The keys it parses and never uses are not kept. See <c>docs/mechanics/war.md</c>,
/// section 2.
/// </summary>
public sealed class CCasusBelliType
{
	public CCasusBelliType(string name, int index)
	{
		Name = name;
		Index = index;
	}

	/// <summary>The null type: number nought, no name, never real.</summary>
	public static CCasusBelliType Null { get; } = new("", 0);

	public string Name { get; }

	public int Index { get; }

	/// <summary>Every type but the null one.</summary>
	public bool IsReal => Index > 0;

	/// <summary>Its place in <c>peace_order</c>, from nought; nought where the list leaves it out.</summary>
	public int Rank { get; set; }

	/// <summary>The frame of the goal's icon.</summary>
	public int SpriteIndex { get; set; }

	/// <summary>Conditions asked in the target's scope with THIS the holder.</summary>
	public CTrigger IsValid { get; } = CTrigger.All();

	public CTrigger AllowedCountries { get; } = CTrigger.All();

	public CTrigger AllowedRegions { get; } = CTrigger.All();

	public CTrigger AllowedProvince { get; } = CTrigger.All();

	/// <summary>Threat spread when the goal is enforced.</summary>
	public CFixedPoint Threat { get; set; }

	/// <summary>Usable without being held.</summary>
	public bool Always { get; set; }

	/// <summary>A goal of it does not make peace when enforced.</summary>
	public bool HandlesPeace { get; set; }

	/// <summary>Its <c>po_*</c> keys set to yes, in the file's order.</summary>
	public List<string> PeaceOptions { get; } = new();

	/// <summary>Whether a goal of it annexes, <c>po_annex</c>: such goals are enforced after every other.</summary>
	public bool Annexes => PeaceOptions.Contains(CWarGoalEnforcement.Annex);

	public bool IsCivilWar { get; set; }

	/// <summary>Effects run when a goal of it is added, in the actor's scope.</summary>
	public List<CEffect> OnAdd { get; } = new();

	/// <summary>Effects run when the goal is enforced.</summary>
	public List<CEffect> OnCompletion { get; } = new();

	public override string ToString() => IsReal ? Name : "(null type)";
}
