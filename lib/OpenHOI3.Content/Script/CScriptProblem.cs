namespace OpenHOI3.Content.Script;

/// <summary>One problem found reading script, with the file, relative to the install, and the line.</summary>
public readonly record struct CScriptProblem(EScriptProblem Kind, string File, int Line, string Message)
{
	public override string ToString()
	{
		return $"{File}:{Line}: {Message}";
	}
}
