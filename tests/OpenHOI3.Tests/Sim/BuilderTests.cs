using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a country may order and what a new unit is called, against
/// <c>docs/mechanics/builders.md</c>, sections 2, 4, 5 and 8.
/// </summary>
public class BuilderTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	private static CSubUnitType Type(string key, EUnitBranch branch = EUnitBranch.Land, string group = "", string priority = "0")
	{
		var type = new CSubUnitType(key, branch) { UnitGroup = group };
		type.Stats.Values.Set(CSubUnitStats.BuildCostIc, CFixedPoint.FromInt(2));
		type.Stats.Values.Set(CSubUnitStats.BuildTime, CFixedPoint.FromInt(100));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		type.Stats.Values.Set("priority", CFixedPoint.Parse(priority));
		return type;
	}

	private static CCountry Germany()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		country.Manpower = CFixedPoint.FromInt(1000);
		country.TotalIc = 100;
		country.ProductionShares[(int)EProductionCategory.Production] = CFixed15.One;
		return country;
	}

	private static CGameState State(CCountry country, IEnumerable<CSubUnitType> types,
		IEnumerable<CTechnology>? technologies = null, IReadOnlyList<CCombinedArmsGroup>? groups = null,
		CUnitNameTexts? texts = null)
	{
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));
		var byKey = new Dictionary<string, CSubUnitType>(System.StringComparer.OrdinalIgnoreCase);
		foreach (CSubUnitType type in types)
		{
			byKey[type.Key] = type;
		}
		var home = new CProvince(1, "Berlin", isSea: false) { Owner = country.Tag, Controller = country.Tag };
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), home },
			new[] { country }, technologies: technologies, defines: defines)
		{
			SubUnitTypes = byKey,
			CombinedArmsGroups = groups ?? System.Array.Empty<CCombinedArmsGroup>(),
			NameTexts = texts ?? new CUnitNameTexts(),
		};
	}

	private static CUnitNameTexts English()
	{
		var texts = new CUnitNameTexts
		{
			Division = "$NUM$$ORDER$ $TYPE$ Division",
			Brigade = "$NUM$$ORDER$ $TYPE$ Brigade",
			SubUnit = "$NUM$$ORDER$ $TYPE$",
			Ordinals = new[] { "th", "st", "nd", "rd" },
		};
		texts.ShortNames["infantry_brigade"] = "Inf";
		texts.ShortNames["destroyer"] = "DD";
		return texts;
	}

	/// <summary>A division of so many brigades of a type, in the field.</summary>
	private static void Raise(CCountry country, CSubUnitType type, int brigades)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag);
		for (int index = 0; index < brigades; index++)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Unit = unit });
		}
		country.Units.Add(unit);
	}

	// ---- what a country may build

	[Fact]
	public void ATypeItsFileHoldsBackIsUnlockedByATechnologyThatActivatesIt()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType engineers = Type("engineer_brigade");
		engineers.Flags[CUnitAvailability.Active] = false;
		var bridging = new CTechnology("engineer_brigade_activation", 1, new CModifier("")) { ActivatesUnits = new[] { "engineer_brigade" } };
		CGameState state = State(country, new[] { infantry, engineers }, new[] { bridging });

		Assert.True(CUnitAvailability.IsUnlocked(state, country, infantry));
		Assert.False(CUnitAvailability.IsUnlocked(state, country, engineers));
		Assert.Equal(0, CUnitAvailability.StillBuildable(state, country, engineers));
		Assert.Equal(new[] { infantry }, CUnitAvailability.Buildable(state, country, EUnitBranch.Land));

		country.TechnologyLevels["engineer_brigade_activation"] = 1;
		Assert.True(CUnitAvailability.IsUnlocked(state, country, engineers));
		Assert.Equal(CUnitAvailability.Unlimited, CUnitAvailability.StillBuildable(state, country, engineers));
		Assert.Equal(new[] { infantry, engineers }, CUnitAvailability.Buildable(state, country, EUnitBranch.Land));
	}

	[Fact]
	public void ATypeIsOnlyForTheCountriesItsFileNamesAndOneThatMayNotBeBuiltIsNotListed()
	{
		CCountry country = Germany();
		CSubUnitType alpini = Type("alpini_brigade");
		alpini.UsableBy.Add(Ita.Value);
		CSubUnitType headquarters = Type("hq_brigade");
		headquarters.Flags[CUnitAvailability.IsBuildable] = false;
		CGameState state = State(country, new[] { alpini, headquarters });

		Assert.Equal(0, CUnitAvailability.StillBuildable(state, country, alpini));
		Assert.Empty(CUnitAvailability.Buildable(state, country, EUnitBranch.Land));
	}

	[Fact]
	public void ATypesAvailableTriggerIsAskedOfTheCountryAndAnEmptyOnePasses()
	{
		CCountry country = Germany();
		CSubUnitType guards = Type("guards_brigade");
		guards.AvailableTrigger = ScriptWorld.Condition("tag = ITA");
		CSubUnitType ss = Type("waffenSS_brigade");
		ss.AvailableTrigger = ScriptWorld.Condition("");
		CGameState state = State(country, new[] { guards, ss });

		Assert.Equal(0, CUnitAvailability.StillBuildable(state, country, guards));
		Assert.Equal(CUnitAvailability.Unlimited, CUnitAvailability.StillBuildable(state, country, ss));

		guards.AvailableTrigger = ScriptWorld.Condition("tag = GER");
		Assert.Equal(CUnitAvailability.Unlimited, CUnitAvailability.StillBuildable(state, country, guards));
	}

	[Fact]
	public void TheTriggerIsReadFromItsUnitsFileTheFirstBlockOfANameWinningAndTheRestOfTheFileNotReported()
	{
		CCountry country = Germany();
		CSubUnitType guards = Type("guards_brigade");
		CGameState state = State(country, new[] { guards });
		OpenHOI3.Content.Script.CScriptContext context = OpenHOI3.Tests.Content.ScriptFixture.Context();

		OpenHOI3.Content.Script.CUnitAvailableTriggerReader.ReadText(
			"guards_brigade = {\n\tusable_by = { SOV }\n\tavailable_trigger = { tag = GER }\n}\n" +
			"guards_brigade = {\n\tavailable_trigger = { tag = ITA }\n}\n",
			state, context, new HashSet<string>());

		Assert.Equal(0, context.Problems.Count);
		Assert.Equal(CUnitAvailability.Unlimited, CUnitAvailability.StillBuildable(state, country, guards));
	}

	[Fact]
	public void ACountryGivenEveryUnitToTryMayOrderWhatIsLockedOrAnothersAndNothingElse()
	{
		CCountry country = Germany();
		CSubUnitType carrier = Type("carrier", EUnitBranch.Naval);
		carrier.Flags[CUnitAvailability.Active] = false;
		CSubUnitType alpini = Type("alpini_brigade");
		alpini.UsableBy.Add(Ita.Value);
		alpini.Stats.Values.Set(CUnitAvailability.MinimumOfType, CFixedPoint.FromInt(6));
		CSubUnitType headquarters = Type("hq_brigade");
		headquarters.Flags[CUnitAvailability.IsBuildable] = false;
		CGameState state = State(country, new[] { carrier, alpini, headquarters });
		var order = new COrderUnitCommand(Ger, new[] { ("alpini_brigade", "") });
		Assert.False(order.MayExecute(state));

		var unlock = new CUnlockEveryUnitCommand(Ger);
		Assert.True(unlock.MayExecute(state));
		unlock.Execute(state);

		// The two locks are lifted; a ration holds, and what cannot be built still cannot.
		Assert.Equal(CUnitAvailability.Unlimited, CUnitAvailability.StillBuildable(state, country, carrier));
		Assert.Equal(6, CUnitAvailability.StillBuildable(state, country, alpini));
		Assert.Equal(new[] { alpini }, CUnitAvailability.Buildable(state, country, EUnitBranch.Land));
		Assert.True(order.MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new[] { ("hq_brigade", "") }).MayExecute(state));

		new CUnlockEveryUnitCommand(Ger, unlocked: false).Execute(state);
		Assert.Equal(0, CUnitAvailability.StillBuildable(state, country, carrier));
		Assert.False(order.MayExecute(state));
		Assert.False(new CUnlockEveryUnitCommand(Ita).MayExecute(state));
	}

	[Fact]
	public void AnEliteBrigadeIsRationedByItsNumberOrItsShareOfTheArmyWhicheverIsMore()
	{
		// The original's alpini: six of them, or four in a hundred of the infantry's group - every land brigade with a
		// width and as soft as infantry (FINDINGS-ministers.md C.5).
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType elite = Type("elite_brigade");
		CSubUnitType armour = Type("armor_brigade");
		foreach (CSubUnitType type in new[] { infantry, elite, armour })
		{
			type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.One);
			type.Stats.Values.Set(CUnitTotals.Softness, CFixedPoint.One);
		}
		armour.Stats.Values.Set(CUnitTotals.Softness, CFixedPoint.Parse("0.2"));
		elite.Stats.Values.Set(CUnitAvailability.MinimumOfType, CFixedPoint.FromInt(6));
		elite.Stats.Values.Set(CUnitAvailability.MaxPercentageOfType, CFixedPoint.Parse("0.04"));
		CGameState state = State(country, new[] { infantry, elite, armour });

		// Armour is a group of its own, and counts for nothing in the elite's share.
		Raise(country, armour, 500);
		Assert.Equal(6, CUnitAvailability.StillBuildable(state, country, elite));

		Assert.Equal(6, CUnitAvailability.StillBuildable(state, country, elite));

		Raise(country, infantry, 98);
		Raise(country, elite, 2);
		Assert.Equal(4, CUnitAvailability.StillBuildable(state, country, elite));           // six, less the two it has

		Raise(country, infantry, 100);
		Assert.Equal(6, CUnitAvailability.StillBuildable(state, country, elite));           // four in a hundred of 200, less two

		// What is being built counts for its whole series, and what waits to be placed counts.
		var line = new CConstruction(Ger) { Size = 3 };
		line.Brigades.Add(new CBrigadeOrder(elite));
		country.Constructions.Add(line);
		var waiting = new CUnit(EUnitKind.Division, Ger);
		waiting.SubUnits.Add(new CSubUnit(elite));
		country.Deployments.Add(waiting);
		Assert.Equal(2, CUnitAvailability.StillBuildable(state, country, elite));

		Assert.True(CUnitAvailability.MayBuildEach(state, country, new[] { elite }, 2));
		Assert.False(CUnitAvailability.MayBuildEach(state, country, new[] { elite }, 3));
		Assert.False(CUnitAvailability.MayBuildEach(state, country, new[] { elite, elite }, 2));
	}

	[Fact]
	public void ADivisionIsOfTwoBrigadesToTheMostTheDefineAndTechnologyAllow()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CGameState state = State(country, new[] { infantry });
		CSubUnitType[] Of(int count) => Enumerable.Repeat(infantry, count).ToArray();

		Assert.Equal(4, CUnitAvailability.MostBrigades(state, country));
		Assert.False(CUnitAvailability.MayBuildDivision(state, country, Of(1), 1));
		Assert.True(CUnitAvailability.MayBuildDivision(state, country, Of(2), 1));
		Assert.True(CUnitAvailability.MayBuildDivision(state, country, Of(4), 1));
		Assert.False(CUnitAvailability.MayBuildDivision(state, country, Of(5), 1));

		// The original's superior firepower: one more brigade.
		country.Technology.Set(CUnitAvailability.DivisionSize, CFixedPoint.One);
		Assert.Equal(5, CUnitAvailability.MostBrigades(state, country));
		Assert.True(CUnitAvailability.MayBuildDivision(state, country, Of(5), 1));
	}

	// ---- combined arms

	[Fact]
	public void CombinedArmsCountsOnlyBesideABrigadeOfABaseGroup()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade", group: "infantry_unit_type");
		CSubUnitType armour = Type("armor_brigade", group: "armor_unit_type");
		CSubUnitType artillery = Type("artillery_brigade", group: "artillery_unit_type");
		var groups = new[]
		{
			new CCombinedArmsGroup("infantry_unit_type", true, CFixedPoint.Zero),
			new CCombinedArmsGroup("armor_unit_type", false, CFixedPoint.Parse("0.05")),
			new CCombinedArmsGroup("artillery_unit_type", false, CFixedPoint.Parse("0.05")),
		};
		CGameState state = State(country, new[] { infantry, armour, artillery }, groups: groups);

		Assert.Equal(new[] { false, false, false }, CCombinedArms.Counted(state, new[] { armour, artillery }));
		Assert.Equal(CFixedPoint.Zero, CCombinedArms.Bonus(state, country, new[] { armour, artillery }));

		Assert.Equal(new[] { true, false, true }, CCombinedArms.Counted(state, new[] { infantry, infantry, artillery }));
		Assert.Equal(CFixedPoint.Parse("0.05"), CCombinedArms.Bonus(state, country, new[] { infantry, infantry, artillery }));

		// A group's number is its place in the file, counted from one; no group is nothing.
		Assert.Equal(1, CCombinedArms.GroupNumber(state, infantry));
		Assert.Equal(3, CCombinedArms.GroupNumber(state, artillery));
		Assert.Equal(0, CCombinedArms.GroupNumber(state, Type("police_brigade")));

		// A technology adds to a group by the group's own name.
		country.Technology.Set("armor_unit_type", CFixedPoint.Parse("0.1"));
		Assert.Equal(CFixedPoint.Parse("0.2"), CCombinedArms.Bonus(state, country, new[] { infantry, armour, artillery }));
	}

	// ---- names

	[Fact]
	public void ADivisionIsNamedFromItsFirstBrigadesListPassingOverNamesInUse()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CGameState state = State(country, new[] { infantry }, texts: English());
		country.UnitNames["infantry_brigade"] = new List<string> { "1. Infanterie-Division", "5. Infanterie-Division", "6. Infanterie-Division" };
		CUnitNames.Register(country, "1. Infanterie-Division");

		string first = CUnitNames.ForDivision(state, country, infantry);
		Assert.Equal("5. Infanterie-Division", first);
		Assert.Equal(1, country.UnitNameCursors["infantry_brigade"]);          // left on the name taken

		// Until it is registered the name is still free, and is offered again.
		Assert.Equal(first, CUnitNames.ForDivision(state, country, infantry));
		CUnitNames.Register(country, first);
		Assert.Equal("6. Infanterie-Division", CUnitNames.ForDivision(state, country, infantry));
		CUnitNames.Register(country, "6. Infanterie-Division");

		// The list has run out: a number is counted up.
		Assert.Equal("1st Inf Division", CUnitNames.ForDivision(state, country, infantry));
		CUnitNames.Register(country, "1st Inf Division");
		Assert.Equal("2nd Inf Division", CUnitNames.ForDivision(state, country, infantry));
	}

	[Fact]
	public void ANumberTakesTheEndingOfItsLastDigitAndOnlyElevenToThirteenAreSpared()
	{
		CUnitNameTexts texts = English();
		string Of(int number) => number + CUnitNames.Ordinal(texts, number);

		Assert.Equal(new[] { "1st", "2nd", "3rd", "4th", "10th", "11th", "12th", "13th", "21st", "22nd", "23rd" },
			new[] { 1, 2, 3, 4, 10, 11, 12, 13, 21, 22, 23 }.Select(Of));
		// The original's slip: 111 is not 11.
		Assert.Equal("111st", Of(111));
	}

	[Fact]
	public void AShipTakesItsOwnTypesListAndALandBrigadeNeverLooksAtOne()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType destroyer = Type("destroyer", EUnitBranch.Naval);
		CGameState state = State(country, new[] { infantry, destroyer }, texts: English());
		country.UnitNames["infantry_brigade"] = new List<string> { "1. Infanterie-Division" };
		country.UnitNames["destroyer"] = new List<string> { "1. Zerstörergeschwader" };

		Assert.Equal("1st Inf Brigade", CUnitNames.ForSubUnit(state, country, infantry));
		Assert.Equal("1. Zerstörergeschwader", CUnitNames.ForSubUnit(state, country, destroyer));
		CUnitNames.Register(country, "1. Zerstörergeschwader");
		Assert.Equal("1st DD", CUnitNames.ForSubUnit(state, country, destroyer));
	}

	[Fact]
	public void ANameWithNoNumberInItIsGivenAsItIs()
	{
		// A game with no text for the pattern would otherwise count for ever.
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CGameState state = State(country, new[] { infantry });

		Assert.Equal(CUnitNameTexts.DivisionKey, CUnitNames.ForDivision(state, country, infantry));
		CUnitNames.Register(country, CUnitNameTexts.DivisionKey);
		Assert.Equal(CUnitNameTexts.DivisionKey, CUnitNames.ForDivision(state, country, infantry));
	}

	// ---- the order

	[Fact]
	public void AnOrderNamesAUnitOfSeveralAndEachOfItsBrigadesAndAUnitOfOneOnlyItsBrigade()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType destroyer = Type("destroyer", EUnitBranch.Naval);
		CGameState state = State(country, new[] { infantry, destroyer }, texts: English());
		country.UnitNames["infantry_brigade"] = new List<string> { "44. Infanterie-Division" };
		var simulation = new CSimulation(state);

		simulation.Post(new COrderUnitCommand(Ger, new[] { ("infantry_brigade", ""), ("infantry_brigade", "") }));
		simulation.Post(new COrderUnitCommand(Ger, new[] { ("destroyer", "") }));
		simulation.Pump();

		CConstruction division = country.Constructions[0];
		Assert.Equal("44. Infanterie-Division", division.Name);
		Assert.Equal(new[] { "1st Inf Brigade", "2nd Inf Brigade" }, division.Brigades.Select(order => order.Name));
		CConstruction flotilla = country.Constructions[1];
		Assert.Equal("", flotilla.Name);
		Assert.Equal("1st DD", flotilla.Brigades[0].Name);
		Assert.Contains("44. Infanterie-Division", country.UnitNamesInUse);
		Assert.Contains("1st DD", country.UnitNamesInUse);
	}

	[Fact]
	public void AShipMayBeOrderedAtAnyLevelUpToItsCountrysAndNoHigher()
	{
		CCountry country = Germany();
		CSubUnitType destroyer = Type("destroyer", EUnitBranch.Naval);
		destroyer.Technologies.Add(new CTechnology("destroyer_engine", 12, new CModifier("")));
		destroyer.Technologies.Add(new CTechnology("destroyer_armour", 12, new CModifier("")));
		CGameState state = State(country, new[] { destroyer }, texts: English());
		country.TechnologyLevels["destroyer_engine"] = 3;
		country.TechnologyLevels["destroyer_armour"] = 1;
		COrderUnitCommand With(params int[] levels) =>
			new(Ger, new[] { ("destroyer", "") }, models: new[] { levels });

		Assert.True(With(3, 1).MayExecute(state));
		Assert.True(With(0, 0).MayExecute(state));
		Assert.False(With(4, 1).MayExecute(state));
		Assert.False(With(3, 2).MayExecute(state));
		Assert.False(With(-1, 0).MayExecute(state));
		Assert.False(With(1, 1, 1).MayExecute(state));

		var simulation = new CSimulation(state);
		simulation.Post(With(1, 0));
		simulation.Post(new COrderUnitCommand(Ger, new[] { ("destroyer", "") }));
		simulation.Pump();

		// Each level left out makes it a hundredth cheaper and quicker.
		Assert.Equal(new[] { 1, 0 }, country.Constructions[0].Brigades[0].Model);
		Assert.Equal(new[] { 3, 1 }, country.Constructions[1].Brigades[0].Model);
		Assert.Equal(CFixedPoint.FromInt(101), country.Constructions[0].Duration);
		Assert.Equal(CFixedPoint.FromInt(104), country.Constructions[1].Duration);
	}

	[Fact]
	public void AnOrderIsRefusedForWhatIsLockedPastItsRationOrTooManyBrigades()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType engineers = Type("engineer_brigade");
		engineers.Flags[CUnitAvailability.Active] = false;
		CSubUnitType elite = Type("elite_brigade");
		elite.Stats.Values.Set(CUnitAvailability.MinimumOfType, CFixedPoint.FromInt(2));
		CGameState state = State(country, new[] { infantry, engineers, elite }, texts: English());
		COrderUnitCommand Of(int size, params string[] keys) => new(Ger, keys.Select(key => (key, "")), size: size);

		Assert.False(Of(1, "engineer_brigade").MayExecute(state));
		Assert.True(Of(1, "infantry_brigade").MayExecute(state));                          // a brigade may be ordered alone
		Assert.True(Of(1, "infantry_brigade", "infantry_brigade", "infantry_brigade", "infantry_brigade").MayExecute(state));
		Assert.False(Of(1, "infantry_brigade", "infantry_brigade", "infantry_brigade", "infantry_brigade", "infantry_brigade").MayExecute(state));
		Assert.True(Of(2, "elite_brigade").MayExecute(state));
		Assert.False(Of(3, "elite_brigade").MayExecute(state));
		Assert.False(Of(2, "elite_brigade", "elite_brigade").MayExecute(state));
	}

	[Fact]
	public void AQuoteIsTheLineAnOrderWouldMakeWithNothingOrdered()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CGameState state = State(country, new[] { infantry }, texts: English());

		CConstruction quote = CBuildQueue.Quote(country, new[] { infantry, infantry }, false);
		Assert.Empty(country.Constructions);
		Assert.Equal(CFixedPoint.FromInt(1000), country.Manpower);
		Assert.Equal(CFixedPoint.FromInt(6), quote.Manpower);
		Assert.Equal(CFixedPoint.FromInt(100), quote.Duration);
		Assert.Equal(CFixedPoint.FromInt(4), quote.Cost);

		var simulation = new CSimulation(state);
		simulation.Post(new COrderUnitCommand(Ger, new[] { ("infantry_brigade", ""), ("infantry_brigade", "") }));
		simulation.Pump();
		Assert.Equal(quote.Cost, country.Constructions[0].Cost);
		Assert.Equal(quote.Duration, country.Constructions[0].Duration);
		Assert.Equal(quote.Manpower, country.Constructions[0].Manpower);
		Assert.Equal(quote.Cost, CBuildQueue.QueueCost(country));
	}

	// ---- templates

	[Fact]
	public void ADesignIsKeptAsATemplateByCommand()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Type("infantry_brigade");
		CSubUnitType artillery = Type("artillery_brigade");
		CGameState state = State(country, new[] { infantry, artillery });
		var template = new CDivisionTemplate("GER_Infantry_1936");
		template.Brigades.AddRange(new[] { "infantry_brigade", "infantry_brigade", "infantry_brigade" });
		country.Templates.Add(template);

		Assert.False(new CSetTemplateCommand(Ger, 1, new[] { "infantry_brigade" }).MayExecute(state));
		Assert.False(new CSetTemplateCommand(Ger, 0, new[] { "no_such_brigade" }).MayExecute(state));

		var simulation = new CSimulation(state);
		simulation.Post(new CSetTemplateCommand(Ger, 0, new[] { "infantry_brigade", "infantry_brigade", "artillery_brigade" }));
		simulation.Pump();
		Assert.Equal(new[] { "infantry_brigade", "infantry_brigade", "artillery_brigade" }, country.Templates[0].Brigades);
	}
}
