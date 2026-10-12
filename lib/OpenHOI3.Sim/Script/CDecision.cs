using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// A decision as read: <c>docs/mechanics/script.md</c>, section 8. How one is taken is not
/// built yet.
/// </summary>
public sealed class CDecision
{
	public CDecision(string name, EDecisionGroup group, string file, int line)
	{
		Name = name;
		Group = group;
		File = file;
		Line = line;
	}

	public string Name { get; }

	public EDecisionGroup Group { get; }

	/// <summary>The file it was read from, relative to the install, and the line it starts on.</summary>
	public string File { get; }

	public int Line { get; }

	/// <summary>When it is shown at all; an <c>and</c>.</summary>
	public CTrigger Potential { get; } = CTrigger.All();

	/// <summary>When it may be taken; an <c>and</c>.</summary>
	public CTrigger Allow { get; } = CTrigger.All();

	public List<CEffect> Effects { get; } = new();

	public CMeanTimeToHappen AiWillDo { get; } = new();

	public bool Major { get; set; }
}
