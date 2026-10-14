using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Intelligence;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the intelligence screen writes, but for the estimates: the player's own figures, a row's, the chosen country's
/// spies, which missions it lists, when the coup may be pressed and which constructions a roll shows. See
/// <c>docs/mechanics/espionagescreen.md</c>.
/// </summary>
public static class CEspionageScreenText
{
	/// <summary>The missions listed about the player itself, and about anybody else.</summary>
	public static readonly ESpyMission[] OwnMissions = { ESpyMission.CounterEspionage, ESpyMission.BoostRulingParty, ESpyMission.RaiseNationalUnity };

	public static readonly ESpyMission[] TheirMissions =
	{
		ESpyMission.CounterEspionage, ESpyMission.Military, ESpyMission.Technology, ESpyMission.BoostRulingParty,
		ESpyMission.BoostOurParty, ESpyMission.LowerNationalUnity, ESpyMission.IncreaseThreat, ESpyMission.CovertOps,
	};

	/// <summary>The localisation keys of the nine missions' titles, in their order.</summary>
	private static readonly string[] MissionKeys =
	{
		"COUNTER_ESPIONAGE", "MILITARY", "TECH", "BOOST_RULING_PARTY", "BOOST_OUR_PARTY", "LOWER_NATIONAL_UNITY",
		"INCREASE_THREAT", "RAISE_NATIONAL_UNITY", "COVERT_OPS",
	};

	/// <summary>More than this share of the most spies at home shows the foreign-actions mark: three in ten.</summary>
	private const long ActionsShown = 300;

	public static string MissionTitle(Func<string, string> localise, ESpyMission mission) =>
		localise("SPYMISSION_" + MissionKeys[(int)mission] + "_TITLE");

	public static string MissionDescription(Func<string, string> localise, ESpyMission mission) =>
		localise("SPYMISSION_" + MissionKeys[(int)mission] + "_DESC");

	public static string Whole(long thousandths) => (thousandths / 1000).ToString(CultureInfo.InvariantCulture);

	public static string FreeSpies(CCountry player) => Whole(player.FreeSpies.Raw);

	public static string HomeSpies(CCountry player) => Whole(CGameState.Presence(player, player).Level);

	/// <summary>Every country's owning a province but the player's spies there, each whole, added up.</summary>
	public static string ForeignSpies(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		long whole = 0;
		foreach (CCountry country in state.Countries)
		{
			if (owns[country.Index] && country != player)
			{
				whole += CGameState.Presence(player, country).Level / 1000;
			}
		}
		return whole.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>Red above 50, yellow above nought, no colour else - and nothing to close it.</summary>
	public static string PartisanEfficiency(CCountry player)
	{
		CFixedPoint value = player.Modifier["partisan_efficiency"];
		string colour = value.Raw > 50000 ? CColouredText.In(CColouredText.Red) : value.Raw > 0 ? CColouredText.In(CColouredText.Yellow) : "";
		return colour + CNumberText.Trimmed(value);
	}

	public static string SpiesCaught(CCountry player) => player.SpiesCaught.ToString(CultureInfo.InvariantCulture);

	/// <summary>The effective neutrality, rounded to a whole number.</summary>
	public static string Neutrality(CCountry player) =>
		Math.Round(player.EffectiveNeutrality.Raw / 1000.0, MidpointRounding.ToEven).ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// More than three spies at home in ten, and some country owning a province with spies in the player - the player
	/// itself counting.
	/// </summary>
	public static bool ShowsForeignActions(CGameState state, CCountry player)
	{
		long most = state.Defines["country", CSpyPlacing.MostSpies].Raw / 1000 * 1000;
		if (most <= 0 || CGameState.Presence(player, player).Level * 1000 / most <= ActionsShown)
		{
			return false;
		}
		bool[] owns = CRelations.Owning(state);
		foreach (CCountry country in state.Countries)
		{
			if (owns[country.Index] && CGameState.Presence(country, player).Level > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A priority button's frame: the priority plus one.</summary>
	public static int PriorityFrame(int priority) => priority + 1;

	/// <summary>A priority button clicked or right-clicked: the next frame, 4 going round to 1 and 1 to 4.</summary>
	public static int NextFrame(int frame, bool up) => up ? (frame >= 4 ? 1 : frame + 1) : (frame <= 1 ? 4 : frame - 1);

	/// <summary>The player's spies in the chosen country, green above 0.6 of knowing it, yellow above 0.3, else red.</summary>
	public static string ChosenSpies(CCountry player, CCountry chosen)
	{
		float factor = (float)(CCountryIntel.Factor(player, chosen) / 1000.0);
		char colour = factor > 0.6f ? CColouredText.Green : factor > 0.3f ? CColouredText.Yellow : CColouredText.Red;
		return CColouredText.In(colour) + Whole(CGameState.Presence(player, chosen).Level) + CColouredText.In('W');
	}

	public static string RowThreat(CCountry player, CCountry country) =>
		CNumberText.Format(CGameState.Status(player, country).Threat, 1);

	public static string RowCovert(CCountry player, CCountry country) => Whole(CGameState.Presence(player, country).CovertOpsPoints);

	/// <summary>A row's spies as made, whole; each frame after it trimmed.</summary>
	public static string RowSpies(CCountry player, CCountry country, bool made) => made
		? Whole(CGameState.Presence(player, country).Level)
		: CNumberText.Trimmed(new CFixedPoint(CGameState.Presence(player, country).Level));

	/// <summary>The coup's button: a whole spy there, the points it costs there, and a coup that may be attempted.</summary>
	public static bool MayCoup(CGameState state, CCountry player, CCountry chosen)
	{
		CSpyPresence presence = CGameState.Presence(player, chosen);
		return presence.Level / 1000 > 0 && presence.CovertOpsPoints >= state.Defines["country", "COUP_COVERT_OPS_COST"].Raw
			&& CCoup.CanAttempt(state, player, chosen);
	}

	/// <summary>The production list's chance, in whole percent: fifty at full knowledge.</summary>
	public static long ProductionChance(CGameState state, CCountry player, CCountry chosen)
	{
		long factor = CCountryIntel.Factor(player, chosen);
		return 100000 * state.Defines["country", "SPY_BASE_PRODUCTION_DETECTION_CHANCE"].Raw / 1000 * factor / 1000 / 1000;
	}

	/// <summary>
	/// The constructions a roll shows, in their order: the C runtime's generator stepped twice from the construction's
	/// number plus the month's key, the product of the two draws' top halves, under the chance.
	/// </summary>
	public static List<CConstruction> SeenConstructions(CGameState state, CCountry player, CCountry chosen)
	{
		long chance = ProductionChance(state, player, chosen);
		int key = CEspionageEstimates.MonthKey(state, player);
		var seen = new List<CConstruction>();
		foreach (CConstruction construction in chosen.Constructions)
		{
			if (Roll(construction.Id + key) < chance)
			{
				seen.Add(construction);
			}
		}
		return seen;
	}

	/// <summary>The production roll for a seed, 0 to 99.</summary>
	public static int Roll(int seed)
	{
		unchecked
		{
			uint first = (uint)seed * 214013u + 2531011u;
			uint second = first * 214013u + 2531011u;
			return (int)((((second >> 16) & 0x7FFF) * ((first >> 16) & 0x7FFF)) % 100);
		}
	}
}
