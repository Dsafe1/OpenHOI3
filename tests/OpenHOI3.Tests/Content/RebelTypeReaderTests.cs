using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading <c>common/rebel_types.txt</c>, against <c>docs/mechanics/rebels.md</c>, section 2.
/// </summary>
public class RebelTypeReaderTests
{
	private static List<CRebelType> Read(string text, out CScriptContext context)
	{
		context = ScriptFixture.Context();
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) },
			System.Array.Empty<CCountry>())
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				["partisan_brigade"] = new CSubUnitType("partisan_brigade", EUnitBranch.Land),
			},
		};
		state.Defines.Set(CRebelTypeReader.Section, "YEARS_UNTIL_BROKEN", CFixedPoint.FromInt(2));
		var read = new List<CRebelType>();
		CRebelTypeReader.ReadFile(ScriptFixture.Parse(text, context), read, state, context);
		return read;
	}

	[Fact]
	public void A_type_reads_its_keys_and_three_of_them_are_yes_for_any_word_but_one()
	{
		List<CRebelType> read = Read(@"
			partisans = {
				ideology = any
				area = nation
				defection = none
				independence = none
				unit = partisan_brigade
				reinforcing = yes
				smart = yes
				auto_convert = yes
				spawn_chance = { factor = 10 modifier = { factor = 0.0 controlled_by = owner } }
				movement_evaluation = { factor = 1 }
				siege_won_trigger = { }
				siege_won_effect = { }
			}
			patriot_rebels = {
				area = culture
				defection = any
				independence = any
				defect_delay = 6
				ideology = stalinist
				is_triggered_only = yes
			}", out CScriptContext context);

		Assert.Equal(new[] { "partisans", "patriot_rebels" }, read.Select(type => type.Name));
		CRebelType partisans = read[0];
		Assert.False(partisans.AnyArea || partisans.Defection || partisans.Independence);
		Assert.True(partisans.Reinforcing && partisans.Smart && partisans.AutoConvert);
		Assert.Null(partisans.Ideology);
		Assert.Equal("partisan_brigade", partisans.Unit);
		Assert.Equal(10, partisans.SpawnChance.Days);
		Assert.Single(partisans.SpawnChance.Modifiers);
		// Two years until broken are 24 months where the type gives none.
		Assert.Equal(24, partisans.DefectDelay);

		CRebelType patriots = read[1];
		Assert.True(patriots.AnyArea && patriots.Defection && patriots.Independence && patriots.IsTriggeredOnly);
		Assert.Equal(6, patriots.DefectDelay);
		Assert.Equal("stalinist", patriots.Ideology);
		Assert.Equal(1, patriots.Index);
		Assert.Empty(context.Problems.All);
	}

	[Fact]
	public void A_key_the_loader_does_not_name_and_a_unit_nobody_has_are_reported()
	{
		Read(@"
			nationalist_rebels = {
				unit_transfer = yes
				unit = militia_brigade
			}", out CScriptContext context);

		Assert.Equal(new[] { EScriptProblem.UnknownKey, EScriptProblem.UnknownName }, context.Problems.All.Select(problem => problem.Kind));
	}
}
