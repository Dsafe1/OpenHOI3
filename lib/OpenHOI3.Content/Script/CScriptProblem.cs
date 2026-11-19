namespace OpenHOI3.Content.Script;

/// <summary>One problem found reading script, with the file, relative to the install, and the line.</summary>
public readonly record struct CScriptProblem(EScriptProblem Kind, string File, int Line, string Message)
{
	public override string ToString()
	{
		return $"{File}:{Line}: {Message}";
	}

	/// <summary>Its line in the validation log: where, what, and its kind in brackets (<c>docs/mechanics/logs.md</c>, section 4).</summary>
	public string Report => CDiagnostics.Located(File, Line, $"{Message} [{Kind}]");

	/// <summary>Whether a reported line is a script problem's <see cref="Report"/>: one ending in a kind in brackets.</summary>
	public static bool IsReport(string message)
	{
		foreach (string kind in System.Enum.GetNames<EScriptProblem>())
		{
			if (message.EndsWith($" [{kind}]", System.StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}
}
