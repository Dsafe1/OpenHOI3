using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A government in exile given back the land it owns that its liberator's faction holds. Across factions it becomes the liberator's
/// puppet; it stops being in exile. Its owners and cores do not change. See <c>docs/mechanics/politics.md</c>.
/// </summary>
public sealed class CLiberateCountryCommand : CCommand
{
	private readonly CCountryTag _liberated;
	private readonly CCountryTag _liberator;

	public CLiberateCountryCommand(CCountryTag liberated, CCountryTag liberator)
	{
		_liberated = liberated;
		_liberator = liberator;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_liberator) is not null && state.Country(_liberated) is { InExile: true };
	}

	public override void Execute(CGameState state)
	{
		CCountry liberator = state.Country(_liberator)!;
		CCountry liberated = state.Country(_liberated)!;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == _liberated && state.Country(province.Controller)?.Faction == liberator.Faction)
			{
				CProvinceHands.SetController(state, province, _liberated, consequences: true, peaceful: true);
			}
		}
		if (liberator.Faction != liberated.Faction)
		{
			CPuppets.Tie(state, liberator, liberated);
			if (COfferRules.RealFaction(liberator))
			{
				CFactions.AddCountry(state, liberated, liberator.Faction, joinWars: true);
			}
		}
		state.RaisePactMessage(new CPactMessage("WE_RETURN_EXILE", "RETURN_EXILE", "RETURN_EXILE", _liberator, _liberated)
		{
			Values = new[] { ("FREE", liberated.Name), ("FROM", liberator.Name), ("FCAPITAL", liberated.Capital != 0 ? state.Province(liberated.Capital).Name : "") },
		});
		CExile.Leave(state, liberated);
		CLaws.OnDiplomacyChanged(state, liberated);
		CLaws.OnDiplomacyChanged(state, liberator);
	}
}
