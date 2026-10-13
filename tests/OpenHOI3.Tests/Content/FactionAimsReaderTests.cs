using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading <c>common/faction_aims.txt</c>, against <c>docs/mechanics/factions.md</c>, section 8: a block a faction, a
/// block a condition, its trigger and whether it is primary; anything else reported.
/// </summary>
public class FactionAimsReaderTests
{
	[Fact]
	public void Each_factions_conditions_are_read_in_order_and_what_is_not_theirs_is_reported()
	{
		CGameState state = FactionTests.World();
		CScriptContext context = ScriptFixture.Context();
		var read = new List<(string Faction, List<CVictoryCondition> Conditions)>();

		CFactionAimsReader.ReadText(
			"Axis = { sealion = { trigger = { 2 = { controller = { faction = axis } } } primary = yes } " +
			"barbarossa = { trigger = { always = yes } colour = red } } " +
			"illuminati = { secret = { trigger = { always = yes } } }",
			state, read, context);

		(string faction, List<CVictoryCondition> conditions) = Assert.Single(read);
		Assert.Equal("axis", faction);
		Assert.Equal(new[] { "sealion", "barbarossa" }, conditions.ConvertAll(c => c.Name));
		Assert.Equal(new[] { true, false }, conditions.ConvertAll(c => c.Primary));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownName));
	}
}
