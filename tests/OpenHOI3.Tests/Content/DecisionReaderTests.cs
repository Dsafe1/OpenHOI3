using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading a decisions file, against <c>docs/mechanics/script.md</c>, sections 4 and 8.
/// </summary>
public class DecisionReaderTests
{
	private static List<CDecision> Read(string text, CScriptContext context)
	{
		var decisions = new List<CDecision>();
		CDecisionReader.ReadFile(ScriptFixture.Parse(text, context), decisions, context);
		return decisions;
	}

	[Fact]
	public void A_decision_reads_its_blocks_in_either_group()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CDecision> decisions = Read(@"
			diplomatic_decisions = {
				anschluss = {
					major = yes
					potential = { tag = GER }
					allow = { war = no }
					effect = { money = 5 }
					ai_will_do = { factor = 1 }
				}
			}
			political_decisions = { reform = { potential = { ai = yes } } }", context);

		Assert.Equal(2, decisions.Count);
		CDecision anschluss = decisions[0];
		Assert.Equal("anschluss", anschluss.Name);
		Assert.Equal(EDecisionGroup.Diplomatic, anschluss.Group);
		Assert.True(anschluss.Major);
		Assert.Single(anschluss.Potential.Children);
		Assert.Single(anschluss.Allow.Children);
		Assert.Single(anschluss.Effects);
		Assert.Equal(1, anschluss.AiWillDo.Days);
		Assert.Equal(EDecisionGroup.Political, decisions[1].Group);
		Assert.Equal(0, context.Problems.Count);
	}

	[Fact]
	public void An_ai_will_do_factor_is_days_and_loses_its_decimals()
	{
		CScriptContext context = ScriptFixture.Context();
		CDecision read = Assert.Single(Read("diplomatic_decisions = { d = { ai_will_do = { factor = 0.75 } } }", context));

		Assert.Equal(0, read.AiWillDo.Days);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.WrongShape));
	}

	[Fact]
	public void A_group_the_original_does_not_know_abandons_the_rest_of_the_file()
	{
		CScriptContext context = ScriptFixture.Context();
		List<CDecision> decisions = Read(@"
			diplomatic_decisions = { a = { } }
			country_decisions = { b = { } }
			diplomatic_decisions = { c = { } }", context);

		Assert.Equal(new[] { "a" }, decisions.ConvertAll(d => d.Name));
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}
}
