using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// What <c>load_oob</c> writes of itself. The original's is the file's path, asked for as a key. An enhanced game
/// whose localisation has the lines says what the file would do (after BiceLib's <c>activateLoadOobDetails</c>;
/// <c>docs/mechanics/scripttext.md</c>): how many units and where, the brigades, ships and wings, the lines for the
/// build queue, and each leader it puts on a unit with where he is now and whether his rank fits. Every word is the
/// localisation's, by keys of ours.
/// </summary>
public static class CLoadOobText
{
	/// <summary>The most provinces named before "and N more", without Alt.</summary>
	public const int MostPlaces = 4;

	/// <summary>The most leaders listed before "and N more", without Alt.</summary>
	public const int MostLeaders = 10;

	public const string MissingKey = "OPENHOI_LOAD_OOB_MISSING";

	/// <summary><c>$COUNT$</c> and <c>$PLACES$</c>. The key whose being there turns the text on.</summary>
	public const string UnitsKey = "OPENHOI_LOAD_OOB_UNITS";

	/// <summary><c>$NAME$</c>: a province with one unit.</summary>
	public const string PlaceKey = "OPENHOI_LOAD_OOB_PLACE";

	/// <summary><c>$NAME$</c> and <c>$COUNT$</c>: a province with several.</summary>
	public const string PlaceManyKey = "OPENHOI_LOAD_OOB_PLACE_MANY";

	/// <summary><c>$COUNT$</c>: the provinces not named.</summary>
	public const string PlacesMoreKey = "OPENHOI_LOAD_OOB_PLACES_MORE";

	public const string BrigadesKey = "OPENHOI_LOAD_OOB_BRIGADES";
	public const string ShipsKey = "OPENHOI_LOAD_OOB_SHIPS";
	public const string WingsKey = "OPENHOI_LOAD_OOB_WINGS";
	public const string ProductionKey = "OPENHOI_LOAD_OOB_PRODUCTION";
	public const string LeadersKey = "OPENHOI_LOAD_OOB_LEADERS";

	/// <summary><c>$NAME$</c>, <c>$KIND$</c> and <c>$WHERE$</c>.</summary>
	public const string LeaderKey = "OPENHOI_LOAD_OOB_LEADER";

	/// <summary><c>$ID$</c>: a number that is none of the country's leaders.</summary>
	public const string LeaderUnknownKey = "OPENHOI_LOAD_OOB_LEADER_UNKNOWN";

	public const string LeadersMoreKey = "OPENHOI_LOAD_OOB_LEADERS_MORE";
	public const string NoCommandKey = "OPENHOI_LOAD_OOB_NO_COMMAND";

	/// <summary><c>$UNIT$</c>: the unit he commands now, its place not known.</summary>
	public const string CommandKey = "OPENHOI_LOAD_OOB_COMMAND";

	/// <summary><c>$UNIT$</c> and <c>$PROVINCE$</c>.</summary>
	public const string CommandAtKey = "OPENHOI_LOAD_OOB_COMMAND_AT";

	/// <summary><c>$RANK$</c>, <c>$WANTED$</c>, <c>$KIND$</c>: loading the file will give him the unit's rank.</summary>
	public const string RankMadeKey = "OPENHOI_LOAD_OOB_RANK_MADE";

	/// <summary><c>$RANK$</c>, <c>$WANTED$</c>, <c>$KIND$</c>: he keeps a rank the unit does not take.</summary>
	public const string RankWrongKey = "OPENHOI_LOAD_OOB_RANK_WRONG";

	public const string HoldAltKey = "OPENHOI_LOAD_OOB_HOLD_ALT";

	/// <summary>A kind of unit with its article, the mod's: this and the kind's name in capitals.</summary>
	public const string KindKeyStem = "OPENHOI_LOAD_OOB_KIND_";

