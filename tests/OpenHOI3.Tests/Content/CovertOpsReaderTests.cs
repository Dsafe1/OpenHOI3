using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading <c>common/covert_ops.txt</c>, against <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public class CovertOpsReaderTests
{
	[Fact]
	public void An_operation_reads_its_cost_its_two_conditions_and_its_effect_and_reports_any_other_key()
	{
		CScriptContext context = ScriptFixture.Context();
		var read = new List<CCovertOperation>();
		CCovertOpsReader.ReadFile(ScriptFixture.Parse(@"
			sabotage = {
				cost = 10
				is_valid = { }
				is_selectable = { war = yes }
				effect = { money = 5 }
				colour = red
			}", context), read, context);

		CCovertOperation sabotage = Assert.Single(read);
		Assert.Equal("sabotage", sabotage.Name);
		Assert.Equal(10, sabotage.Cost);
		Assert.Empty(sabotage.IsValid.Children);
		Assert.Single(sabotage.IsSelectable.Children);
		Assert.Single(sabotage.Effects);
		Assert.Single(ScriptFixture.Of(context, EScriptProblem.UnknownKey));
	}
}
