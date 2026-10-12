using System;
using System.Globalization;
using OpenHOI3.Interface.Events;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// The one line each condition writes of itself, word for word as the original writes it, in the
/// second of its two wordings where <c>redWhen</c> is set - inside a <c>not</c> - and for a yes-or-no
/// condition wherever <c>redWhen</c> is what was written. A block writes none. See
/// <c>docs/mechanics/scripttext.md</c>, section 3, and
/// <c>reversing/findings/FINDINGS-scripttext.md</c>, sections 3 to 5, which have every line.
/// </summary>
public static class CConditionLines
{
	private const string Y = CScriptWords.Yellow;
	private const string W = CScriptWords.White;

	private static readonly string[] Months =
	{
		"January", "February", "March", "April", "May", "June",
		"July", "August", "September", "October", "November", "December",
	};

	public static string Line(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen)
	{
		CScriptArgument value = trigger.Argument;
		switch (trigger.Kind)
		{
			case ETriggerKind.Keyword:
				return Keyword(words, trigger, scope, redWhen);
			case ETriggerKind.Building:
				return Y + words.Localise(trigger.Key) + W + words.Localise(redWhen ? "BUILDING_LOWER_THAN" : "BUILDING_AT_LEAST")
					+ Y + CScriptWords.Whole(value.Amount) + W + "\n";
			case ETriggerKind.Technology:
			{
				string folder = words.State.Technologies.TryGetValue(trigger.Key, out CTechnology? technology)
					? words.Localise(technology.Folder)
					: "";
				return Y + words.Localise(trigger.Key) + " (" + folder + ")" + W
					+ words.Localise(redWhen ? "TECHNOLOGY_LOWER_THAN" : "TECHNOLOGY_HIGHER_THAN") + Y + CScriptWords.Whole(value.Amount) + W + "\n";
			}
			case ETriggerKind.UnitType:
				return words.Localise(redWhen ? "HAVE_LESS" : "HAVE_MORE") + " " + Y + words.Localise(trigger.Key) + W + " "
					+ words.Localise("THAN") + Y + CScriptWords.Whole(value.Amount) + W + "\n";
			case ETriggerKind.LawGroup:
				return words.T(redWhen ? "IS_NOT_LAW_TRIGGER" : "IS_LAW_TRIGGER",
					("LAW", NameFor(words, value.Text, scope.Country)), ("GROUP", words.Localise(trigger.Key)));
			case ETriggerKind.CombinedArms:
				// The value is never kept, so it is always "no".
				return words.Localise(words.Localise(trigger.Key)) + ": " + words.Localise(redWhen ? "YES" : "NO") + "\n";
			case ETriggerKind.Ideology:
				return words.T(redWhen ? "ALL_ORG_POP_IS_NOT_TRIGGER" : "ALL_ORG_POP_IS_ATLEAST_TRIGGER",
					("IDEOLOGY", NameFor(words, trigger.Key, scope.Country)),
					("ORGANISATION", CScriptWords.Trimmed(Field(trigger, "organisation"))),
					("POPULARITY", CScriptWords.Trimmed(Field(trigger, "popularity"))));
			case ETriggerKind.GovernmentPosition:
				return words.T(redWhen ? "GOVERNMENT_POSITION_NOT_TRIGGER" : "GOVERNMENT_POSITION_TRIGGER",
					("NAME", MinisterName(words, (int)CConditions.WholeOf(value.Amount)) ?? ""),
					("POSITION", words.Localise(trigger.Key)));
			default:
				return "";
		}
	}

