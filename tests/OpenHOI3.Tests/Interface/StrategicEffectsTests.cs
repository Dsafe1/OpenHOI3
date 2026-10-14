using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the Strategic Effects window says and what a triggered modifier's messages say, against
/// <c>docs/mechanics/strategiceffects.md</c> and <c>docs/mechanics/modifiers.md</c>, section 5.
/// </summary>
public class StrategicEffectsTests
{
	private static (int, int) Tally(CGameState state, string text) =>
		CConditionTally.Of(state, ScriptWorld.Condition(text), CEventScope.Start(ScriptWorld.Ger));

	private static CTriggeredModifier Entry(string name, string potential, string trigger, int icon = 0)
	{
		var entry = new CTriggeredModifier(name) { Icon = icon };
		entry.Potential.Children.AddRange(ScriptWorld.Condition(potential).Children);
		entry.Trigger.Children.AddRange(ScriptWorld.Condition(trigger).Children);
		entry.Modifier.Set("research_efficiency", CFixedPoint.Parse("0.05"));
		return entry;
	}

	[Fact]
	public void A_plain_condition_counts_one_and_one_of_counts_one_met_where_any_holds()
	{
		CGameState state = ScriptWorld.State();

		Assert.Equal((1, 2), Tally(state, "tag = GER tag = POL"));
		Assert.Equal((1, 1), Tally(state, "or = { tag = POL tag = GER }"));
		Assert.Equal((0, 1), Tally(state, "or = { }"));
		Assert.Equal((1, 3), Tally(state, "and = { tag = GER tag = POL } or = { tag = POL }"));
	}

	[Fact]
	public void None_of_counts_its_own_and_turns_the_met_round_and_with_nothing_in_it_counts_nothing()
	{
		CGameState state = ScriptWorld.State();

		Assert.Equal((1, 2), Tally(state, "not = { tag = POL tag = GER }"));
		Assert.Equal((2, 2), Tally(state, "not = { tag = POL tag = ITA }"));
		Assert.Equal((0, 0), Tally(state, "not = { }"));
	}

	[Fact]
	public void A_scope_counts_in_the_scope_it_names_and_a_block_over_a_set_in_the_one_it_was_given()
	{
		CGameState state = ScriptWorld.State();

		Assert.Equal((1, 1), Tally(state, "ITA = { tag = ITA }"));
		// Asked whether it holds, Germany's capital is its province 1; counted, it is asked in Germany's own scope, whose
		// province is none.
		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("any_owned_province = { province_id = 1 }").Children[0],
			CEventScope.Start(ScriptWorld.Ger)));
		Assert.Equal((0, 1), Tally(state, "any_owned_province = { province_id = 1 }"));
		// Province 6 has nobody controlling it: its controller names nothing, and the tally counts in nobody's scope.
		Assert.Equal((0, 1), Tally(state, "6 = { controller = { tag = GER } }"));
	}

	[Fact]
	public void The_rows_are_those_whose_potential_holds_in_file_order_with_whether_the_country_has_each()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CTriggeredModifier steel = Entry("steel", "tag = GER", "tag = GER tag = POL", icon: 12);
		CTriggeredModifier polish = Entry("polish", "tag = POL", "always = yes");
		CTriggeredModifier army = Entry("army", "", "always = yes");
		CTriggeredModifier none = Entry("none", "", "");
		state.TriggeredModifiers.AddRange(new[] { steel, polish, army, none });
		CTriggeredModifiers.Update(state, germany);

		List<CStrategicEffectRow> rows = CStrategicEffectsText.Rows(state, germany);

		Assert.Equal(new[] { steel, army, none }, rows.ConvertAll(row => row.Entry));
		Assert.Equal(new[] { false, true, true }, rows.ConvertAll(row => row.Present));
		Assert.Equal("1 / 2", CStrategicEffectsText.Status(rows[0]));
		Assert.Equal(50, CStrategicEffectsText.Progress(rows[0]));
		Assert.Equal(13, CStrategicEffectsText.Frame(steel));
		Assert.Equal("0 / 0", CStrategicEffectsText.Status(rows[2]));
		Assert.Equal(0, CStrategicEffectsText.Progress(rows[2]));
	}

	[Fact]
	public void A_rows_tooltip_is_its_trigger_written_out_for_the_country()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		var words = new CScriptWords(key => key, state, ScriptWorld.Ger);

		string text = CStrategicEffectsText.Tooltip(words, Entry("steel", "", "always = yes always = no"), germany);

		string[] lines = text.TrimEnd('\n').Split('\n');
		Assert.Equal(2, lines.Length);
		Assert.StartsWith("(§G*§W)", lines[0]);
		Assert.StartsWith("(§R*§W)", lines[1]);
	}

	[Fact]
	public void A_modifiers_messages_say_its_name_every_value_of_it_and_on_coming_its_description()
	{
		CGameState state = ScriptWorld.State();
		state.TriggeredModifiers.Add(Entry("steel", "", ""));
		var table = new Dictionary<string, string>
		{
			["TRIGMOD_1"] = "$NAME$",
			["TRIGMOD_6"] = "$EFFECT$",
			["TRIGMOD_LOG"] = "$EXPLANATION$",
			["steel"] = "Pact of Steel",
			["desc_steel"] = "Allied with Italy.",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CMessageTypes types = CMessageTypes.Read(ClausewitzParser.Parse("TRIGMOD = { } TRIGMODLOST = { }"), _ => { });
		var gained = new CPactMessage(CTriggeredModifiers.Gained, "", "", ScriptWorld.Ger, ScriptWorld.Ger)
		{
			Told = ScriptWorld.Ger,
			Messenger = CTriggeredModifiers.Messenger,
			Values = new[] { ("NAME", "steel"), ("EFFECT", "steel"), ("EXPLANATION", "steel") },
		};

		CMessage message = CMessages.Pact(Words, types, state, ScriptWorld.Ger, gained)!;

		Assert.Equal("Pact of Steel", CMessageText.Text(Words, message, "_1"));
		Assert.Contains("+5.0%", CMessageText.Text(Words, message, "_6"));
		Assert.Equal("Allied with Italy.", CMessageText.Text(Words, message, "_LOG"));
		Assert.Null(CMessages.Pact(Words, types, state, ScriptWorld.Pol, gained));
	}
}
