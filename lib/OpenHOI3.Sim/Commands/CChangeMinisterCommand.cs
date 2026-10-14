using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A minister into a cabinet post: posted by the politics screen, the AI's Lua and its weekly filling of empty posts.
/// It checks nothing but that the country, the minister and the post are real, and does nothing but put him there and
/// tell the player; his modifier arrives with the next day's. See <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public sealed class CChangeMinisterCommand : CCommand
{
	public const string AppointedMessage = "MINISTER_APPOINTED";

	public CChangeMinisterCommand(CCountryTag country, int minister, string position)
	{
		Country = country;
		Minister = minister;
		Position = position;
	}

	public CCountryTag Country { get; }

	/// <summary>The minister, by the number his country's file gives him.</summary>
	public int Minister { get; }

	public string Position { get; }

	public override bool MayExecute(CGameState state) => Find(state) is not null;

	public override void Execute(CGameState state)
	{
		if (Find(state) is not ({ } country, { } minister))
		{
			return;
		}
		country.Government[Position] = minister;
		if (state.Played.Contains(country.Tag))
		{
			state.RaisePactMessage(new Agreements.CPactMessage(AppointedMessage, "", "", country.Tag, country.Tag)
			{
				Told = country.Tag,
				Values = new[]
				{
					("MINISTERNAME", minister.Name),
					("PERSONALITY_DESC", minister.TypeIn(Position)?.Name ?? ""),
					("POSITION", Position),
				},
			});
		}
	}

	private (CCountry Country, CMinister Minister)? Find(CGameState state)
	{
		if (state.Country(Country) is not { } country || !country.Ministers.TryGetValue(Minister, out CMinister? minister))
		{
			return null;
		}
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (position.Name == Position)
			{
				return (country, minister);
			}
		}
		return null;
	}
}
