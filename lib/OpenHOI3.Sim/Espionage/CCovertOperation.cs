using System.Collections.Generic;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// One of <c>common/covert_ops.txt</c>'s operations a country's spies may carry out in a province: its cost in whole
/// covert-ops points, when it may be chosen and what it does. See <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public sealed class CCovertOperation
{
	public CCovertOperation(string name)
	{
		Name = name;
	}

	public string Name { get; }

	/// <summary>Whole covert-ops points, about the province's controller.</summary>
	public int Cost { get; set; }

	/// <summary>Read and kept; who asks it in the original was not found.</summary>
	public CTrigger IsValid { get; } = CTrigger.All();

	/// <summary>What the province window asks before the operation may be chosen, where it says anything.</summary>
	public CTrigger IsSelectable { get; } = CTrigger.All();

	/// <summary>Run with the province as the scope and FROM the country that sent its spies.</summary>
	public List<CEffect> Effects { get; } = new();
}
