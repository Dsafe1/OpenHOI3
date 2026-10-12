using System;
using System.Globalization;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// The one line each effect writes of itself, word for word as the original writes it, and the
/// short name of what it acts on that folds a run of one keyword onto one line. Many lines are
/// nothing where the effect would change nothing; what the simulation does not hold is asked as
/// the conditions ask it. See <c>docs/mechanics/scripttext.md</c>, section 4, and
/// <c>reversing/findings/FINDINGS-scripttext.md</c>, sections 1 and 2, which have every line.
/// </summary>
public static class CEffectLines
{
	private const string Y = CScriptWords.Yellow;
	private const string W = CScriptWords.White;
	private const string R = CScriptWords.Red;
	private const string G = CScriptWords.Green;

	private static readonly string[] Months =
	{
		"January", "February", "March", "April", "May", "June",
		"July", "August", "September", "October", "November", "December",
	};

	/// <summary>
	/// What a run of one keyword is folded by: the province <c>add_core</c>, <c>remove_core</c> and a
	/// unit type act on, or nothing. <c>add_core</c>'s is nothing where the core is had, and
	/// <c>remove_core</c>'s - the same code - nothing where it is <i>not</i> had.
	/// </summary>
	public static string Label(CScriptWords words, CEffect effect, CEventScope scope)
	{
		if (effect.Kind == EEffectKind.UnitType)
		{
			return words.Province(effect.Argument.Province != 0 ? effect.Argument.Province : scope.Province);
		}
		if (effect.Keyword is not ("add_core" or "remove_core"))
		{
			return "";
		}
		(int province, CCountryTag? country) = Core(words, effect, scope);
		if (country is not { } core || words.State.Province(province).Cores.Contains(core))
		{
			return "";
		}
		return words.Province(province);
	}

	public static string Line(CScriptWords words, CEffect effect, CEventScope scope, string group)
	{
		CScriptArgument value = effect.Argument;
		switch (effect.Kind)
		{
			case EEffectKind.Keyword:
				return Keyword(words, effect, scope, group);
			case EEffectKind.UnitType:
				return words.T("ADD_SUB_UNIT_EFFECT",
					("WHERE", words.Nested(Y + group + Label(words, effect, scope) + W)),
					("UNIT", Y + words.Localise(effect.Key) + W)) + "\n";
			case EEffectKind.Building:
			{
				CBuilding? building = null;
				foreach (CBuilding each in words.State.Buildings)
				{
					if (each.Key.Equals(effect.Key, StringComparison.OrdinalIgnoreCase))
					{
						building = each;
					}
				}
				CFixedPoint working = building is null ? CFixedPoint.Zero : words.State.Province(scope.Province).BuildingLevel(building);
				// The value is set against the level working, though the effect adds it.
				return words.T(value.Amount < working ? "REMOVE_BUILDING_EFFECT" : "ADD_BUILDING_EFFECT",
					("BUILDING", words.Localise(effect.Key)), ("PROVINCE", words.Province(scope.Province)),
					("VALUE", CScriptWords.Trimmed(value.Amount)));
			}
			case EEffectKind.Ideology:
				return OrganisationPopularity(words, effect, scope);
			case EEffectKind.Technology:
			{
				CFixedPoint amount = value.Amount;
				return words.T("TECH_INVEST", ("TECH", words.Nested(words.Localise(effect.Key))),
					("DIRECTION", words.Nested(words.Localise(amount.Raw < 0 ? "LOSE" : "GAIN"))),
					("VALUE", Colour(amount) + CScriptWords.Trimmed(amount) + W + "\n"));
			}
			case EEffectKind.RebelType:
				return words.T("CREATE_REBELS", ("WHERE", words.Nested(Y + words.Province(scope.Province) + W)),
					("SIZE", words.Nested(R + RevoltSize(words, value, scope) + W)),
					("TYPE", words.Nested(R + words.Localise(effect.Key + "_title") + W)));
			case EEffectKind.GovernmentPosition:
				return ReplaceMinister(words, effect, scope);
			default:
				return "";
		}
	}

