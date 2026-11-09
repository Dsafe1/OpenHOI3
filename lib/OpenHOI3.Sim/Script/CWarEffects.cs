using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on wars and casus belli: <c>war</c>, <c>add_wargoal</c>, <c>casus_belli</c> and
/// <c>add_casus_belli</c>. See <c>docs/mechanics/war.md</c>, section 9.
/// </summary>
public static class CWarEffects
{
	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		switch (effect.Keyword)
		{
			case "war":
				War(state, effect, scope);
				return true;
			case "split_troops":
				// THIS hands units and manpower to the scope's country (effects.md, section 9).
				Units.CSplitTroops.Run(state, effect, scope);
				return true;
			case "add_wargoal":
				if (state.Country(scope.Country) is { } actor && state.Country(WarTarget(state, effect, scope)) is { } target
					&& CWarSides.Between(state, actor.Tag, target.Tag) is { } war && Goal(state, effect, "war_goal", actor, target) is { } goal)
				{
					CWarGoals.AddToWar(state, goal, war);
				}
				return true;
			case "casus_belli":
				// The scope is given one on TAG.
				if (state.Country(scope.Country) is { } holder && state.Country(Inline(state, effect, scope)) is { } on)
				{
					CCasusBellis.Give(state, holder, on);
				}
				return true;
			case "add_casus_belli":
				// The other way round: TAG is given one on the scope.
				if (state.Country(scope.Country) is { } against && state.Country(Inline(state, effect, scope)) is { } given)
				{
					CCasusBellis.Give(state, given, against);
				}
				return true;
			case "load_oob":
				// A file of units read into the scope's country, placed at once (units.md, section 3).
				if (state.Country(scope.Country) is { } loaded && effect.Argument.Text.Length > 0
					&& (CCountryLists.OwnedCount(state, loaded.Tag) > 0 || loaded.Tag.Value == "REB"))
				{
					loaded.OrderOfBattleFile = effect.Argument.Text;
					state.LoadOrderOfBattle?.Invoke(loaded, effect.Argument.Text);
				}
				return true;
			case "inherit":
			case "surrender_inherit":
				// The scope annexes TAG, unguarded: itself not refused (war.md, section 13).
				if (state.Country(scope.Country) is { } annexer && state.Country(Inline(state, effect, scope)) is { } annexed)
				{
					CAnnexation.Annex(state, annexer, annexed);
				}
				return true;
			case "form_government_in_exile":
				// With a FROM, the scope's land on FROM's capital's continent or beside FROM's goes to FROM where FROM is
				// at war with its owner, otherwise to its owner; then the exile begins or ends (war.md, section 14).
				if (state.Country(scope.Country) is { } exile)
				{
					if (state.Country(scope.FromCountry) is { } host)
					{
						CExile.HandLand(state, exile, host);
					}
					if (effect.Argument.IsYes)
					{
						CExile.Enter(state, exile);
					}
					else
					{
						CExile.Leave(state, exile);
					}
				}
				return true;
			case "release":
				// TAG is given every one of its cores the scope's country owns, and need not exist; none, nothing
				// (rebels.md, section 8).
				if (scope.Country is { } giver && Inline(state, effect, scope) is { } freed)
				{
					var cores = new System.Collections.Generic.List<Provinces.CProvince>();
					foreach (Provinces.CProvince province in state.Provinces)
					{
						if (province.Owner == giver && province.Cores.Contains(freed))
						{
							cores.Add(province);
						}
					}
					CRelease.Release(state, cores, freed);
				}
				return true;
			case "release_vassal":
				ReleaseVassal(state, effect, scope);
				return true;
			case "create_vassal":
				// TAG becomes the scope's puppet, unless it is already or owns nothing.
				if (state.Country(scope.Country) is { } overlord && state.Country(CEffects.Target(state, effect, scope)) is { } vassal
					&& vassal.Overlord != overlord.Tag && CCountryLists.OwnedCount(state, vassal.Tag) > 0)
				{
					CPuppets.Make(state, overlord, vassal);
				}
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// <c>war</c>: nothing where the scope is at war with TAG already or either is <c>REB</c>; otherwise a
	/// declaration carrying the first attacker goal, carried out without asking whether it may be, and then
	/// the other attacker goals and the defender's added.
	/// </summary>
	private static void War(CGameState state, CEffect effect, CEventScope scope)
	{
		if (state.Country(scope.Country) is not { } actor || state.Country(WarTarget(state, effect, scope)) is not { } target
			|| actor == target || CCountryLists.AtWar(state, actor.Tag, target.Tag) || actor.Tag.Value == "REB"
			|| target.Tag.Value == "REB")
		{
			return;
		}
		var attackerGoals = new List<CWarGoal>();
		var defenderGoals = new List<CWarGoal>();
		foreach (CScriptField field in effect.Fields)
		{
			if (field.Key == "attacker_goal" && Goal(state, field, actor, target) is { } attacking)
			{
				attackerGoals.Add(attacking);
			}
			else if (field.Key == "defender_goal" && Goal(state, field, target, actor) is { } defending)
			{
				defenderGoals.Add(defending);
			}
		}
		bool limited = CEffects.Field(effect, "limited")?.IsYes ?? false;
		CWarGoal? first = attackerGoals.Count > 0 ? attackerGoals[0] : null;
		CDeclarations.Apply(state, actor, target, !limited, first);
		if (CWarSides.Between(state, actor.Tag, target.Tag) is not { } war)
		{
			return;
		}
		for (int i = 1; i < attackerGoals.Count; i++)
		{
			CWarGoals.AddToWar(state, attackerGoals[i], war);
		}
		foreach (CWarGoal goal in defenderGoals)
		{
			CWarGoals.AddToWar(state, goal, war);
		}
	}

	/// <summary>
	/// The country <c>war</c>, <c>add_wargoal</c> and <c>undeclared_war</c> mean, as the original's effect finds it:
	/// the <c>target</c> written, THIS or FROM; where none is written, or one nobody has, the controller of
	/// the scope's province.
	/// </summary>
	public static CCountryTag? WarTarget(CGameState state, CEffect effect, CEventScope scope)
	{
		CScriptArgument? written = CEffects.Field(effect, "target");
		if (written is not null && state.Country(CEffects.Target(state, written, scope)) is { } named)
		{
			return named.Tag;
		}
		return state.Province(scope.Province).Controller;
	}

	/// <summary>
	/// The country <c>casus_belli</c> and its kin mean, inline: THIS the root's country or its province's
	/// owner, FROM the scope's own FROM country, otherwise the tag written.
	/// </summary>
	/// <summary>
	/// <c>release_vassal</c>: a country the scope's may release, by name or drawn from the list, given its cores the scope's
	/// owns and holds but for its capital, and made its puppet, in its faction. A rebel faction's release is not read, and
	/// does nothing here (<c>docs/mechanics/effects.md</c>, section 8).
	/// </summary>
	private static void ReleaseVassal(CGameState state, CEffect effect, CEventScope scope)
	{
		if (state.Country(scope.Country) is not { } liberator)
		{
			return;
		}
		List<CCountry> possible = CCountryLists.PossibleVassals(state, liberator);
		CCountry? vassal;
		if (effect.Argument.IsWord("random"))
		{
			vassal = possible.Count == 0 ? null : possible[scope.Draw(possible.Count)];
		}
		else
		{
			vassal = state.Country(Inline(state, effect, scope));
		}
		if (vassal is null || !possible.Contains(vassal))
		{
			return;
		}
		var given = new List<Provinces.CProvince>();
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Cores.Contains(vassal.Tag) && province.Id != liberator.Capital && province.Owner == liberator.Tag
				&& province.Controller == liberator.Tag)
			{
				given.Add(province);
			}
		}
		CRelease.Release(state, given, vassal.Tag);
		CPuppets.Tie(state, liberator, vassal);
		if (Agreements.COfferRules.RealFaction(liberator))
		{
			CFactions.AddCountry(state, vassal, liberator.Faction, joinWars: true);
		}
		Politics.CLaws.OnDiplomacyChanged(state, vassal);
		Politics.CLaws.OnDiplomacyChanged(state, liberator);
	}

	private static CCountryTag? Inline(CGameState state, CEffect effect, CEventScope scope)
	{
		CScriptArgument value = effect.Argument;
		if (value.IsThis)
		{
			return scope.Root.Country ?? state.Province(scope.Root.Province).Owner;
		}
		return value.IsFrom ? scope.FromCountry : CEventScope.Known(value.Tag);
	}

	/// <summary>The goal a block of the effect gives, its actor and recipient stamped; none where the effect has no such block.</summary>
	private static CWarGoal? Goal(CGameState state, CEffect effect, string key, CCountry actor, CCountry recipient)
	{
		foreach (CScriptField field in effect.Fields)
		{
			if (field.Key == key)
			{
				return Goal(state, field, actor, recipient);
			}
		}
		return null;
	}

	private static CWarGoal Goal(CGameState state, CScriptField block, CCountry actor, CCountry recipient)
	{
		CCasusBelliType type = CCasusBelliType.Null;
		CCountryTag? third = null;
		Provinces.CRegion? region = null;
		foreach (CScriptField field in block.Fields)
		{
			switch (field.Key)
			{
				case "casus_belli":
					type = CCasusBellis.Find(state, field.Argument.Text);
					break;
				case "country":
					third = CEventScope.Known(field.Argument.Tag);
					break;
				case "region":
					region = state.Regions.Count > 0
						? System.Linq.Enumerable.FirstOrDefault(state.Regions, r => r.Name == field.Argument.Text)
						: null;
					break;
			}
		}
		return new CWarGoal(type, actor.Tag, recipient.Tag) { Country = third, Region = region };
	}
}
