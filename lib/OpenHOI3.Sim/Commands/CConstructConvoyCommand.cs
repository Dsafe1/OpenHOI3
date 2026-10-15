using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Transports or escorts ordered: the original's <c>CConstructConvoyCommand</c>. Always valid - not even escorts without
/// <c>allow_escorts</c> are refused - it makes a new line of the size asked at the end of the queue, priced once. Nothing
/// is paid and nothing is said. See <c>docs/mechanics/convoylines.md</c>, section 1.
/// </summary>
public sealed class CConstructConvoyCommand : CCommand
{
	public CConstructConvoyCommand(CCountryTag country, bool escorts, int amount)
	{
		Country = country;
		Escorts = escorts;
		Amount = amount;
	}

	public CCountryTag Country { get; }

	/// <summary>Escorts, or transports.</summary>
	public bool Escorts { get; }

	/// <summary>How many items one after another: the line's size.</summary>
	public int Amount { get; }

	public override bool MayExecute(CGameState state)
	{
		return state.Country(Country) is not null;
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(Country) is not { } country)
		{
			return;
		}
		CConstruction line = CConvoyLines.NewLine(state, country, Escorts, Amount);
		line.Id = ++state.LastUnitId;
		country.Constructions.Add(line);
		CBuildQueue.UpdateCost(country);
	}
}
