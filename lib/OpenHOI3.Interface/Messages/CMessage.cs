using System.Collections.Generic;
using OpenHOI3.Content.Messages;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// One message: its type, the date it was made on, its named values and the countries and province
/// it is about. Its words are its type's, filled with the values when shown. See
/// <c>docs/mechanics/messages.md</c>.
/// </summary>
public sealed class CMessage
{
	public CMessage(CMessageType type, string date)
	{
		Type = type;
		Date = date;
	}

	public CMessageType Type { get; }

	/// <summary>" 13:00, 1 January, 1936" - the hour it was made, a space first.</summary>
	public string Date { get; }

	/// <summary>The named values, in order; each is looked up as a key where it is shown.</summary>
	public List<(string Key, string Value)> Variables { get; } = new();

	public CCountryTag? FirstCountry { get; init; }

	public CCountryTag? SecondCountry { get; init; }

	public int Province { get; init; }

	/// <summary>An election's parties' scores, by ideology.</summary>
	public long[]? Scores { get; init; }

	public override string ToString()
	{
		return $"{Type.Name}{Date}";
	}
}
