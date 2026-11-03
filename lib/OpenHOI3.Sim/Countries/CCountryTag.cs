namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A country's three-letter tag, the key everything else hangs off: history files,
/// flags, colours and every reference in the scenario data.
///
/// Like the date type, the original has no RTTI for this, so the name follows the
/// mangled luabind signatures in the binary, which spell it CCountryTag.
///
/// Compared and hashed by its letters packed into a number when it is made: everything is keyed by tag, and a string's
/// own comparison and hash were a tenth of an hour. A text that is not three plain characters is compared as a string.
/// </summary>
public readonly struct CCountryTag : System.IEquatable<CCountryTag>
{
	public const int Length = 3;

	private readonly int _code;

	public CCountryTag(string value)
	{
		Value = value;
		_code = Pack(value);
	}

	public string Value { get; }

	public bool Equals(CCountryTag other)
	{
		return _code == other._code && (_code != 0 || string.Equals(Value, other.Value, System.StringComparison.Ordinal));
	}

	public override bool Equals(object? obj) => obj is CCountryTag other && Equals(other);

	public override int GetHashCode() => _code != 0 ? _code : Value?.GetHashCode() ?? 0;

	public static bool operator ==(CCountryTag left, CCountryTag right) => left.Equals(right);

	public static bool operator !=(CCountryTag left, CCountryTag right) => !left.Equals(right);

	/// <summary>Three characters below 128 as one number, never nought; nought for anything else.</summary>
	private static int Pack(string? value)
	{
		if (value is not { Length: Length } || value[0] >= 128 || value[1] >= 128 || value[2] >= 128 || value[0] == 0)
		{
			return 0;
		}
		return value[0] << 16 | value[1] << 8 | value[2];
	}

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
