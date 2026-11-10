using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// How good a country is at a mission: what its technologies give it - each one's
/// <c>efficiency</c> for the mission times the level the country has of it - and the
/// country modifier <c>&lt;mission&gt;_eff</c>. It is put on the attack of every ship of a
/// fleet under the order, and it sets an escort or a patrol against a raider when one
/// looks for the other. See <c>docs/mechanics/navalmissions.md</c>, section 10.
/// </summary>
public static class CMissionEfficiency
{
	/// <summary>What the country modifier of a mission is called: its name and this.</summary>
	public const string ModifierSuffix = "_eff";

	/// <summary>
	/// The names the original knows its missions by, which are what a technology's block
	/// and a savegame call them, in the original's own order.
	/// </summary>
	public static readonly IReadOnlyList<string> Names = new[]
	{
		"support_attack", "strategic_redeployment", "move_order", "rebase", "reserves", "patrol", "intercept",
		"sortie", "convoy_escort", "convoy_raid", "air_convoy_raid", "transport", "invasion", "strategic_bomb",
		"logistical_strike", "runway_cratering", "installation_strike", "ground_attack", "interdiction",
		"air_intercept", "carrier_protection", "paradrop_mission", "port_strike", "naval_strike",
		"rebase_to_carrier", "nuke_mission", "transport_supplies_mission", "rebase_air", "air_reserve",
		"air_superiority", "join_fleet", "join_air",
	};

	/// <summary>The original's name for an order, or an empty string for none.</summary>
	public static string NameOf(EUnitOrder order)
	{
		return order switch
		{
			EUnitOrder.Move => "move_order",
			EUnitOrder.Rebase => "rebase",
			EUnitOrder.RebaseAir => "rebase_air",
			EUnitOrder.Reserve => "reserves",
			EUnitOrder.Patrol => "patrol",
			EUnitOrder.Intercept => "intercept",
			EUnitOrder.Sortie => "sortie",
			EUnitOrder.ConvoyEscort => "convoy_escort",
			EUnitOrder.ConvoyRaid => "convoy_raid",
			EUnitOrder.AirSuperiority => "air_superiority",
			EUnitOrder.AirIntercept => "air_intercept",
			EUnitOrder.CarrierProtection => "carrier_protection",
			EUnitOrder.Paradrop => "paradrop_mission",
			EUnitOrder.RebaseCarrier => "rebase_to_carrier",
			EUnitOrder.AirSupply => "transport_supplies_mission",
			EUnitOrder.SupportAttack => "support_attack",
			EUnitOrder.StrategicRedeployment => "strategic_redeployment",
			EUnitOrder.Transport => "transport",
			EUnitOrder.Invasion => "invasion",
			EUnitOrder.StrategicBombing => "strategic_bomb",
			EUnitOrder.LogisticalStrike => "logistical_strike",
			EUnitOrder.RunwayCratering => "runway_cratering",
			EUnitOrder.InstallationStrike => "installation_strike",
			EUnitOrder.GroundAttack => "ground_attack",
			EUnitOrder.Interdiction => "interdiction",
			EUnitOrder.PortStrike => "port_strike",
			EUnitOrder.NavalStrike => "naval_strike",
			EUnitOrder.Nuke => "nuke_mission",
			EUnitOrder.JoinFleet => "join_fleet",
			EUnitOrder.JoinAir => "join_air",
			_ => "",
		};
	}

	/// <summary>A country's efficiency at the mission of an order, in thousandths. Nothing for no order.</summary>
	public static long Of(CGameState state, CCountry? country, EUnitOrder order)
	{
		string name = NameOf(order);
		if (country is null || name.Length == 0)
		{
			return 0;
		}
		long efficiency = country.Modifier[name + ModifierSuffix].Raw;
		foreach ((string technologyName, int level) in country.TechnologyLevels)
		{
			if (level > 0 && state.Technologies.TryGetValue(technologyName, out CTechnology? technology)
				&& technology.MissionEfficiency.TryGetValue(name, out CFixedPoint perLevel))
			{
				efficiency += perLevel.Raw * level;
			}
		}
		return efficiency;
	}
}
