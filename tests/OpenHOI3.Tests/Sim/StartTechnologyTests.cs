using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A new game's head start in technology, against <c>docs/mechanics/research.md</c>, section 9: the budget from the core
/// provinces' leadership, and the cheapest due level bought at a time.
/// </summary>
public class StartTechnologyTests
{
	private static readonly CCountryTag Chi = new("CHI");

	private static CTechnology Technology(string name, int maxLevel, int difficulty, int start, int first, int additional,
		string folder = "infantry_folder")
	{
		return new CTechnology(name, maxLevel, new CModifier(name))
		{
			Difficulty = difficulty,
			StartYear = start,
			FirstOffset = first,
			AdditionalOffset = additional,
			Folder = folder,
		};
	}

	/// <summary>A country whose cores are the provinces given leadership, and one more it owns that is not a core.</summary>
	private static CGameState State(CCountry country, string[] leadership, params CTechnology[] technologies)
	{
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int i = 0; i < leadership.Length; i++)
		{
			var province = new CProvince(i + 1, $"P{i + 1}", isSea: false) { Leadership = CFixedPoint.Parse(leadership[i]) };
			province.Cores.Add(country.Tag);
			provinces.Add(province);
		}
		provinces.Add(new CProvince(leadership.Length + 1, "Not a core", isSea: false)
		{
			Owner = country.Tag,
			Controller = country.Tag,
			Leadership = CFixedPoint.FromInt(4),
		});
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { country }, technologies: technologies)
		{
			StartDate = new CDate(1936, 1, 1),
		};
	}

	private static CCountry China() => new() { Tag = Chi, Name = "China", DefinitionFile = "", Capital = 1 };

	[Fact]
	public void TheBudgetIsTheCoresLeadershipHeldToFiveTimes3650Times1917()
	{
		// Germany in the start save: core leadership far over five, a budget of 34 985.
		CCountry germany = China();
		Assert.Equal(34985, CStartTechnology.Budget(State(germany, new[] { "3", "4" }), germany));

		// One point of leadership on the cores; the province it owns that is no core counts nothing.
		CCountry china = China();
		Assert.Equal(6997, CStartTechnology.Budget(State(china, new[] { "0.6", "0.4" }), china));
	}

	[Fact]
	public void TheCheapestDueLevelIsBoughtAtATimeAndOneNotYetDueCostsDouble()
	{
		CCountry china = China();
		CTechnology theory = Technology("infantry_theory", 10, 0, 1900, 1901, 1, CStartTechnology.TheoryFolder);
		CTechnology activation = Technology("militia_activation", 1, 0, 1910, 0, 0);
		CTechnology practical = Technology("militia_practical_tech", 3, 0, 1930, 1940, 2);
		CTechnology rifles = Technology("smallarms_technology", 10, 1, 1920, 1925, 5);
		china.TechnologyLevels["militia_activation"] = 1;
		CGameState state = State(china, new[] { "1" }, theory, activation, practical, rifles);

		// 6997: the null technology at 125 first (1250 off); then the practical's first level at 125; then the rifles at
		// 137.5, the practical's second level not being due until 1940 and so costing 250 - four times, the fourth taking
		// the budget below nought. The theory is never a candidate, nor the one-level technology the country has.
		Assert.Equal(6, CStartTechnology.Buy(state, china));
		Assert.Equal(1, china.TechnologyLevels["militia_practical_tech"]);
		Assert.Equal(4, china.TechnologyLevels["smallarms_technology"]);
		Assert.False(china.TechnologyLevels.ContainsKey("infantry_theory"));
		Assert.Equal(1, china.TechnologyLevels["militia_activation"]);
	}

	[Fact]
	public void ACountryWithNoCoreLeadershipBuysNothing()
	{
		CCountry china = China();
		CGameState state = State(china, new[] { "0" }, Technology("smallarms_technology", 10, 0, 1920, 1925, 5));

		Assert.Equal(0, CStartTechnology.Buy(state, china));
		Assert.Empty(china.TechnologyLevels);
	}
}
