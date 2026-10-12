using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading an events file, against <c>docs/mechanics/script.md</c>, sections 3 and 4.
/// </summary>
public class EventReaderTests
{
	private static List<CEvent> Read(string text, CScriptContext context)
	{
		var events = new List<CEvent>();
		CEventReader.ReadFile(ScriptFixture.Parse(text, context), events, context);
		return events;
	}

	[Fact]
	public void An_event_reads_its_keys_its_options_and_its_immediate()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEvent> events = Read(@"
			country_event = {
				id = 1001
				is_triggered_only = yes
				major = YES
				title = ""EVT_1001_NAME""
				desc = ""EVT_1001_DESC""
				picture = ""anschluss""
				trigger = { ai = yes }
				trigger = { war = no }
				immediate = { money = 5 }
				option = {
					name = ""EVT_1001_A""
					ai_chance = { factor = 0.75 }
					money = 10
					add_core = 42
				}
				option = { name = ""EVT_1001_B"" }
			}
			province_event = { id = 1002 title = t desc = d option = { name = o } }", context);

		Assert.Equal(2, events.Count);
		CEvent read = events[0];
		Assert.False(read.IsProvinceEvent);
		Assert.Equal(1001, read.Id);
		Assert.True(read.IsTriggeredOnly);
		Assert.True(read.Major);
		Assert.False(read.FireOnlyOnce);
		Assert.Equal("EVT_1001_NAME", read.Title);
		Assert.Equal(2, read.Trigger.Children.Count);
		Assert.Single(read.Immediate.Effects);
		Assert.Equal(2, read.Options.Count);
		Assert.Equal("EVT_1001_A", read.Options[0].Name);
		Assert.Equal(new CFixedPoint(750), read.Options[0].AiChance.AiFactor);
		Assert.Equal(2, read.Options[0].Effects.Count);
		Assert.True(events[1].IsProvinceEvent);
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void What_an_event_lacks_and_does_not_take_is_reported_and_the_event_kept()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEvent> events = Read("country_event = { id = 7 color = red } news_event = { id = 8 }", context);

		Assert.Single(events);
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.UnknownKey).Length);
		Assert.Equal(3, ScriptFixture.Of(context, EScriptProblem.Structure).Length);
	}

	[Fact]
	public void A_mean_time_to_happen_counts_days_and_its_modifiers_conditions()
	{
		CScriptContext context = ScriptFixture.Context();
		CEvent read = Assert.Single(Read(@"
			country_event = {
				id = 1 title = t desc = d option = { name = o }
				mean_time_to_happen = {
					days = 10
					months = 2
					modifier = { factor = 0.5 ai = yes war = yes }
					weeks = 1
				}
			}", context));

		Assert.Equal(60, read.MeanTimeToHappen.Days);
		CMeanTimeToHappenModifier modifier = Assert.Single(read.MeanTimeToHappen.Modifiers);
		Assert.Equal(new CFixedPoint(500), modifier.Factor);
		Assert.Equal(2, modifier.Condition.Children.Count);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}

	[Fact]
	public void Two_events_of_one_id_and_an_id_no_event_has_are_reported()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CEvent> events = Read(@"
			country_event = { id = 5 title = t desc = d option = { name = o country_event = 6 } }
			country_event = { id = 5 title = t desc = d option = { name = o country_event = 5 } }", context);

		CScriptLoader.CheckAcrossFiles(events, new List<CDecision>(), context);

		Assert.Single(ScriptFixture.Of(context, EScriptProblem.Quirk));
		Assert.Equal(new[] { "no event has the id 6" }, ScriptFixture.Of(context, EScriptProblem.UnknownName));
	}
}
