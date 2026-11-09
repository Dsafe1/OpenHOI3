using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country with no land made a puppet out of land of its cores the overlord owns: released, tied, its relation raised, under the
/// overlord's government and in its faction. See <c>docs/mechanics/politics.md</c>.
/// </summary>
public sealed class CCreateVassalCommand : CCommand
{
	private const string Rebels = "REB";

	private readonly CCountryTag _vassal;
	private readonly CCountryTag _overlord;

	public CCreateVassalCommand(CCountryTag vassal, CCountryTag overlord)
	{
		_vassal = vassal;
		_overlord = overlord;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_overlord) is not null && state.Country(_vassal) is not null && CCountryLists.OwnedCount(state, _vassal) == 0;
	}

	public override void Execute(CGameState state)
	{
		CCountry overlord = state.Country(_overlord)!;
		CCountry vassal = state.Country(_vassal)!;
		// The overlord's capital stays, and land the rebels hold goes with the rest.
		var given = new List<CProvince>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Cores.Contains(_vassal) && province.Id != overlord.Capital && province.Owner == _overlord
				&& (province.Controller == _overlord || province.Controller?.Value == Rebels))
			{
				given.Add(province);
			}
		}
		CRelease.Release(state, given, _vassal);
		CPuppets.Tie(state, overlord, vassal);
		if (COfferRules.RealFaction(overlord))
		{
			CFactions.AddCountry(state, vassal, overlord.Faction, joinWars: true);
		}
		CLaws.OnDiplomacyChanged(state, vassal);
		CLaws.OnDiplomacyChanged(state, overlord);
	}
}
