using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the tech and production scripts are handed, against <c>docs/mechanics/ministers.md</c>: the shares the
/// leadership and investment commands make, half away from nought; <c>FastNormalizeByPriority</c>; the tech minister's
/// <c>CanResearch</c>, a level's year and the database by its exact key; the unit order from a <c>SubUnitList</c>; and the
/// building order's <c>IsValid</c>. Germany owns Berlin (1) and Kiel (2).
/// </summary>
public sealed class LuaMinisterTests : IDisposable
{
	private static readonly CCountryTag Ger = new("GER");

	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luaministers-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaMinisterTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load()
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), "");
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static CGameState World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, Manpower = CFixedPoint.FromInt(50) };
		var technologies = new[]
		{
			new CTechnology("smallarms_technology", 10, new CModifier("smallarms_technology"))
				{ StartYear = 1918, FirstOffset = 1936, AdditionalOffset = 2 },
			new CTechnology("radar", 1, new CModifier("radar")) { StartYear = 1939 },
		};
		var infantry = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		infantry.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Kiel", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany }, buildings: new[] { Industry }, technologies: technologies)
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase) { [infantry.Key] = infantry },
		};
	}

	private const string Tag = "CCountryDataBase.GetTag('GER')";

	private static T Posted<T>(CGameState state) where T : CCommand => Assert.IsType<T>(Assert.Single(state.Outbox));

	[Fact]
	public void The_leadership_command_rounds_each_share_half_away_from_nought_as_Englands_save_holds()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		scripts.Ask(state, $"CCurrentGameState.Post(CChangeLeadershipCommand({Tag}, 0.2, 0.08, 0.03, 0.69))");
		Posted<CChangeLeadershipCommand>(state).Execute(state);

		// The save's 0.20001 0.07999 0.03000 0.69000: officers first.
		Assert.Equal(new long[] { 6554, 2621, 983, 22610 }, state.Country(Ger)!.LeadershipShares.Select(share => share.Raw));
	}

	[Fact]
	public void The_investment_command_gives_an_empty_share_0_00005_as_2_where_truncating_gives_1()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		scripts.Ask(state, $"CCurrentGameState.Post(CChangeInvestmentCommand({Tag}, 0.00005, 0.00005, 0.5, 0.5, 0, -1))");
		Posted<CChangeInvestmentCommand>(state).Execute(state);

		long[] shares = state.Country(Ger)!.ProductionShares.Select(share => share.Raw).ToArray();
		Assert.Equal(2, shares[0]);
		Assert.Equal(2, shares[1]);
		Assert.Equal(16382, shares[2]);
		// Below nought is nought.
		Assert.Equal(0, shares[5]);
	}

	[Fact]
	public void FastNormalizeByPriority_serves_the_level_s_order_each_a_little_more_and_at_most_what_is_left()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		double At(int result, string figures) =>
			(double)scripts.Ask(state, $"select({result}, CAI.FastNormalizeByPriority(0, {figures}))")!;

		// Nothing asked: every share 0.00005 as a float, and the rest left over.
		Assert.Equal((double)(float)0.00005, At(1, "0, 0, 0, 0, 0, 0"));
		Assert.Equal((double)(float)0.00005, At(6, "0, 0, 0, 0, 0, 0"));
		Assert.True(At(7, "0, 0, 0, 0, 0, 0") > 0.9996);

		// Production comes fifth at level 0 and gets what is left - consumer, lend-lease, supply and reinforcement first.
		Assert.Equal(1 - 3 * 0.00005 - 0.10005, At(3, "0, 0, 1.2, 0, 0.1, 0"), 5);
		Assert.Equal(0.0, At(6, "0, 0, 1.2, 0, 0.1, 0"));
		Assert.Equal(0.0, At(7, "0, 0, 1.2, 0, 0.1, 0"));
	}

	[Fact]
	public void The_tech_minister_may_start_a_technology_allowed_and_not_queued()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		const string minister = "CCountryDataBase.GetTag('GER'):GetCountry()";
		string CanResearch(string name) =>
			$"CTechnologyDataBase.GetTechnology('{name}') ~= nil and CTechnologyDataBase.GetTechnology('{name}'):GetKey():GetString()";

		Assert.Equal("smallarms_technology", scripts.Ask(state, CanResearch("smallarms_technology")));
		// Exact, case and all.
		Assert.Equal(false, scripts.Ask(state, CanResearch("Smallarms_technology")));
		// The latest of the end date's year and every technology's last level: 1936 + (10 - 2) x 2.
		Assert.Equal(1952.0, scripts.Ask(state, "CTechnologyDataBase.GetLatestTechYear()"));
		Assert.Equal(1918.0, scripts.Ask(state, $"{minister}:GetTechnologyStatus():GetYear(CTechnologyDataBase.GetTechnology('smallarms_technology'), 1)"));
		Assert.Equal(1936.0, scripts.Ask(state, $"{minister}:GetTechnologyStatus():GetYear(CTechnologyDataBase.GetTechnology('smallarms_technology'), 2)"));
		Assert.Equal(1940.0, scripts.Ask(state, $"{minister}:GetTechnologyStatus():GetYear(CTechnologyDataBase.GetTechnology('smallarms_technology'), 4)"));

		state.Country(Ger)!.ResearchQueue.Add("smallarms_technology");
		Assert.Equal(1.0, scripts.Ask(state, $"{minister}:GetNumberOfCurrentResearch()"));
		scripts.Ask(state, $"CCurrentGameState.Post(CStartResearchCommand({Tag}, CTechnologyDataBase.GetTechnology('smallarms_technology')))");
		Assert.False(Posted<CStartResearchCommand>(state).MayExecute(state));
	}

	[Fact]
	public void A_unit_order_is_its_list_s_brigades_in_order_and_its_reserve_must_be_a_boolean()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		const string order = "local list = SubUnitList() SubUnitList.Append(list, CSubUnitDataBase.GetSubUnit('infantry_brigade')) " +
			"SubUnitList.Append(list, CSubUnitDataBase.GetSubUnit('infantry_brigade')) ";

		scripts.Ask(state, "true");
		Exception? failed = Record.Exception(() => scripts.Lua.Run(System.Text.Encoding.Latin1.GetBytes(order +
			$"CCurrentGameState.Post(CConstructUnitCommand({Tag}, list, 99, 2, true, CNullTag(), CID()))"), "=test"));
		Assert.Null(failed?.Message);
		CConstructUnitCommand command = Posted<CConstructUnitCommand>(state);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		CConstruction line = Assert.Single(state.Country(Ger)!.Constructions);
		Assert.Equal(2, line.Size);
		Assert.True(line.IsReserve);
		Assert.Equal(2, line.Brigades.Count);

		Assert.Throws<CLuaException>(() => scripts.Lua.Run(System.Text.Encoding.Latin1.GetBytes(order +
			$"CConstructUnitCommand({Tag}, list, 1, 1, 1, CNullTag(), CID())"), "=test"));
	}

	[Fact]
	public void A_building_order_of_an_unknown_building_is_made_and_is_not_valid()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Equal(true, scripts.Ask(state, "CBuildingDataBase.GetBuilding('infrastructure') == nil"));
		Assert.Equal(false, scripts.Ask(state, $"CConstructBuildingCommand({Tag}, CBuildingDataBase.GetBuilding('infrastructure'), 1, 1):IsValid()"));
		Assert.Equal(1.0, scripts.Ask(state, "CBuildingDataBase.GetBuilding('industry'):GetIndex()"));
	}
}
