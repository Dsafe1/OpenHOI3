using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on the scope's province: its figures, its resource, its name, its cores, who holds it
/// and who owns it: <c>docs/mechanics/effects.md</c>, section 5. None but the building asks whether
/// there is a province, so in a country's scope they change province 0.
/// </summary>
public static class CProvinceEffects
{
	/// <summary>The rebels' tag.</summary>
	public const string Rebels = "REB";

	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		CProvince here = state.Province(scope.Province);
		CScriptArgument argument = effect.Argument;
		CFixedPoint value = argument.IsThis || argument.IsFrom ? CFixedPoint.Zero : argument.Amount;
		switch (effect.Keyword)
		{
			case "change_manpower":
				here.Manpower = Floored(here.Manpower + value);
				Provinces.CProvinceHistory.Record(state, here, Provinces.CProvinceHistory.Manpower, here.Manpower);
				return true;
			case "leadership":
				here.Leadership = Floored(here.Leadership + value);
				Provinces.CProvinceHistory.Record(state, here, Provinces.CProvinceHistory.Leadership, here.Leadership);
				return true;
			case "revolt_risk":
				// Added to the accumulator, never below nought (revolt.md, section 5).
				Revolts.CRevoltRisk.Add(here, value);
				return true;
			case "create_revolt":
				// The province's manpower times N brigades of the type it chooses, in a real area (rebels.md, section 8).
				if (Revolts.CRebelFactions.AreaOf(state, here.Id) is not null)
				{
					Revolts.CRisings.Rise(state, here, Revolts.CRebelChoice.TypeFor(state, here), Revolts.CRisings.Brigades(here, value));
				}
				return true;
			case "strategic_resource":
				// Ours: a resource nobody has does nothing, where the original faults.
				if (argument.IsWord("none"))
				{
					here.StrategicResource = "";
				}
				else if (state.FindStrategicResource(argument.Text) is not null)
				{
					here.StrategicResource = argument.Text;
				}
				return true;
			case "change_province_name":
				here.Name = argument.Text;
				return true;
			case "add_core":
			case "remove_core":
				Core(state, effect, scope, effect.Keyword == "add_core");
				return true;
			case "change_controller":
				if (CEffects.Target(state, effect, scope) is { } controller)
				{
					CProvinceHands.SetController(state, here, controller, consequences: true);
				}
				return true;
			case "secede_province":
				Secede(state, effect, scope, here);
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// The province and the country of a core: a number is that province and the scope's country;
	/// <c>from</c> FROM's province and the scope's country; a tag or <c>this</c> (by owner) that
	/// country and the scope's province.
	/// </summary>
	private static void Core(CGameState state, CEffect effect, CEventScope scope, bool add)
	{
		CScriptArgument argument = effect.Argument;
		int province = argument.Province != 0 ? argument.Province : argument.IsFrom ? scope.FromProvince : scope.Province;
		// REB is the scope's rebel faction's country, and with no faction in the scope the rebels' tag
		// itself, the original's last answer (rebels.md, section 10).
		CCountryTag? country = argument.IsThis ? CConditions.ThisByOwner(state, scope)
			: !argument.Quoted && argument.Text == Rebels ? Revolts.CRebelFactions.Meant(scope)
			: argument.Province == 0 && !argument.IsFrom ? CEventScope.Known(argument.Tag)
			: scope.Country;
		if (country is not { } core)
		{
			return;
		}
		CProvince target = state.Province(province);
		// Each core gained or lost goes into the province's history (savegame.md, section 4).
		if (add && !target.Cores.Contains(core))
		{
			target.Cores.Add(core);
			Provinces.CProvinceHistory.Record(state, target, Provinces.CProvinceHistory.AddCore, core);
		}
		else if (!add && target.Cores.Remove(core))
		{
			Provinces.CProvinceHistory.Record(state, target, Provinces.CProvinceHistory.RemoveCore, core);
		}
	}

	/// <summary>
	/// <c>secede_province</c>: occupied land of the country's is handed back to it, then land the
	/// scope's country owns is given to it.
	/// </summary>
	private static void Secede(CGameState state, CEffect effect, CEventScope scope, CProvince here)
	{
		CScriptArgument argument = effect.Argument;
		// The rebels' country is the scope's rebel faction's (rebels.md, section 10); with none in the scope, nobody.
		CCountryTag? to = argument.IsThis ? CConditions.ThisByOwner(state, scope)
			: argument.IsFrom ? scope.FromCountry
			: !argument.Quoted && argument.Text == Rebels && scope.RebelFaction is not null ? Revolts.CRebelFactions.Meant(scope)
			: CEventScope.Known(argument.Tag);
		if (to is not { } receiver || receiver.Value == Rebels)
		{
			return;
		}
		CCountryTag? giver = scope.Country ?? CConditions.ThisByOwner(state, scope);
		if (here.Controller == giver && here.Owner == receiver)
		{
			CProvinceHands.ChangeOwner(state, here, receiver);
		}
		if (here.Owner == giver)
		{
			CProvinceHands.ChangeOwner(state, here, receiver);
		}
	}

	private static CFixedPoint Floored(CFixedPoint value)
	{
		return value < CFixedPoint.Zero ? CFixedPoint.Zero : value;
	}
}
