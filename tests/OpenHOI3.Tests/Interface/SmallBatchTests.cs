using System.Linq;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>The diplomacy panel's lists, a battle's popup and Disband All, against the specs named in each.</summary>
public class SmallBatchTests
{
	private static readonly CCountryTag Eng = new("ENG"), Ger = new("GER"), Fra = new("FRA");

	private static CGameState World()
	{
		var countries = new[]
		{
			new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" },
			new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
			new CCountry { Tag = Fra, Name = "France", DefinitionFile = "" },
		};
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Calais", isSea: false) };
		return new CGameState(new CDate(1939, 9, 1), provinces, countries);
	}

	private static CUnit Unit(CGameState state, CCountryTag owner, string name)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++state.LastUnitId, Location = 1, Name = name };
		state.Country(owner)!.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void TheFirstInfluenceOutgoingIsMarkedAndTheRowOtherwiseIncoming()
	{
		CGameState state = World();
		state.Influences.Add(new CInfluence(Eng, Fra, "", 0));
		var rows = CDiplomacyPanelLists.Drift(key => key, state, state.Country(Eng)!);
		Assert.Equal("@FRA France", Assert.Single(rows).Name);
		Assert.Equal("--", rows[0].Influence);
		Assert.Single(CDiplomacyPanelLists.Drift(key => key, state, state.Country(Fra)!));
	}

	[Fact]
	public void ACountryAtPeaceHasNoGoals()
	{
		CGameState state = World();
		var goal = new CWarGoal(new CCasusBelliType("conquer", 1), Eng, Ger);
		var war = new CWar("war");
		war.Attackers.Add(Eng);
		war.Defenders.Add(Ger);
		war.Goals[Eng] = new() { goal };
		state.Wars.Add(war);
		Assert.Empty(CDiplomacyPanelLists.GoalsBy(state, state.Country(Fra)!));
	}

	[Fact]
	public void TheNullCasusBelliIsNoGoal()
	{
		var goal = new CWarGoal(CCasusBelliType.Null, Eng, Ger);
		Assert.False(goal.Type.IsReal);
	}

	[Fact]
	public void ThePopupsColumnIsTheRawValueWithItsFirstWhiteDark()
	{
		var message = new CMessage(new OpenHOI3.Content.Messages.CMessageType("ATTACKUS"), " 0:00");
		message.Variables.Add(("ATTDETAIL", "a§Wb§Wc"));
		Assert.Equal("a§bb§Wc", CMessageText.Column(message, "ATTDETAIL"));
		Assert.Equal("", CMessageText.Column(message, "DEFDETAIL"));
	}

	[Theory]
	[InlineData("ATTACKUS", true)]
	[InlineData("AIRATTACKTHEM", true)]
	[InlineData("NAVALATTACKUS", true)]
	[InlineData("BOMBUS", false)]
	[InlineData("ELECTIONRESULTS", false)]
	public void OnlyTheSixBattleTypesUseTheBattlePopup(string name, bool combat)
	{
		Assert.Equal(combat, CMessageText.IsCombatStart(name));
	}

	[Fact]
	public void ThePopupNamesSixUnitsAndThenSaysThereAreMore()
	{
		CGameState state = World();
		var combat = new CCombat(1);
		for (int index = 0; index < 8; index++)
		{
			combat.Attacker.Units.Add(new CUnit(EUnitKind.Division, Eng) { Name = "Unit " + index });
		}
		string text = CBattleRowTooltip.ShortNames(key => key, state, combat.Attacker, 6, null);
		Assert.Equal(7, text.Split('\n').Length);
		Assert.EndsWith("AND_MORE", text);
	}

	[Fact]
	public void ANameIsCutToTwentyLetters()
	{
		CGameState state = World();
		var combat = new CCombat(1);
		combat.Attacker.Units.Add(new CUnit(EUnitKind.Division, Eng) { Name = new string('x', 30) });
		// A unit with no brigade is drawn by the unknown counter.
		Assert.Equal("@ENG £unknown " + new string('x', 20) + "\n", CBattleRowTooltip.ShortNames(key => key, state, combat.Attacker, 6, null));
	}

	[Fact]
	public void ASidesCountriesAreNamedOnceEachAndJoined()
	{
		CGameState state = World();
		var combat = new CCombat(1);
		foreach (CCountryTag tag in new[] { Eng, Fra, Eng, Ger })
		{
			combat.Attacker.Units.Add(new CUnit(EUnitKind.Division, tag));
		}
		Assert.Equal("England, France AND Germany", CBattleRowTooltip.CountryNames(key => key, state, combat.Attacker));
	}

	[Fact]
	public void DisbandAllLeavesWhatIsAboardOrRunByAnAgent()
	{
		CGameState state = World();
		CUnit free = Unit(state, Eng, "a");
		CUnit aboard = Unit(state, Eng, "b");
		aboard.Transport = new CUnit(EUnitKind.Navy, Eng);
		CUnit flying = Unit(state, Eng, "c");
		flying.Plane = new CUnit(EUnitKind.Air, Eng);
		Assert.Equal(free, Assert.Single(CAttachText.Disbandable(state, new[] { free, aboard, flying })));
	}

	[Fact]
	public void AnElectionsPieHasEveryPartyAtOneOrMore()
	{
		var scores = new long[] { 0, 90000, 0 };
		Assert.Equal(100, CMessageText.ElectionPercent(scores, 1));
		Assert.Equal(1, CMessageText.ElectionPercent(scores, 2));
	}

	[Fact]
	public void ATheatresLineKeepsItsLastCommaAndItsTooltipNamesTheTypes()
	{
		CGameState state = World();
		Assert.Equal(" Infantry\n Tanks\n ".TrimStart(), CTheatreScreenText.QueueNeedsTooltip(key => key == "infantry_brigade" ? "Infantry" : "Tanks", WithTypes(), "£infantry_brigade, £armor_brigade, ").TrimStart());
		Assert.NotNull(state);
	}

	private static CGameState WithTypes()
	{
		var countries = new[] { new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" } };
		return new CGameState(new CDate(1939, 9, 1), new[] { new CProvince(0, "", isSea: false) }, countries)
		{
			SubUnitTypes = new System.Collections.Generic.Dictionary<string, CSubUnitType>
			{
				["infantry_brigade"] = new("infantry_brigade", EUnitBranch.Land),
				["armor_brigade"] = new("armor_brigade", EUnitBranch.Land),
			},
		};
	}

	[Fact]
	public void AnUnknownTypeInTheLineIsLeftOut()
	{
		Assert.Equal("3 \n", CTheatreScreenText.QueueNeedsTooltip(key => key, WithTypes(), "3 £nothing,"));
	}

	[Fact]
	public void AUnitInABattleMayNotBeDisbanded()
	{
		CGameState state = World();
		CUnit unit = Unit(state, Eng, "a");
		Assert.True(CAttachText.MayDisband(state, unit));
		unit.Combats.Add(new CCombat(1));
		Assert.False(CAttachText.MayDisband(state, unit));
	}

	[Fact]
	public void TheAirBuildersLinesAreOneATechnology()
	{
		var country = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" };
		var type = new CSubUnitType("interceptor", EUnitBranch.Air);
		var technology = new OpenHOI3.Sim.Research.CTechnology("engine", 5, new OpenHOI3.Sim.Modifiers.CModifier("engine"));
		type.Technologies.Add(technology);
		country.TechnologyLevels["engine"] = 2;
		string line = Assert.Single(CBrigadeTooltips.DescriptionLines(key => key, key => key == "engine_2" ? "Mark II" : null, country, type));
		Assert.Equal("engine : §YMark II§W", line);
	}
}