	private static string Keyword(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen)
	{
		CScriptArgument value = trigger.Argument;
		CCountry? country = words.State.Country(scope.Country);
		// A yes-or-no condition takes its second wording where redWhen is what was written.
		bool turned = redWhen == value.IsYes;
		string Pick(string first, string second, bool second_) => words.Localise(second_ ? second : first);
		string Figure(string first, string second, string figure) => Pick(first, second, redWhen) + Y + figure + W + "\n";
		string Text(string first, string second, params (string, string)[] values) => words.T(redWhen ? second : first, values);
		string Here() => Y + words.Province(scope.Province) + W;
		string Whole() => CScriptWords.Whole(value.Amount);
		switch (trigger.Keyword)
		{
			// Yes or no.
			case "always": return Pick("ALWAYS_TRUE", "ALWAYS_FALSE", turned) + "\n";
			case "war": return Pick("ATWAR", "ATPEACE", turned) + "\n";
			case "remove_fow": return Pick("FOW_IS_LIFTED", "FOW_IS_NOT_LIFTED", turned) + "\n";
			case "frontage_full": return Pick("HAS_FULL_FRONTAGE", "HAS_NO_FULL_FRONTAGE", turned) + "\n";
			case "has_hostile_spy_mission": return Pick("HAS_HOSTILE_MISSION", "HAS_NO_HOSTILE_MISSION", turned) + "\n";
			case "has_strategic_resource": return Pick("HAS_STRATEGIC_RESOURCE", "HAS_NO_STRATEGIC_RESOURCE", turned) + "\n";
			case "empty":
				return turned ? Here() + words.Localise("IS_NOT_EMPTY") + "\n" : Here() + words.Localise("IS_EMPTY") + W + "\n";
			case "has_removable_minister":
				// The other way round from the rest, and nothing after.
				return Pick("HAS_NO_REMOVABLE_MINISTER", "HAS_REMOVABLE_MINISTER", turned);
			case "can_create_vassals": return Pick("CAN_CREATE_VASSAL", "CANT_CREATE_VASSAL", redWhen) + "\n";
			case "has_empty_adjacent_province": return Pick("HAVE_EMPTY_ADJACENT_PROVINCE", "HAVE_NO_EMPTY_ADJACENT_PROVINCE", redWhen) + "\n";
			case "ai": return Pick("IS_AI", "IS_NOT_AI", turned) + "\n";
			case "is_attacker": return Pick("IS_ATTACKER", "IS_NOT_ATTACKER", turned) + "\n";
			case "is_subject": return Pick("IS_SUBJECT", "IS_NOT_SUBJECT", turned) + "\n";
			case "is_in_any_faction": return Pick("IS_IN_FACTION_TRIGGER", "IS_NOT_IN_FACTION_TRIGGER", turned);
			case "government_in_exile":
				return Y + words.Country(scope.Country) + " " + W + Pick("IS_IN_EXILE", "IS_NOT_IN_EXILE", turned) + "\n";
			case "is_blockaded": return Here() + " " + Pick("HAVE_BLOCKADE_IN", "HAVE_NO_BLOCKADED", turned) + "\n";
			case "is_capital": return Here() + Pick("IS_CAPITAL", "IS_NOT_CAPITAL", turned) + "\n";
			case "is_mission_country":
				return Y + words.Country(scope.Country) + W + " " + Pick("IS_MISSION_TARGET", "IS_NOT_MISSION_TARGET", turned)
					+ " " + Y + words.Country(scope.Root.Country) + W + "\n";
			case "is_mission_province":
				return Here() + " " + Pick("IS_MISSION_TARGET", "IS_NOT_MISSION_TARGET", turned)
					+ " " + Y + words.Country(scope.Root.Country) + W + "\n";
			case "port": return Pick("HAVE_A_PORT_IN", "HAVE_NOT_A_IN", turned) + Here() + "\n";
			case "reserves": return Pick("HAS_RESERVES", "HAS_NO_RESERVES", turned) + "\n";
			case "unit_has_leader": return Pick("HAVE_A_UNITS_WITH_A_LEADER", "HAVE_NO_UNITS_WITH_A_LEADER", redWhen) + "\n";
			case "unit_in_battle": return Pick("HAVE_UNITS_IN_BATTLE", "HAVE_NO_UNITS_IN_BATTLE", redWhen) + "\n";
			case "exists":
				if (CEventScope.Known(value.Tag) is { } existing)
				{
					return Y + words.Country(existing) + W + Pick("EXISTS", "DOSNT_EXISTS", redWhen) + "\n";
				}
				return Y + words.Country(scope.Country) + W + Pick("EXISTS", "DOSNT_EXISTS", turned) + "\n";

			// A figure.
			case "air_battles_fought":
				return Figure("AIR_BATTLES_FOUGHT_MORE", "AIR_BATTLES_FOUGHT_LESS", Whole()) + Currently(words, country?.AirBattlesFought ?? 0) + "\n";
			case "land_battles_fought":
				return Figure("LAND_BATTLES_FOUGHT_MORE", "LAND_BATTLES_FOUGHT_LESS", Whole()) + Currently(words, country?.LandBattlesFought ?? 0) + "\n";
			case "naval_battles_fought":
				return Figure("NAVAL_BATTLES_FOUGHT_MORE", "NAVAL_BATTLES_FOUGHT_LESS", Whole()) + Currently(words, country?.NavalBattlesFought ?? 0) + "\n";
			case "total_amount_of_brigades":
				return Text("AMOUNT_OF_BRIGADES_IS_ATLEAST_TRIGGER", "AMOUNT_OF_BRIGADES_IS_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", Whole())) + Currently(words, CCountryConditions.Brigades(country));
			case "total_amount_of_divisions":
				return Text("AMOUNT_OF_DIVISIONS_IS_ATLEAST_TRIGGER", "AMOUNT_OF_DIVISIONS_IS_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", Whole())) + Currently(words, CCountryConditions.Divisions(country));
			case "total_amount_of_planes":
				return Figure("TOTAL_NUM_OF_PLANES_MORE", "TOTAL_NUM_OF_PLANES_LESS", Whole())
					+ Currently(words, CCountryConditions.SubUnitsOf(country, EUnitKind.Air)) + "\n";
			case "total_amount_of_ships":
				return Figure("TOTAL_NUM_OF_SHIPS_MORE", "TOTAL_NUM_OF_SHIPS_LESS", Whole())
					+ Currently(words, CCountryConditions.SubUnitsOf(country, EUnitKind.Navy)) + "\n";
			case "base_neutrality":
			case "neutrality":
				return Text("NEUTRALITY_IS_ATLEAST_TRIGGER", "NEUTRALITY_IS_NOT_TRIGGER", ("VALUE", CScriptWords.Trimmed(value.Amount)));
			case "crude_oil":
			case "energy":
			case "metal":
			case "money":
			case "rare_materials":
			case "supplies":
			case "dissent":
			{
				string stem = trigger.Keyword.ToUpperInvariant();
				return Text(stem + "_IS_ATLEAST_TRIGGER", stem + "_IS_NOT_TRIGGER", ("VALUE", CScriptWords.Trimmed(value.Amount)));
			}
			case "national_unity":
				return Text("UNITY_IS_ATLEAST_TRIGGER", "UNITY_NOT_TRIGGER", ("VALUE", CScriptWords.Trimmed(value.Amount)));
			case "blockade": return Figure("BLOCKADE_PENALTY_MORE_THAN", "BLOCKADE_PENALTY_LESS_THAN", CScriptWords.Cut(value.Amount, 1));
			case "faction_progress": return Figure("FACTION_PROGRESS_MORE", "FACTION_PROGRESS_LESS", CScriptWords.Cut(value.Amount, 1));
			case "enemy_ic_ratio":
				return Text("ENEMY_IC_RATIO_DESC", "ENEMY_IC_RATIO_NOT_DESC", ("RATIO", CScriptWords.Cut(CScriptWords.Percent(value.Amount), 2))) + "\n";
			case "manpower": return Figure("HAVE_MORE_MANPOWER_THAN", "HAVE_LESS_MANPOWER_THAN", CScriptWords.Cut(value.Amount, 1));
			case "max_manpower":
				// The key itself, never looked up, the wrong way round, and a percent sign after men.
				return (redWhen ? "HAVE_MORE_MANPOWER_THEN_MAXIMUM" : "HAVE_LESS_MANPOWER_THEN_MAXIMUM") + Y + CScriptWords.Cut(value.Amount, 1) + "%" + W + "\n";
			case "max_manpower_greater_than":
				return Figure("HAVE_MAX_MANPOWER_GREATER_THAN", "HAVE_MAX_MANPOWER_LESS_THAN", CScriptWords.Cut(value.Amount, 1));
			case "manpower_percentage":
				return Pick("HAVE_MORE_MANPOWERPERCENT_THAN", "HAVE_LESS_MANPOWERPERCENT_THAN", redWhen)
					+ Y + CScriptWords.Cut(CScriptWords.Percent(value.Amount), 1) + "%" + W + "\n";
			case "revolt_percentage":
				return Pick("THE_NUMBER_OF_REVOLT_FRACTION_IS_MORE_THAN", "THE_NUMBER_OF_REVOLT_FRACTION_IS_LESS_THAN", redWhen)
					+ Y + CScriptWords.Cut(CScriptWords.Percent(value.Amount), 1) + W + "%\n";
			case "leadership":
				// Its two wordings are made two ways.
				return redWhen
					? words.Localise("HAVE_LESS_LEADERSHIP_THAN") + " " + Y + CScriptWords.Cut(value.Amount, 1) + W + "\n"
					: words.T("HAVE_MORE_LEADERSHIP_THAN", ("VAL", Y + CScriptWords.Cut(value.Amount, 1) + W));
			case "nationalism": return Figure("NATIONALISM_HIGHER_THAN", "NATIONALISM_LOWER_THAN", Whole());
			case "num_of_allies": return Figure("HAVE_MORE_ALLIES_THAN", "HAVE_LESS_ALLIES_THAN", Whole());
			case "num_of_cities":
				// Under THIS or FROM a country's name stands where the number goes.
				return Figure("HAVE_MORE_CITIES_THAN", "HAVE_LESS_CITIES_THAN",
					value.IsThis ? words.Country(CConditions.ThisByOwner(words.State, scope))
					: value.IsFrom ? words.Country(scope.FromCountry)
					: Whole());
			case "num_of_convoys": return Figure("TOTAL_NUM_OF_CONVOYS_MORE", "TOTAL_NUM_OF_CONVOYS_LESS", Whole());
			case "num_of_ports": return Figure("HAVE_MORE_PORTS_THAN", "HAVE_LESS_PORTS_THAN", Whole());
			case "total_num_of_ports": return Figure("HAVE_MORE_TOTAL_PORTS_THAN", "HAVE_LESS_TOTAL_PORTS_THAN", Whole());
			case "num_of_revolts": return Figure("THEN_NUMBER_OF_REVOLTS_ARE_MORE_THAN", "THEN_NUMBER_OF_REVOLTS_ARE_LESS_THAN", Whole());
			case "num_of_vassals": return Figure("HAVE_MORE_VASSALS_THAN", "HAVE_LESS_VASSALS_THAN", Whole());
			case "last_air_battle_loser_losses":
			case "last_air_battle_winner_losses":
			case "last_battle_loser_losses":
			case "last_battle_winner_losses":
			case "last_naval_battle_loser_losses":
			case "last_naval_battle_winner_losses":
			{
				string stem = trigger.Keyword.ToUpperInvariant();
				return Figure(stem + "_MORE", stem + "_LESS", Whole());
			}
			case "out_of_supply_days":
				return Pick("OUT_OF_SUPPLY_DAYS_MORE", "OUT_OF_SUPPLY_DAYS_LESS", redWhen) + Y + Whole() + W + " " + words.Localise("DAYS") + "\n";
			case "month":
			{
				long month = Math.Clamp(CConditions.WholeOf(value.Amount), 0, 11);
				return Figure("THE_MONTH_IS_AFTER", "THE_MONTH_IS_BEFORE", words.Localise(Months[month]));
			}
			case "year": return Figure("THE_YEAR_IS_AFTER", "THE_YEAR_IS_BEFORE", Whole());
			case "date": return Text("THE_DATE_IS_AFTER", "THE_DATE_IS_BEFORE", ("DATE", Date(words, value.Text)));
			case "num_in_faction":
				return Text("IS_NUM_IN_FACTION_TRIGGER", "IS_BELOW_NUM_IN_FACTION_TRIGGER", ("VAL", Whole()));
			case "is_threatend":
				return Text("IS_THREATEND_TRIGGER", "IS_NOT_THREATEND_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", CScriptWords.Trimmed(value.Amount)));
			case "lost_ic":
				return Text("LOST_IC_IS_ATLEAST_TRIGGER", "LOST_IC_IS_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", CScriptWords.Cut(CScriptWords.Percent(value.Amount), 1)));
			case "lost_national":
				return Text("LOST_NATIONAL_PROVINCES_ATLEAST_TRIGGER", "LOST_NATIONAL_PROVINCES_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", CScriptWords.Cut(CScriptWords.Percent(value.Amount), 1)));
			case "pure_revolt_risk": return Figure("PURE_REVOLT_RISK_HIGHER_THAN", "PURE_REVOLT_RISK_LOWER_THAN", Whole());
			case "revolt_risk": return Figure("REVOLT_RISK_HIGHER_THAN", "REVOLT_RISK_LOWER_THAN", Whole());
			case "organisation":
				return Text("ORGANISATION_IS_ATLEAST_TRIGGER", "ORGANISATION_IS_NOT_TRIGGER",
					("IDEOLOGY", RulingIdeology(words, country)), ("VALUE", CScriptWords.Trimmed(value.Amount)));
			case "popularity":
				return Text("POPULARITY_IS_ATLEAST_TRIGGER", "POPULARITY_IS_NOT_TRIGGER",
					("IDEOLOGY", RulingIdeology(words, country)), ("VALUE", CScriptWords.Trimmed(value.Amount)));
			case "skill_advantage": return Pick("SKILL_ADVANTAGE_MORE", "SKILL_ADVANTAGE_LESS", redWhen) + Y + Whole() + "§!\n";
			case "skill": return Pick("SKILL_MORE", "SKILL_LESS", redWhen) + Y + Whole() + "§!\n";
			case "spies": return Figure("SPIES_INFILTRATED_MORE", "SPIES_INFILTRATED_LESS", CScriptWords.Trimmed(value.Amount));
			case "spy_mission_count":
				return Text("ALLOCATED_SPIES_FOR_MISSION_TRIGGER_MORE", "ALLOCATED_SPIES_FOR_MISSION_TRIGGER_LESS",
					("MISSION", words.Localise(SpyMission(FieldText(trigger, "type")))),
					("VALUE", CScriptWords.Whole(Field(trigger, "value"))));
			case "strat_bomb_impact": return Figure("TOTAL_BOMB_IMPACT_MORE", "TOTAL_BOMB_IMPACT_LESS", CScriptWords.Cut(value.Amount, 1));
			case "strat_allies_impact": return Figure("TOTAL_ALLIED_IMPACT_MORE", "TOTAL_ALLIED_IMPACT_LESS", CScriptWords.Cut(value.Amount, 1));
			case "strat_convoy_impact": return Figure("TOTAL_CONVOY_IMPACT_MORE", "TOTAL_CONVOY_IMPACT_LESS", CScriptWords.Cut(value.Amount, 1));
			case "surrender_progress":
				return Text("SURRENDER_PROGRESS_ATLEAST_TRIGGER", "SURRENDER_PROGRESS_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("VALUE", CScriptWords.Cut(value.Amount, 1)));
			case "threat":
				return Text("THREAT_IS_ATLEAST_WITH_TRIGGER", "THREAT_IS_NOT_TRIGGER",
					("COUNTRY", words.Country(scope.Country)),
					("TARGET_COUNTRY", words.Country(FieldCountry(words, trigger, "who", scope))),
					("VALUE", CScriptWords.Whole(Field(trigger, "value"))));
			case "total_defensives": return Figure("TOTAL_LAND_DEFENSE_BATTLES_MORE", "TOTAL_LAND_DEFENSE_BATTLES_LESS", Whole());
			case "total_ic": return Figure("TOTAL_IC_MORE", "TOTAL_IC_LESS", Whole());
			case "total_of_ours_sunk": return Figure("TOTAL_LOST_SHIPS_MORE", "TOTAL_LOST_SHIPS_LESS", Whole());
			case "total_offensives": return Figure("TOTAL_LAND_OFFENSIVES_MORE", "TOTAL_LAND_OFFENSIVES_LESS", Whole());
			case "total_sea_battles": return Figure("TOTAL_SEA_BATTLES_MORE", "TOTAL_SEA_BATTLES_LESS", Whole());
			case "total_sunk_by_us": return Figure("TOTAL_SUNK_BY_US_MORE", "TOTAL_SUNK_BY_US_LESS", Whole());
			case "total_we_bomb": return Figure("TOTAL_BOMBINGS_MORE", "TOTAL_BOMBINGS_LESS", Whole());
			case "war_exhaustion": return Figure("WAR_EXHAUSTION_MORE_THAN", "WAR_EXHAUSTION_LESS_THAN", CScriptWords.Cut(value.Amount, 1));
			case "check_variable":
				return words.Localise("VARIABLE") + FieldText(trigger, "which") + Pick("IS_GREATER_THAN", "IS_EQUAL_OR_LESS_THAN", redWhen)
					+ Y + CScriptWords.Cut(Field(trigger, "value"), 2) + W + "\n";
			case "relation":
				return Y + words.Country(scope.Country) + W + " "
					+ Pick("HAVE_DIPLOMATIC_RELATION", "HAVE_LESS_DIPLOMATIC_RELATION", redWhen) + " "
					+ Y + CScriptWords.Whole(Field(trigger, "value")) + W + " " + Pick("WITHG", "WITHL", redWhen) + " "
					+ Y + words.Country(FieldCountry(words, trigger, "who", scope)) + W + "\n";

			// A name.
			case "active_mission":
				return Y + value.Text + "_title" + W + " " + Pick("IS_MISSION_OF", "IS_NOT_MISSION_OF", redWhen)
					+ " " + Y + words.Country(scope.Country) + W + "\n";
			case "alliance_with":
				return Figure("HAVE_ALLIANCE_WITH", "HAVE_N0T_ALLIANCE_WITH", words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "brigade_exist": return Figure("BRIGADE_EXISTS", "BRIGADE_NOT_EXISTS", value.Text);
			case "brigade_in_combat": return Figure("BRIGADE_IN_COMBAT", "BRIGADE_NOT_IN_COMBAT", value.Text);
			case "capital": return Figure("THE_CAPITAL_IS", "THE_CAPITAL_IS_NOT", words.Province(Province(value)));
			case "casus_belli": return Figure("HAVE_CASUS_BELLI_AGAINST", "HAVE_NOT_CASUS_BELLI_AGAINST", words.Country(CEventScope.Known(value.Tag)));
			case "continent":
				return Here() + Pick("IS_ON_THE_CONTINET", "IS_NOT_ON_THE_CONTINET", redWhen) + Y + Continent(words, value, scope) + W + "\n";
			case "controlled_by":
				return Figure("CONTROLLED_BY", "NOT_CONTROLLED_BY",
					value.IsWord("owner") ? words.Localise("ITS_OWNER") : words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "controls": return Figure("HAVE_CONTROL_OVER", "HAVENT_CONTROL_OVER", words.Province(Province(value)));
			case "owns": return Figure("OWNS", "DO_NOT_OWN", words.Province(Province(value)));
			case "owned_by": return Figure("OWNED_BY", "NOT_OWNED_BY", words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "province_id": return Figure("PROVINCE_IS", "PROVINCE_IS_NOT", words.Province(Province(value)));
			case "country_units_in_province":
				return Text("COUNTRY_HAS_UNITS_IN_PROVINCE_TRIGGER", "COUNTRY_HAS_NO_UNITS_IN_PROVINCE_TRIGGER",
					("COUNTRY", words.Country(CConditions.TagByOwner(words.State, trigger, scope))), ("PROVINCE", words.Province(scope.Province)));
			case "faction":
				return Text("IS_MEMBER_OF_FACTION_TRIGGER", "IS_NOT_MEMBER_OF_FACTION_TRIGGER", ("FACTION", FactionName(words, trigger, scope)));
			case "government": return Figure("HAVE", "HAVE_NOT", words.Localise(value.Text));
			case "military_access":
				return Text("MILITARY_ACCESS_WITH_TRIGGER", "NO_MILITARY_ACCESS_WITH_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("TARGET_COUNTRY", words.Country(CConditions.TagByOwner(words.State, trigger, scope))));
			case "guarantee":
				return Text("IS_GUARANTEEING_TRIGGER", "IS_NOT_GUARANTEEING_TRIGGER",
					("COUNTRY", words.Country(scope.Country)), ("TARGET_COUNTRY", words.Country(CConditions.TagByOwner(words.State, trigger, scope))));
			case "non_aggression_pact":
				return Text("NON_AGGRESSION_PACT_WITH_TRIGGER", "NO_NON_AGGRESSION_PACT_WITH",
					("COUNTRY", words.Country(scope.Country)), ("TARGET_COUNTRY", words.Country(CConditions.TagDirect(trigger, scope))));
			case "has_cb":
				return HasCasusBelli(words, trigger, scope, redWhen);
			case "has_country_modifier": return Figure("HAVE_THE_MODIFIER", "DONT_HAVE_THE_MODIFIER", words.Localise(value.Text));
			case "has_province_modifier": return Figure("HAVE_THE_PROVINCE_MODIFIER", "DONT_HAVE_THE_PROVICEN_MODIFIER", words.Localise(value.Text));
			case "has_global_flag": return Pick("HAVE_GLOBAL_FLAG", "HAVE_NOT_GLOBAL_FLAG", redWhen) + "'" + Y + value.Text + W + "'\n";
			case "has_province_flag": return Pick("HAVE_PROVINCE_FLAG", "HAVE_NOT_PROVINCE_FLAG", redWhen) + "'" + Y + value.Text + W + "'\n";
			case "has_country_flag":
				return Pick("HAVE_COUNTRY_FLAG", "HAVE_NOT_COUNTRY_FLAG", redWhen) + "'" + Y + words.Localise(value.Text) + W + "'\n";
			case "has_leader": return Figure("HAVE_THE_LEADER", "HAVE_NOT_THE_LEADER", value.Text);
			case "has_wargoal": return Figure("HAS_WAR_GOAL_AGAINST", "HAS_NO_WAR_GOAL_AGAINST", words.Country(CEventScope.Known(value.Tag)));
			case "has_building":
				return words.T(redWhen ? "DONT_HAVE_A_X_INY" : "HAVE_A_X_INY",
					("X", Y + words.Localise(value.Text) + W), ("Y", Here())) + "\n";
			case "ideology":
				return Text("IDEOLOGY_IS_EQUAL_TO_TRIGGER", "IDEOLOGY_IS_NOT_TRIGGER",
					("NAME", NameFor(words, value.Text, scope.Country)), ("COUNTRY", words.Country(scope.Country)));
			case "ideology_group":
				return Text("IDEOLOGYG_IS_EQUAL_TO_TRIGGER", "IDEOLOGYG_IS_NOT_TRIGGER", ("NAME", words.Localise(value.Text)));
			case "minister_alive":
				return MinisterName(words, (int)CConditions.WholeOf(value.Amount)) is { } minister
					? Text("MINISTER_ALIVE_TRIGGER", "MINISTER_IS_NOT_ALIVE_TRIGGER", ("NAME", minister))
					: CScriptWords.Red + "COULD_NOT_FIND_MINISTER";
			case "is_core":
				if (value.Province != 0)
				{
					return Figure("HAVE_CORE_IN", "HAVENT_CORE", words.Province(value.Province));
				}
				return words.State.Country(CConditions.TagByOwner(words.State, trigger, scope)) is { } core
					? Text("IS_COUNTRY_CORE", "IS_NOT_COUNTRY_CORE", ("COUNTRYADJ", Y + CEventText.Adjective(words.Localise, core) + W)) + "\n"
					: "";
			case "is_possible_vassal":
				return Y + words.Country(CEventScope.Known(value.Tag)) + W + Pick("IS_A_POSSIBLE_VASSAL", "IS_NOT_A_POSSIBLE_VASSAL", redWhen) + "\n";
			case "neighbour": return Figure("IS_NEIGHBOUR_WITH", "IS_NOT_NEIGHBOUR_WITH", words.Country(CEventScope.Known(value.Tag)));
			case "last_mission": return Pick("PREVIOUS_MISSION_WAS", "PREVIOUS_MISSION_WASNT", redWhen) + "'" + Y + value.Text + W + "'\n";
			case "region":
				return Here() + Pick("IS_IN_REGION", "IS_NOT_IN_THE_REGION", redWhen) + Y + RegionName(words, value.Text) + W + "\n";
			case "strategic_resource":
				return Text("DOES_HAVE_STRATRES", "DOES_NOT_HAVE_STRATRES", ("NAME", words.Localise(value.Text)));
			case "tag": return Figure("IS", "IS_NOT", words.Country(CConditions.TagDirect(trigger, scope)));
			case "trait": return Text("HAS_TRAIT", "DOESNT_HAVE_TRAIT", ("NAME", words.Localise(value.Text)));
			case "truce_with": return Figure("HAS_TRUCE_WITH", "HAS_NO_TRUCE_WITH", words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "undeclared_war_with":
				return Figure("IS_AT_UNDECLARED_WAR_WITH", "IS_AT_PEACE_WITH", words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "war_with": return Figure("IS_AT_WAR_WITH", "IS_AT_PEACE_WITH", words.Country(CConditions.TagByOwner(words.State, trigger, scope)));
			case "vassal_of": return Figure("IS_VASSAL_OF", "IS_NOT_VASSAL_OF", words.Country(CEventScope.Known(value.Tag)));
			case "units_in_province":
				if (value.IsThis || value.IsFrom)
				{
					return Text("HAVE_UNITS_IN", "HAVE_NO_UNITS_IN", ("PROVINCE", Here()),
						("COUNTRY", Y + words.Country(CConditions.TagByOwner(words.State, trigger, scope)) + W)) + W + "\n";
				}
				return Text("HAVE_MORE_UNITS_IN_THAN", "HAVE_LESS_UNITS_IN_THAN", ("PROVINCE", Here())) + Y + Whole() + W + "\n";
			default:
				// The blocks, and the seven conditions of a battle's tactics, which write nothing.
				return "";
		}
	}

	/// <summary>
	/// "(Currently n)", the count the line is about.
	/// </summary>
	private static string Currently(CScriptWords words, int count)
	{
		return words.T("CURRENT_NUMBER_OF_DIVISIONS", ("CURRENT", count.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>A law's or an ideology's name for a country: its own where the country has one, else the plain one.</summary>
	internal static string NameFor(CScriptWords words, string key, CCountryTag? country)
	{
		if (country is { } tag)
		{
			string own = key + "_" + tag.Value;
			string text = words.Localise(own);
			if (text != own)
			{
				return text;
			}
		}
		return words.Localise(key);
	}

	/// <summary>The ruling party's name; there is none to name here, as in the original with none.</summary>
	private static string RulingIdeology(CScriptWords words, CCountry? country)
	{
		return CScriptWords.Red + "NO_SUCH_IDEOLOGY" + W;
	}

	internal static string? MinisterName(CScriptWords words, int id)
	{
		foreach (CCountry country in words.State.Countries)
		{
			if (country.Ministers.TryGetValue(id, out CMinister? minister))
			{
				return minister.Name;
			}
		}
		return null;
	}

	private static string FactionName(CScriptWords words, CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		string faction = value.IsThis
			? words.State.Country(CConditions.ThisByOwner(words.State, scope))?.Faction ?? ""
			: value.IsFrom ? words.State.Country(scope.FromCountry)?.Faction ?? "" : value.Text;
		return faction.Length == 0 ? words.Localise("NO_FACTION") : words.Localise(faction);
	}

	private static string HasCasusBelli(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen)
	{
		string type = FieldText(trigger, "type");
		if (type.Length == 0)
		{
			return "§R(Trigger error)§!";
		}
		CCountryTag? actor = FieldCountry(words, trigger, "actor", scope);
		// No break after either.
		return actor is null
			? words.T(redWhen ? "HAVE_NO_CB_FROM_ANYONE" : "HAVE_CB_FROM_ANYONE", ("CB", words.Localise(type)))
			: words.T(redWhen ? "HAVE_NO_CB_FROM" : "HAVE_CB_FROM", ("CB", words.Localise(type)), ("ACTOR", words.Country(actor)));
	}

	/// <summary>The continent named, or the one THIS's or FROM's capital is on.</summary>
	private static string Continent(CScriptWords words, CScriptArgument value, CEventScope scope)
	{
		if (value.IsThis || value.IsFrom)
		{
			CCountryTag? whose = value.IsThis ? scope.Root.Country : scope.FromCountry;
			return words.State.Country(whose) is { } country ? words.State.Province(country.Capital).Continent : "";
		}
		return value.Text;
	}

	private static string RegionName(CScriptWords words, string name)
	{
		var named = new CTrigger(ETriggerKind.Region, name, CScriptArgument.None);
		return CConditionText.RegionName(words, named);
	}

	/// <summary>The province a leaf names; nought where <c>this</c> or <c>from</c> was written.</summary>
	private static int Province(CScriptArgument value)
	{
		return value.IsThis || value.IsFrom ? 0 : (int)CConditions.WholeOf(value.Amount);
	}

	/// <summary>
	/// A date as "1 January, 1936": the day, the month's name and the year; the hour is dropped.
	/// </summary>
	private static string Date(CScriptWords words, string written)
	{
		string[] parts = written.Split('.');
		int year = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) ? y : 0;
		int month = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) ? m : 1;
		int day = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) ? d : 1;
		month = Math.Clamp(month, 1, 12);
		return day.ToString(CultureInfo.InvariantCulture) + " " + words.Localise(Months[month - 1]) + ", "
			+ year.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The title of a spy mission by its index, or "none" for any other.</summary>
	private static string SpyMission(string written)
	{
		string[] missions =
		{
			"SPYMISSION_COUNTER_ESPIONAGE_TITLE", "SPYMISSION_MILITARY_TITLE", "SPYMISSION_TECH_TITLE",
			"SPYMISSION_BOOST_RULING_PARTY_TITLE", "SPYMISSION_BOOST_OUR_PARTY_TITLE", "SPYMISSION_LOWER_NATIONAL_UNITY_TITLE",
			"SPYMISSION_INCREASE_THREAT_TITLE", "SPYMISSION_RAISE_NATIONAL_UNITY_TITLE", "SPYMISSION_COVERT_OPS_TITLE",
		};
		return int.TryParse(written, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < missions.Length
			? missions[index]
			: "SPYMISSION_NONE_TITLE";
	}

	private static CFixedPoint Field(CTrigger trigger, string key)
	{
		foreach (CScriptField field in trigger.Fields)
		{
			if (field.Key == key)
			{
				return field.Argument.Amount;
			}
		}
		return CFixedPoint.Zero;
	}

	private static string FieldText(CTrigger trigger, string key)
	{
		foreach (CScriptField field in trigger.Fields)
		{
			if (field.Key == key)
			{
				return field.Argument.Text;
			}
		}
		return "";
	}

	/// <summary>The country a block keyword's field names: THIS by owner, FROM, or the tag.</summary>
	private static CCountryTag? FieldCountry(CScriptWords words, CTrigger trigger, string key, CEventScope scope)
	{
		foreach (CScriptField field in trigger.Fields)
		{
			if (field.Key == key)
			{
				CScriptArgument value = field.Argument;
				return value.IsThis ? CConditions.ThisByOwner(words.State, scope)
					: value.IsFrom ? scope.FromCountry
					: CEventScope.Known(value.Tag);
			}
		}
		return null;
	}
}
