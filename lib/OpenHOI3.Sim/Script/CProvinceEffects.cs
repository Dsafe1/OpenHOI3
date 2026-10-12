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
				return true;
			case "leadership":
				here.Leadership = Floored(here.Leadership + value);
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
					SetController(state, here, controller);
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
		// REB is the scope's rebel faction's country, and with rebel factions not built it is the
		// rebels' tag itself, the original's last answer.
		CCountryTag? country = argument.IsThis ? CConditions.ThisByOwner(state, scope)
			: !argument.Quoted && argument.Text == Rebels ? new CCountryTag(Rebels)
			: argument.Province == 0 && !argument.IsFrom ? CEventScope.Known(argument.Tag)
			: scope.Country;
		if (country is not { } core)
		{
			return;
		}
		CProvince target = state.Province(province);
		if (add && !target.Cores.Contains(core))
		{
			target.Cores.Add(core);
		}
		else if (!add)
		{
			target.Cores.Remove(core);
		}
	}

	/// <summary>
	/// <c>secede_province</c>: occupied land of the country's is handed back to it, then land the
	/// scope's country owns is given to it.
	/// </summary>
	private static void Secede(CGameState state, CEffect effect, CEventScope scope, CProvince here)
	{
		CScriptArgument argument = effect.Argument;
		// The rebels' country is the scope's rebel faction's, which is not built.
		CCountryTag? to = argument.IsThis ? CConditions.ThisByOwner(state, scope)
			: argument.IsFrom ? scope.FromCountry
			: CEventScope.Known(argument.Tag);
		if (to is not { } receiver || receiver.Value == Rebels)
		{
			return;
		}
		CCountryTag? giver = scope.Country ?? CConditions.ThisByOwner(state, scope);
		if (here.Controller == giver && here.Owner == receiver)
		{
			ChangeOwner(state, here, receiver);
		}
		if (here.Owner == giver)
		{
			ChangeOwner(state, here, receiver);
		}
	}

	/// <summary>
	/// A province's controller set as the original's setter does what is built of it: nothing where
	/// it already is, nor for land nobody owns.
	/// </summary>
	public static void SetController(CGameState state, CProvince province, CCountryTag controller)
	{
		if (province.Controller == controller || province.Owner is null)
		{
			return;
		}
		province.Controller = controller;
		state.ControlChanges++;
	}

	/// <summary>
	/// A province changes owner: the new owner holds it too, the modifiers events gave it go, and
	/// the old owner, owning nothing now, leaves its faction.
	/// </summary>
	public static void ChangeOwner(CGameState state, CProvince province, CCountryTag owner)
	{
		CCountryTag? old = province.Owner;
		if (province.Controller != owner)
		{
			province.Controller = owner;
			state.ControlChanges++;
		}
		province.Owner = owner;
		if (province.EventModifiers.Count > 0)
		{
			province.EventModifiers.Clear();
			CProvinceModifiers.Rebuild(state);
		}
		if (state.Country(old) is { } former && CCountryLists.OwnedCount(state, former.Tag) == 0)
		{
			former.Faction = "";
		}
	}

	private static CFixedPoint Floored(CFixedPoint value)
	{
		return value < CFixedPoint.Zero ? CFixedPoint.Zero : value;
	}
}
