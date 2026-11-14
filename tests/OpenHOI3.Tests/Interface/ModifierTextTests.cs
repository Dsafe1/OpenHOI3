using System.Linq;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// A modifier written out, against <c>docs/mechanics/modifiers.md</c>, section 8: a source's
/// line, the sources of one kind, every value of a modifier, and how each kind is written.
/// </summary>
public class ModifierTextTests
{
	private static string Localise(string key) => key switch
	{
		"base_values" => "Base Value",
		"war_economy" => "War Economy",
		"dissent" => "Dissent",
		"MODIFIER_GLOBAL_IC" => "IC",
		"MODIFIER_DISSENT" => "Daily Dissent Change",
		_ => key,
	};

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	[Fact]
	public void A_percentage_is_a_hundred_times_its_value_to_one_place()
	{
		CModifierKind ic = CModifierKinds.Of("global_ic");
		Assert.Equal("§G+10.0%§W", CModifierText.Value(ic, CFixedPoint.Parse("0.1")));
		Assert.Equal("§R-7.5%§W", CModifierText.Value(ic, CFixedPoint.Parse("-0.075")));
	}

	[Fact]
	public void A_plain_number_is_written_to_two_places()
	{
		Assert.Equal("§G+4.00§W", CModifierText.Value(CModifierKinds.Of("global_manpower"), CFixedPoint.FromInt(4)));
	}

	[Fact]
	public void Where_more_is_worse_a_positive_value_is_red()
	{
		Assert.Equal("§R+5.0%§W", CModifierText.Value(CModifierKinds.Of("dissent"), CFixedPoint.Parse("0.05")));
		Assert.Equal("§G-5.0%§W", CModifierText.Value(CModifierKinds.Of("dissent"), CFixedPoint.Parse("-0.05")));
	}

	[Fact]
	public void A_kind_is_written_as_the_original_builds_it()
	{
		Assert.Equal(new CModifierKind("attrition", "ATTRITION", false, false), CModifierKinds.Of("attrition"));
		Assert.Equal(new CModifierKind("national_unity", "MODIFIER_NATIONAL_UNITY", false, true), CModifierKinds.Of("national_unity"));
		Assert.Equal(new CModifierKind("national_unity_effect", "MODIFIER_NATIONAL_UNITY_EFFECT", true, false),
			CModifierKinds.Of("national_unity_effect"));
		Assert.Equal(107, CModifierKinds.All.Count);
		// A mission efficiency is written by the mission's key and named by the key and _eff.
		Assert.True(CModifierKinds.IsKnown("convoy_raid"));
		Assert.False(CModifierKinds.IsKnown("convoy_raid_eff"));
		Assert.Equal(new CModifierKind("convoy_raid", "convoy_raid_eff", true, true), CModifierKinds.Of("convoy_raid"));
		Assert.True(CModifierKinds.IsKnown("suseptibility_axis"));
		Assert.False(CModifierKinds.IsKnown("made_up"));
	}

	[Fact]
	public void The_kinds_written_are_the_kinds_a_file_may_name_in_the_same_order()
	{
		Assert.Equal(OpenHOI3.Sim.Modifiers.CModifierKeys.Engine, CModifierKinds.All.Select(kind => kind.Key).ToArray());
		Assert.Equal(107, CModifierKinds.Order("support_attack"));
		Assert.Equal(107 + 8, CModifierKinds.Order("convoy_escort"));
		Assert.Equal(107 + 32, CModifierKinds.Order("suseptibility_axis"));
	}

	[Fact]
	public void The_sources_of_a_kind_are_a_line_each_and_those_without_it_write_nothing()
	{
		var sources = new[]
		{
			new CModifierSource(Modifier("base_values", ("global_manpower", "4"))),
			new CModifierSource(Modifier("war_economy", ("global_ic", "0.1"))),
			new CModifierSource(Modifier("", ("ic", "1")), "Berlin"),
			new CModifierSource(Modifier("dissent", ("global_ic", "-0.075"))),
		};

		Assert.Equal("War Economy: §G+10.0%§W\nDissent: §R-7.5%§W", CModifierText.Sources(Localise, sources, "global_ic"));
		Assert.Equal("", CModifierText.Sources(Localise, sources, "officer_recruitment"));
	}

	[Fact]
	public void A_source_after_the_last_line_that_writes_nothing_still_leaves_its_line_break()
	{
		var sources = new[]
		{
			new CModifierSource(Modifier("", ("ic", "1")), "Berlin"),
			new CModifierSource(Modifier("dissent", ("global_ic", "-0.075"))),
		};

		// The original's: the tooltip's own trimming takes it off the end.
		Assert.Equal("Berlin: §G+1.00§W\n", CModifierText.Sources(Localise, sources, "ic"));
	}

	[Fact]
	public void Every_value_is_a_line_under_the_kinds_own_name_in_the_originals_order()
	{
		CModifier dissent = Modifier("dissent", ("dissent", "0.02"), ("global_ic", "-0.075"));

		Assert.Equal("IC: §R-7.5%§W\nDaily Dissent Change: §R+2.0%§W", CModifierText.AllValues(Localise, dissent));
	}
}
