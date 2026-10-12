namespace OpenHOI3.Content.Script;

/// <summary>The kinds of problem reading script reports: <c>docs/mechanics/script.md</c>, section 9.</summary>
public enum EScriptProblem
{
	/// <summary>A key nothing in its place takes.</summary>
	UnknownKey,

	/// <summary>A name, an id or a tag nothing has.</summary>
	UnknownName,

	/// <summary>A name that is there only in another case, which the original does not find.</summary>
	OtherCase,

	/// <summary>A value of the wrong shape: a block for one value, a word for a number, cut decimals.</summary>
	WrongShape,

	/// <summary>A key with no <c>=</c>, a brace too many or too few.</summary>
	Structure,

	/// <summary>Something the original does that the file is unlikely to have meant.</summary>
	Quirk,
}
