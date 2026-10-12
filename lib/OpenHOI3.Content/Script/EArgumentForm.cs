namespace OpenHOI3.Content.Script;

/// <summary>
/// The shapes a key's value can take: <c>docs/mechanics/script.md</c>, sections 5 and 6.
/// </summary>
public enum EArgumentForm
{
	/// <summary>The word <c>yes</c>; anything else is false.</summary>
	Yes,

	/// <summary>A whole number, its decimals cut.</summary>
	Whole,

	/// <summary>A number to three places.</summary>
	Number,

	/// <summary>Anything, kept as written.</summary>
	Text,

	/// <summary><c>year.month.day</c>, an hour after it optional.</summary>
	Date,

	/// <summary>A country's tag.</summary>
	Tag,

	/// <summary>A province's number.</summary>
	Province,

	/// <summary>A province's number, or else a country's tag.</summary>
	ProvinceOrTag,

	/// <summary>A name out of one of the lists.</summary>
	Name,

	/// <summary>A name out of one of the lists, or else a whole number.</summary>
	NameOrWhole,

	/// <summary>An event's id.</summary>
	Event,

	/// <summary>Anything: the original does not read it.</summary>
	Ignored,

	/// <summary>A block: of conditions or effects, as the keyword's language is, and its own keys.</summary>
	Block,
}
