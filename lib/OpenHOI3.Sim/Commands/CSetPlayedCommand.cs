using System;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country taken up by a player in play, or left to the AI: a player of a game between several machines who drops,
/// and one who comes back and chooses among the countries nobody plays (<c>docs/multiplayer.md</c>, sections 2 and
/// 4.1). *Ours* - the original changes who plays a country in its lobby alone. The host posts it; the console's
/// <c>tag</c> does the same two things (<see cref="CCheatCommand"/>).
/// </summary>
public sealed class CSetPlayedCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly bool _played;

	public CSetPlayedCommand(CCountryTag country, bool played)
	{
		_country = country;
		_played = played;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && state.Played.Contains(_country) != _played;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		if (_played)
		{
			Take(state, country);
		}
		else
		{
			Leave(state, country);
		}
	}

	/// <summary>
	/// A country a player takes up: the AI no longer runs its units, and every headquarters the AI ran is let go - its
	/// plan switched off, by what the game holds of it - as a player who wants one run for him hands it over himself.
	/// </summary>
	public static void Take(CGameState state, CCountry country)
	{
		state.Played.Add(country.Tag);
		country.UnitAi.RunsUnits = false;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				CAgents.LetGo(state, headquarters);
			}
		}
		country.UnitAi.Agents.Clear();
	}

	/// <summary>
	/// A country left to the AI: it runs its units again, and its ministers, whose countdowns stood at nought while it was
	/// played, start again at their next hour.
	/// </summary>
	public static void Leave(CGameState state, CCountry country)
	{
		state.Played.Remove(country.Tag);
		country.UnitAi.RunsUnits = true;
		country.PoliticsCountdown = Math.Max(country.PoliticsCountdown, 1);
		country.IntelligenceCountdown = Math.Max(country.IntelligenceCountdown, 1);
		country.ForeignCountdown = Math.Max(country.ForeignCountdown, 1);
		country.ProductionCountdown = Math.Max(country.ProductionCountdown, 1);
		country.TechCountdown = Math.Max(country.TechCountdown, 1);
	}
}
