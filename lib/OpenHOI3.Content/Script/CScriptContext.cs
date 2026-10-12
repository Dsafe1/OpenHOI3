using System.Collections.Generic;

namespace OpenHOI3.Content.Script;

/// <summary>
/// What the reading of script carries from file to file: the names it checks against, the
/// problems found, and the events referred to by id, which are checked once every file is read.
/// </summary>
public sealed class CScriptContext
{
	public CScriptContext(CScriptNames names)
	{
		Names = names;
	}

	public CScriptNames Names { get; }

	public CScriptProblems Problems { get; } = new();

	/// <summary>Each <c>country_event</c> and <c>province_event</c> effect's id, where it was written.</summary>
	public List<(string File, int Line, int Id)> EventReferences { get; } = new();
}
