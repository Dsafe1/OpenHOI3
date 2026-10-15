using System.Collections.Generic;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// One kind of rising out of <c>common/rebel_types.txt</c>: what makes it the one a province chooses, where its armies
/// march, what it frees and what it is made of. See <c>docs/mechanics/rebels.md</c>, section 2.
/// </summary>
public sealed class CRebelType
{
	public CRebelType(string name, int index)
	{
		Name = name;
		Index = index;
		NameText = name + "_name";
		ArmyText = name + "_army";
		DescText = name + "_desc";
	}

	/// <summary>Its key in the file: its texts are <c>&lt;name&gt;_title</c>, <c>_name</c>, <c>_army</c> and <c>_desc</c>.</summary>
	public string Name { get; }

	/// <summary>Its place in the file, from 0.</summary>
	public int Index { get; }

	/// <summary>What the type scores in a province, the highest rising (section 4).</summary>
	public CMeanTimeToHappen SpawnChance { get; } = new();

	/// <summary>What a province scores as somewhere for an army to march (section 9).</summary>
	public CMeanTimeToHappen MovementEvaluation { get; } = new();

	/// <summary><c>area</c> other than <c>nation</c>: the rebels may go beyond their country's land, and outlive it.</summary>
	public bool AnyArea { get; set; }

	/// <summary>The ideology whose governments a faction prefers, or none - <c>any</c> among them (*inferred*).</summary>
	public string? Ideology { get; set; }

	/// <summary><c>defection</c> other than <c>none</c>.</summary>
	public bool Defection { get; set; }

	/// <summary><c>independence</c> other than <c>none</c>: a rising frees a country.</summary>
	public bool Independence { get; set; }

	/// <summary>Whole months a province is held before it counts towards independence.</summary>
	public int DefectDelay { get; set; }

	/// <summary>The brigade type the rebels are made of, by name.</summary>
	public string Unit { get; set; } = "";

	public bool Resilient { get; set; }

	public bool Reinforcing { get; set; }

	public bool Smart { get; set; }

	/// <summary>Partisans of an exile's land go to the exile.</summary>
	public bool AutoConvert { get; set; }

	/// <summary>Only an underground's rising may be of it.</summary>
	public bool IsTriggeredOnly { get; set; }

	/// <summary>Asked of a province the rebels take, with the faction in the scope.</summary>
	public CTrigger SiegeWonTrigger { get; set; } = CTrigger.All();

	public List<CEffect> SiegeWonEffect { get; } = new();

	/// <summary>Read, and never asked.</summary>
	public CTrigger CanEnforceTrigger { get; set; } = CTrigger.All();

	/// <summary>Read, and never run.</summary>
	public List<CEffect> DemandsEnforcedEffect { get; } = new();

	/// <summary>
	/// <c>&lt;name&gt;_name</c>, <c>_army</c> and <c>_desc</c> as the game's texts have them, which the simulation does
	/// not read: whoever loads the game hands them over, and a text not handed over is its key.
	/// </summary>
	public string NameText { get; set; } = "";

	public string ArmyText { get; set; } = "";

	public string DescText { get; set; } = "";

	public override string ToString() => Name;
}