	/// <param name="country">The country the effect's scope is, whose leaders the file's numbers are looked for among.</param>
	public static string For(CScriptWords words, string path, CCountry? country)
	{
		CGameState state = words.State;
		if (!state.Rules.Enhanced || state.SummariseOrderOfBattle is not { } summarise || words.Localise(UnitsKey) == UnitsKey)
		{
			// The path asked for as a key, which shows it bare.
			return words.Localise(path);
		}
		COrderOfBattleSummary summary = summarise(path);
		bool all = words.ShowAll?.Invoke() == true;
		bool cut = false;
		var text = new StringBuilder(path);
		if (!summary.Found)
		{
			return text.Append('\n').Append(words.Localise(MissingKey)).Append('\n').ToString();
		}
		if (summary.Units > 0)
		{
			var places = new List<string>();
			foreach (COrderOfBattlePlace place in summary.Places)
			{
				if (!all && places.Count == MostPlaces)
				{
					cut = true;
					break;
				}
				string name = ProvinceName(state, place.Province);
				places.Add(place.Units > 1 ? words.T(PlaceManyKey, ("NAME", name), ("COUNT", Whole(place.Units))) : words.T(PlaceKey, ("NAME", name)));
			}
			string named = string.Join(", ", places);
			if (places.Count < summary.Places.Count)
			{
				named += words.T(PlacesMoreKey, ("COUNT", Whole(summary.Places.Count - places.Count)));
			}
			text.Append('\n').Append(words.T(UnitsKey, ("COUNT", Whole(summary.Units)), ("PLACES", named)));
		}
		var contents = new List<string>();
		foreach ((int count, string key) in new[] { (summary.Brigades, BrigadesKey), (summary.Ships, ShipsKey), (summary.Wings, WingsKey) })
		{
			if (count > 0)
			{
				contents.Add(words.T(key, ("COUNT", Whole(count))));
			}
		}
		if (contents.Count > 0)
		{
			text.Append('\n').Append(string.Join(", ", contents));
		}
		if (summary.Constructions > 0)
		{
			text.Append('\n').Append(words.T(ProductionKey, ("COUNT", Whole(summary.Constructions))));
		}
		if (summary.Leaders.Count > 0)
		{
			text.Append('\n').Append(words.T(LeadersKey, ("COUNT", Whole(summary.Leaders.Count))));
			int listed = 0;
			foreach (COrderOfBattleLeader assignment in summary.Leaders)
			{
				if (!all && listed == MostLeaders)
				{
					cut = true;
					text.Append('\n').Append(words.T(LeadersMoreKey, ("COUNT", Whole(summary.Leaders.Count - listed))));
					break;
				}
				listed++;
				Leader(words, text, country, assignment);
			}
		}
		if (cut)
		{
			text.Append('\n').Append(words.Localise(HoldAltKey)).Append('\n');
		}
		// The texts of an option's effects run together, so the next one would start on this one's last line.
		return text.Append('\n').ToString();
	}

	private static void Leader(CScriptWords words, StringBuilder text, CCountry? country, COrderOfBattleLeader assignment)
	{
		// Among all the country's leaders, in service or not: the file puts him on whoever he is.
		CLeader? leader = country?.Leaders.Find(each => each.Id == assignment.Leader);
		if (leader is null)
		{
			text.Append('\n').Append(words.T(LeaderUnknownKey, ("ID", Whole(assignment.Leader))));
			return;
		}
		string kind = words.Localise(KindKeyStem + assignment.Kind.ToString().ToUpperInvariant());
		string where = words.Localise(NoCommandKey);
		if (leader.Unit is { } commanded)
		{
			string unit = Text.CColouredText.Strip(commanded.Name);
			where = commanded.Location > 0 && commanded.Location < words.State.Provinces.Count
				? words.T(CommandAtKey, ("UNIT", unit), ("PROVINCE", ProvinceName(words.State, commanded.Location)))
				: words.T(CommandKey, ("UNIT", unit));
		}
		text.Append('\n').Append(words.T(LeaderKey, ("NAME", Text.CColouredText.Strip(leader.Name)), ("KIND", kind), ("WHERE", where)));

		// The rank a land unit's level takes, exactly (leaders.md, section 4); a fleet and an air unit take any.
		int wanted = assignment.Kind switch
		{
			EUnitKind.Division => 1,
			EUnitKind.Corps => 2,
			EUnitKind.Army => 3,
			EUnitKind.ArmyGroup or EUnitKind.Theatre => 4,
			_ => 0,
		};
		if (wanted == 0 || leader.Rank == wanted)
		{
			return;
		}
		// Whether loading the file will give him the unit's rank is the game's fix, for a land leader.
		bool made = words.State.Rules.Has(EFix.OobLeaderRanks) && leader.Type == ELeaderType.Land;
		text.Append('\n').Append(words.T(made ? RankMadeKey : RankWrongKey,
			("RANK", Whole(leader.Rank)), ("WANTED", Whole(wanted)), ("KIND", kind)));
	}

	/// <summary>A province's name, or its number where it has none.</summary>
	private static string ProvinceName(CGameState state, int id)
	{
		string name = id > 0 && id < state.Provinces.Count ? Text.CColouredText.Strip(state.Province(id).Name) : "";
		return name.Length != 0 ? name : Whole(id);
	}

	private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);
}
