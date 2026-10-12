using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// The problems found reading event and decision script, file by file. The reader says which
/// file it is in and each problem is filed under it.
/// </summary>
public sealed class CScriptProblems
{
	private readonly List<CScriptProblem> _all = new();

	/// <summary>The file being read, relative to the install.</summary>
	public string File { get; set; } = "";

	public IReadOnlyList<CScriptProblem> All => _all;

	public int Count => _all.Count;

	public void Add(EScriptProblem kind, int line, string message)
	{
		_all.Add(new CScriptProblem(kind, File, line, message));
	}

	/// <summary>How many there are of each kind.</summary>
	public SortedDictionary<EScriptProblem, int> CountByKind()
	{
		var counts = new SortedDictionary<EScriptProblem, int>();
		foreach (CScriptProblem problem in _all)
		{
			counts[problem.Kind] = counts.GetValueOrDefault(problem.Kind) + 1;
		}
		return counts;
	}
}