	/// <summary>
	/// A government position given a minister: "Name will become Position." - nothing where he is not
	/// found, the post is not one of his, or he holds it already.
	/// </summary>
	private static string ReplaceMinister(CScriptWords words, CEffect effect, CEventScope scope)
	{
		int id = (int)CConditions.WholeOf(effect.Argument.Amount);
		foreach (CCountry country in words.State.Countries)
		{
			if (!country.Ministers.TryGetValue(id, out CMinister? minister))
			{
				continue;
			}
			if (minister.TypeIn(effect.Key) is null
				|| (words.State.Country(scope.Country) is { } holder
					&& holder.Government.TryGetValue(effect.Key, out CMinister? holding) && holding == minister))
			{
				return "";
			}
			return words.T("REPLACE_MINISTER_EFFECT", ("NAME", minister.Name), ("POSITION", words.Localise(effect.Key)));
		}
		return "";
	}

	private static string Keyword(CScriptWords words, CEffect effect, CEventScope scope, string group)
	{
		CGameState state = words.State;
		CScriptArgument value = effect.Argument;
		CFixedPoint amount = value.Amount;
		CCountry? country = state.Country(scope.Country);
		CCountryTag? target = CEffects.Target(state, effect, scope);
		string Us() => words.Country(scope.Country);
		string Them() => words.Country(target);
		string Where() => words.Nested(Y + words.Province(scope.Province) + W);
		switch (effect.Keyword)
		{
			// Diplomacy.
			case "create_alliance":
				return scope.Country is { } us && target is { } them && CCountryLists.HasAlliance(state, us, them)
					? ""
					: words.T("ALLIANCE_EFFECT", ("WHO", Y + Them() + W));
			case "leave_alliance":
				return scope.Country is { } leaver && target is { } left && CCountryLists.HasAlliance(state, leaver, left)
					? words.T("LEAVE_ALLIANCE_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()))
					: "";
			// Guarantees, pacts and access are not built: none is had.
			case "guarantee": return words.T("START_GUARANTEE_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()));
			case "end_guarantee": return "";
			case "end_non_aggression_pact": return "";
			case "non_aggression_pact": return words.T("START_NON_AGGRESSION_PACT_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()));
			case "military_access": return words.T("GRANT_MILITARY_ACCESS_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()));
			case "end_military_access": return "";
			case "end_war":
				return scope.Country is { } side && target is { } enemy && CCountryLists.AtWar(state, side, enemy)
					? words.T("END_WAR_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()))
					: "";
			case "war":
				return scope.Country is { } attacker && target is { } defender && CCountryLists.AtWar(state, attacker, defender)
					? ""
					: words.T("WAR_EFFECT", ("WHO", Y + Them() + W));
			case "undeclared_war":
				return scope.Country is { } raider && target is { } raided && CCountryLists.AtWar(state, raider, raided)
					? ""
					: words.Localise("ADD_UNDECLARED_REGION_EFFECT")
						+ CConditionText.RegionName(words, new CTrigger(ETriggerKind.Region, CEffects.Field(effect, "region")?.Text ?? "", CScriptArgument.None));
			case "create_vassal":
				return state.Country(target)?.Overlord == scope.Country || target is null || CCountryLists.OwnedCount(state, target) < 1
					? ""
					: words.T("CREATE_VASSAL_EFFECT", ("COUNTRY", Us()), ("TARGET_COUNTRY", Them()));
			case "release_vassal":
				return target is null ? "" : words.T("RELEASE_VASSAL_EFFECT", ("TARGET", Y + Them() + W));
			case "casus_belli": return words.T("GAIN_CB", ("WHO", words.Nested(Y + words.Country(AsItStands(state, value, scope)) + W)));
			case "add_casus_belli":
				return words.T("REV_GAIN_CB", ("ACTIVE", words.Nested(Y + words.Country(AsItStands(state, value, scope)) + W)),
					("PASSIVE", words.Nested(Y + Us() + W)));
			case "inherit": return words.T("INHERIT", ("WHO", words.Nested(Y + words.Country(AsItStands(state, value, scope)) + W)));
			case "surrender_inherit":
				return words.T("SURRENDER_INHERIT", ("WHO", words.Nested(Y + words.Country(AsItStands(state, value, scope)) + W)));
			case "release":
			{
				CCountryTag? released = value.IsThis ? scope.Root.Country : value.IsFrom ? scope.FromCountry : CEventScope.Known(value.Tag);
				// Said only of a country that owns no province.
				return released is null || CCountryLists.OwnedCount(state, released) > 0
					? ""
					: words.T("RELEASE", ("WHO", words.Nested(Y + words.Country(released) + W)));
			}
			case "relation":
			{
				CCountryTag? who = FieldCountry(state, effect, "who", scope);
				CFixedPoint much = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
				string figure = Colour(much) + CScriptWords.Cut(much, 0) + W;
				return scope.Country == words.Player
					? words.T("RELATION_CHANGE_SHORT", ("PASSIVE", words.Nested(words.Country(who))), ("MUCH", figure))
					: words.T("RELATION_CHANGE", ("ACTIVE", words.Nested(Us())), ("PASSIVE", words.Nested(words.Country(who))), ("MUCH", figure));
			}
			case "threat":
			{
				CScriptArgument? who = CEffects.Field(effect, "who");
				CFixedPoint much = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
				string named = who is null || who.IsWord("all") ? "ALL" : words.Country(FieldCountry(state, effect, "who", scope));
				return words.T(much.Raw <= 0 ? "REMOVE_THREAT_EFFECT" : "ADD_THREAT_EFFECT",
					("COUNTRY", Us()), ("TARGET_COUNTRY", named), ("VALUE", CScriptWords.Trimmed(much)));
			}
			case "split_troops":
				return words.T("SPLIT_TROOPS_EFF", ("NAME", words.Country(scope.Root.Country)),
					("VALUE", CScriptWords.Trimmed(CScriptWords.Percent(amount))));
			case "add_wargoal":
			{
				string type = CEffects.Field(effect, "war_goal")?.Text ?? "";
				return words.T("WARGOAL_EFFECT", ("GOAL", words.T(type + "_setup", ("ACTOR", Us()), ("RECIPIENT", Them()))));
			}

			// Politics.
			case "government":
				return words.T("GOV_CHANGE", ("GOV", words.Nested(Y + words.Localise(value.Text) + W)));
			case "coup_by":
				// THIS and FROM name a ruling party, and none is held here: the original's nothing where there is none.
				return value.IsThis || value.IsFrom
					? ""
					: words.T("PERFORM_COUP", ("PARTY", words.Nested(Y + CConditionLines.NameFor(words, value.Text, scope.Country) + W)));
			case "do_election":
			{
				CCountryTag? voting = AsItStands(state, value, scope);
				return voting is null || CCountryLists.OwnedCount(state, voting) > 0
					? ""
					: words.T("ELECTION_EFFECT", ("WHO", words.Nested(Y + words.Country(voting) + W)));
			}
			case "join_faction":
				return country is not null && country.Faction.Equals(value.Text, StringComparison.OrdinalIgnoreCase)
					? ""
					: words.T("JOIN_FACTION_EFFECT", ("FACTION", words.Nested(Y + words.Localise(value.Text) + W)));
			case "leave_faction":
			{
				if (country is null || !country.Faction.Equals(value.Text, StringComparison.OrdinalIgnoreCase)
					|| CCountryLists.FactionMembers(state, value.Text) is not { Count: > 0 } members || members[0] == country)
				{
					return "";
				}
				return words.T("LEAVE_FACTION_EFFECT", ("FACTION", words.Nested(Y + words.Localise(value.Text) + W)));
			}
			case "form_government_in_exile":
				return words.Localise(value.IsYes ? "FORM_GOVERMENT_IN_EXILE" : "REVOKE_GOVERMENT_IN_EXILE");
			case "country_event":
			case "province_event":
				return SpawnEvent(words, effect, scope);
			case "remove_minister":
				return CConditionLines.MinisterName(words, (int)CConditions.WholeOf(amount)) is { } removed
					? words.T("REMOVE_MINISTER_EFFECT", ("NAME", removed))
					: "";
			case "kill_leader":
			{
				int id = (int)CConditions.WholeOf(amount);
				CLeader? leader = country?.ActiveLeaders.Find(each => each.Id == id);
				return leader is null ? "" : words.T("KILL_LEADER_EFFECT", ("NAME", Y + leader.Name + W));
			}
			case "load_oob":
				// The path asked for as a key, which shows it bare.
				return words.Localise(value.Text);
			case "organisation":
			case "popularity":
			{
				string stem = effect.Keyword == "organisation" ? "ORG" : "POP";
				bool gains = amount.Raw > 0;
				return words.T(gains ? "ADD_RULING_PARTY_" + stem + "_EFFECT" : "REMOVE_RULING_PARTY_" + stem + "_EFFECT",
					("NAME", ""), ("VALUE", CScriptWords.Trimmed(gains ? amount : new CFixedPoint(-amount.Raw))));
			}

			// A country's figures and goods.
			case "money":
			case "crude_oil":
			case "rare_materials":
			case "supplies":
			case "energy":
			case "metal":
			case "fuel":
			{
				string stem = effect.Keyword.ToUpperInvariant();
				string whole = CScriptWords.Whole(amount);
				if (scope.Province != 0)
				{
					return words.T(amount.Raw <= 0 ? "REMOVE_PROVINCE_" + stem + "_EFFECT" : "ADD_PROVINCE_" + stem + "_EFFECT",
						("VALUE", whole), ("PROVINCE", words.Province(scope.Province)));
				}
				return words.T(amount.Raw > 0 ? "ADD_" + stem + "_EFFECT" : "REMOVE_" + stem + "_EFFECT", ("VALUE", whole));
			}
			case "national_unity":
			{
				long scaled = amount.Raw * (1000 + (country?.Modifier["national_unity_effect"].Raw ?? 0)) / 1000;
				return words.T(scaled > 0 ? "ADD_UNITY_EFFECT" : "REMOVE_UNITY_EFFECT", ("VALUE", CScriptWords.Whole(scaled / CFixedPoint.Scale)));
			}
			case "neutrality":
				return words.T(amount.Raw >= 0 ? "ADD_NEUTRALITY_EFFECT" : "REMOVE_NEUTRALITY_EFFECT",
					("COUNTRY", Us()), ("VALUE", CScriptWords.Trimmed(amount)));
			case "officer_pool":
				return words.T(amount.Raw >= 0 ? "ADD_OFFICERS_EFFECT" : "REMOVE_OFFICERS_EFFECT",
					("COUNTRY", Us()), ("VALUE", CScriptWords.Trimmed(amount)));
			case "manpower":
				return amount.Raw < 0
					? words.T("REMOVE_MANPOWER_EFFECT", ("VALUE", R + CScriptWords.Cut(new CFixedPoint(-amount.Raw), 1) + W))
					: words.T("ADD_MANPOWER_EFFECT", ("VALUE", G + CScriptWords.Cut(amount, 1) + W));
			case "dissent":
				return amount.Raw < 0
					? words.T("REMOVE_DISSENT_EFFECT", ("VALUE", CScriptWords.Trimmed(new CFixedPoint(-amount.Raw))))
					: words.T("ADD_DISSENT_EFFECT", ("VALUE", CScriptWords.Trimmed(amount)));
			case "practical":
			{
				CFixedPoint gain = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
				if (gain.Raw == 0)
				{
					return "";
				}
				// The category's short name's key, never looked up.
				string category = (CEffects.Field(effect, "technology_group")?.Text ?? "") + "_short";
				return gain.Raw > 0
					? words.T("ADD_PRACTICAL_EFFECT", ("VAL", CScriptWords.Trimmed(gain)), ("TARGET", category))
					: words.T("REDUCE_PRACTICAL_EFFECT", ("VAL", CScriptWords.Trimmed(new CFixedPoint(-gain.Raw))), ("TARGET", category));
			}
			case "modify_spies":
			{
				long count = CConditions.WholeOf(CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero);
				string spied = words.Country(FieldCountry(state, effect, "target", scope));
				return count > 0 ? words.T("ADD_SPIES_EFFECT", ("VAL", CScriptWords.Whole(count)), ("TARGET", spied))
					: count < 0 ? words.T("REMOVE_SPIES_EFFECT", ("VAL", CScriptWords.Whole(-count)), ("TARGET", spied))
					: "";
			}
			case "war_exhaustion":
				return words.T("WAREXHAUSTION_EFFECT", ("DIRECTION", words.Localise(amount.Raw < 0 ? "LOSE" : "GAIN")),
					("VALUE", Colour(amount) + CScriptWords.Trimmed(amount) + W));

			// A province's things.
			case "add_core":
			{
				string label = Label(words, effect, scope);
				return label.Length == 0 ? "" : words.T("NEW_CORE_EFFECT", ("NEW", words.Nested(Y + group + label + W)));
			}
			case "remove_core":
			{
				string label = Label(words, effect, scope);
				return label.Length == 0 ? "" : words.T("LOSE_CORE_EFFECT", ("LOSE", words.Nested(Y + group + label + W)));
			}
			case "capital":
			{
				int id = value.IsThis || value.IsFrom ? 0 : (int)CConditions.WholeOf(amount);
				return country?.Capital == id ? "" : words.T("CAPITAL_CHANGE", ("NEW", words.Nested(Y + words.Province(id) + W)));
			}
			case "change_controller":
				return state.Province(scope.Province).Controller == target
					? ""
					: words.T("CHANGE_CONTROLLER_EFFECT", ("WHERE", Where()), ("WHO", Y + Them() + W));
			case "secede_province":
				return target is null || state.Province(scope.Province).Owner == target
					? ""
					: words.T("SECEDE_PROVINCE", ("WHERE", Where()), ("WHO", words.Nested(Y + Them() + W)));
			case "change_manpower":
				return words.T("PROV_MANPOWER", ("DIRECTION", words.Nested(words.Localise(amount.Raw >= 0 ? "GAIN" : "LOSE"))),
					("VALUE", Colour(amount) + CScriptWords.Whole(amount.Raw / CFixedPoint.Scale * 100) + W), ("WHERE", Where()));
			case "leadership":
				return words.T("PROV_LEADERSHIP", ("DIRECTION", words.Nested(words.Localise(amount.Raw >= 0 ? "GAIN" : "LOSE"))),
					("VALUE", Colour(amount) + CScriptWords.Cut(amount, 2) + W), ("WHERE", Where()));
			case "revolt_risk":
				return words.T("REVOLTRISK_EFFECT", ("DIRECTION", words.Localise(amount.Raw < 0 ? "LOSE" : "GAIN")),
					("VALUE", Colour(amount) + CScriptWords.Trimmed(amount) + W), ("WHERE", words.Province(scope.Province)));
			case "create_revolt":
				return words.T("CREATE_REVOLT", ("WHERE", Where()), ("SIZE", words.Nested(R + RevoltSize(words, value, scope) + W)));
			case "local_intel_boost":
				return scope.Province == 0 || scope.FromCountry is null
					? ""
					: words.T("LOCAL_INTEL_BOOST", ("PROVINCE", words.Province(scope.Province)));
			case "strategic_resource":
				return value.IsWord("none")
					? words.Localise("EFF_NO_MORE_RES")
					: words.T("EFF_STRAT_RES", ("WHICH", words.Localise(value.Text)));
			case "change_province_name":
				return words.T("RENAME_PROV_EFF", ("OLD", words.Province(scope.Province)), ("NEW", value.Text));
			case "remove_brigade":
				return words.T("REMOVE_BRIGADE", ("NAME", value.Text));
			case "remove_fow":
			{
				CCountryTag? whose = scope.Province != 0 ? state.Province(scope.Province).Owner : scope.Country;
				return words.T("REMOVE_FOW", ("COUNTRY", Y + words.Country(whose) + W), ("MONTHS", Y + CScriptWords.Trimmed(amount) + W));
			}
			case "add_country_modifier":
				return country is not null ? ModifierAdded(words, effect, country.EventModifiers, Us()) : "";
			case "add_province_modifier":
				return ModifierAdded(words, effect, state.Province(scope.Province).EventModifiers, words.Nested(words.Province(scope.Province)));
			case "remove_country_modifier":
				return country is not null && country.EventModifiers.Exists(given => given.Modifier.Name == value.Text)
					? words.T("MODIFIER_REMOVED", ("WHERE", Us()), ("WHAT", words.Nested(words.Localise(value.Text))))
					: "";
			case "remove_province_modifier":
				return state.Province(scope.Province).EventModifiers.Exists(given => given.Modifier.Name == value.Text)
					? words.T("MODIFIER_REMOVED", ("WHERE", words.Nested(words.Province(scope.Province))), ("WHAT", words.Nested(words.Localise(value.Text))))
					: "";
			default:
				// Flags, variables, the AI's strategies and add_division say nothing.
				return "";
		}
	}

	/// <summary>
	/// "Germany gets 'name' until date, giving the following effects:" and each of its values, where
	/// the modifier is not given already.
	/// </summary>
	private static string ModifierAdded(CScriptWords words, CEffect effect, System.Collections.Generic.List<CTimedModifier> given, string where)
	{
		CModifier? modifier = CEffects.Field(effect, "name") is { } name ? words.State.FindStaticModifier(name.Text) : null;
		if (modifier is null || CMarkEffects.Holds(given, modifier))
		{
			return "";
		}
		long days = CConditions.WholeOf(CEffects.Field(effect, "duration")?.Amount ?? CFixedPoint.Zero);
		CDate until = words.State.Date.AddDays((int)Math.Max(days, 0));
		return words.T("MODIFIER_ADDED", ("WHERE", where), ("WHAT", words.Nested(words.Localise(modifier.Name))),
			("DATE", HourDate(words, until, words.State.Hour)),
			("EFFECT", words.Nested(CModifierText.AllValues(words.Localise, modifier))));
	}

	/// <summary>" 13:00, 1 January, 1936", the original's way of writing an hour's date, a space first.</summary>
	private static string HourDate(CScriptWords words, CDate date, int hour)
	{
		return string.Format(CultureInfo.InvariantCulture, " {0}:00, {1} {2}, {3}", hour, date.Day,
			words.Localise(Months[Math.Clamp(date.Month, 1, 12) - 1]), date.Year);
	}

	/// <summary>"will get the 'title' event." - the title in a scope whose FROM is the scope's own country.</summary>
	private static string SpawnEvent(CScriptWords words, CEffect effect, CEventScope scope)
	{
		int id = (int)CConditions.WholeOf(effect.Argument.Amount);
		foreach (CEvent each in words.State.Events)
		{
			if (each.Id != id)
			{
				continue;
			}
			CEventScope titled = effect.Keyword == "country_event"
				? CEventScope.Start(scope.Country, scope.Province, fromCountry: scope.Country)
				: scope;
			return words.T("SPAWN_EVENT", ("WHICH", CEventText.Localise(words.Localise, words.State, each.Title, titled)));
		}
		return words.Localise("SPAWN_EVENT");
	}

	/// <summary>
	/// An ideology's organisation and popularity: gained where either figure is at least nought, a
	/// negative one shown in red with its minus; lost where both are below.
	/// </summary>
	private static string OrganisationPopularity(CScriptWords words, CEffect effect, CEventScope scope)
	{
		CFixedPoint organisation = CEffects.Field(effect, "organisation")?.Amount ?? CFixedPoint.Zero;
		CFixedPoint popularity = CEffects.Field(effect, "popularity")?.Amount ?? CFixedPoint.Zero;
		string ideology = CConditionLines.NameFor(words, effect.Key, scope.Country);
		if (organisation.Raw >= 0 || popularity.Raw >= 0)
		{
			string Shown(CFixedPoint figure) => figure.Raw < 0 ? R + CScriptWords.Trimmed(figure) + W : CScriptWords.Trimmed(figure);
			return words.T("ADD_ORGANISATION_POPULARITY_EFFECT", ("IDEOLOGY", ideology),
				("ORGANISATION", Shown(organisation)), ("POPULARITY", Shown(popularity)));
		}
		return words.T("REMOVE_ORGANISATION_POPULARITY_EFFECT", ("IDEOLOGY", ideology),
			("ORGANISATION", CScriptWords.Trimmed(organisation)), ("POPULARITY", CScriptWords.Trimmed(popularity)));
	}

	/// <summary>How many regiments rise: the value times the province's manpower, whole.</summary>
	private static string RevoltSize(CScriptWords words, CScriptArgument value, CEventScope scope)
	{
		long size = value.Amount.Raw * words.State.Province(scope.Province).Manpower.Raw / CFixedPoint.Scale / CFixedPoint.Scale;
		return CScriptWords.Whole(size);
	}

	/// <summary>Green above nought, red below, yellow at it.</summary>
	private static string Colour(CFixedPoint value)
	{
		return value.Raw > 0 ? G : value.Raw < 0 ? R : Y;
	}

	/// <summary>A country as the effects that take FROM's tag as it stands name it: THIS by owner, FROM, or the tag.</summary>
	private static CCountryTag? AsItStands(CGameState state, CScriptArgument value, CEventScope scope)
	{
		return value.IsThis ? CConditions.ThisByOwner(state, scope) : value.IsFrom ? scope.FromCountry : CEventScope.Known(value.Tag);
	}

	private static CCountryTag? FieldCountry(CGameState state, CEffect effect, string key, CEventScope scope)
	{
		return CEffects.Field(effect, key) is { } named ? AsItStands(state, named, scope) : null;
	}

	/// <summary>The province and the country of a core, as the effect takes them.</summary>
	private static (int Province, CCountryTag? Country) Core(CScriptWords words, CEffect effect, CEventScope scope)
	{
		CScriptArgument argument = effect.Argument;
		int province = argument.Province != 0 ? argument.Province : argument.IsFrom ? scope.FromProvince : scope.Province;
		CCountryTag? country = argument.IsThis ? CConditions.ThisByOwner(words.State, scope)
			: argument.Province == 0 && !argument.IsFrom ? CEventScope.Known(argument.Tag)
			: scope.Country;
		return (province, country);
	}
}
