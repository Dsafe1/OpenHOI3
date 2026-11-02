using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A new game's laws brought forward, against <c>docs/mechanics/politics.md</c>, section 13: to the latest law of a group
/// that is allowed, not past a law that gives units starting experience, and never back.
/// </summary>
public class StartLawsTests
{
	private static CLaw Law(string group, string name, string? startExperience = null)
	{
		var modifier = new CModifier(name);
		if (startExperience is not null)
		{
			modifier.Set(CStartLaws.StartExperience, CFixedPoint.Parse(startExperience));
		}
		return new CLaw(group, modifier);
	}

	private static CLawGroup Group(string name, int index, params CLaw[] laws)
	{
		var group = new CLawGroup(name, index);
		group.Laws.AddRange(laws);
		return group;
	}

	[Fact]
	public void EachLawMovesToTheLatestOfItsGroupButNotPastStartingExperienceOrBack()
	{
		CLaw volunteer = Law("conscription_law", "volunteer_army");
		CLaw oneYear = Law("conscription_law", "one_year_draft");
		CLaw twoYear = Law("conscription_law", "two_year_draft");
		CLaw minimal = Law("training_laws", "minimal_training");
		CLaw basic = Law("training_laws", "basic_training");
		CLaw specialist = Law("training_laws", "specialist_training", startExperience: "10");
		CLaw free = Law("press_laws", "free_press");
		CLaw propaganda = Law("press_laws", "propaganda_press");

		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "", Capital = 1 };
		germany.Laws["conscription_law"] = volunteer;
		germany.Laws["training_laws"] = minimal;
		germany.Laws["press_laws"] = propaganda;
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany })
		{
			LawGroups = new[]
			{
				Group("conscription_law", 1, volunteer, oneYear, twoYear),
				Group("training_laws", 2, minimal, basic, specialist),
				Group("press_laws", 3, free, propaganda),
			},
		};

		Assert.Equal(1, CStartLaws.Move(state, germany));
		Assert.Same(twoYear, germany.Laws["conscription_law"]);    // the latest, every one being allowed
		Assert.Same(minimal, germany.Laws["training_laws"]);       // its last law gives starting experience: left alone
		Assert.Same(propaganda, germany.Laws["press_laws"]);       // already the latest
	}
}
