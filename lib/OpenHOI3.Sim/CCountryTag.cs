namespace OpenHOI3.Sim;

/// <summary>
/// A country's three-letter tag, the key everything else hangs off: history files,
/// flags, colours and every reference in the scenario data.
///
/// Like the date type, the original has no RTTI for this, so the name follows the
/// mangled luabind signatures in the binary, which spell it CCountryTag.
/// </summary>
public readonly record struct CCountryTag(string Value)
{
	public const int Length = 3;

	public bool IsValid => Value is { Length: Length };

	public static bool TryParse(string? text, out CCountryTag tag)
	{
		tag = default;
		if (text is null)
		{
			return false;
		}
		string trimmed = text.Trim();
		if (trimmed.Length != Length)
		{
			return false;
		}
		foreach (char c in trimmed)
		{
			// Tags are upper case letters, with the odd digit or underscore.
			if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_')
			{
				return false;
			}
		}
		tag = new CCountryTag(trimmed);
		return true;
	}

	public override string ToString()
	{
		return Value ?? "";
	}
}
