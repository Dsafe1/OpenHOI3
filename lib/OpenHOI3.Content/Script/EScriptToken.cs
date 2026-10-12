namespace OpenHOI3.Content.Script;

/// <summary>The kinds of word a script file is cut into: <c>docs/mechanics/script.md</c>, section 2.</summary>
public enum EScriptToken
{
	/// <summary>A bare word, which is one of the language's words whatever its case.</summary>
	Word,

	/// <summary>A word in quotes, which never is.</summary>
	Quoted,

	Equals,

	Open,

	Close,

	/// <summary><c>(</c>, <c>)</c> or <c>,</c>, which nothing in this language uses.</summary>
	Punctuation,
}
