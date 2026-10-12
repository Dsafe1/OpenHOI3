using System;

namespace OpenHOI3.Content.Script;

/// <summary>
/// How a key's value is read: its shape, the list a name is looked up in, the words it also
/// takes - <c>this</c>, <c>from</c>, <c>owner</c> and the like, matched without case - and, for
/// a block, what the block takes.
/// </summary>
public sealed class CArgumentRule
{
	public CArgumentRule(EArgumentForm form, EScriptName names = EScriptName.None, string[]? words = null,
		CBlockRule? block = null)
	{
		Form = form;
		Names = names;
		Words = words ?? Array.Empty<string>();
		Block = block;
	}

	public EArgumentForm Form { get; }

	public EScriptName Names { get; }

	public string[] Words { get; }

	public CBlockRule? Block { get; }

	public bool TakesWord(string text)
	{
		foreach (string word in Words)
		{
			if (word.Equals(text, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
