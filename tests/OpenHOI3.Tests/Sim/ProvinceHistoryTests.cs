using System.Linq;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>A province's history of changes made in play, against <c>docs/mechanics/savegame.md</c>, section 4.</summary>
public class ProvinceHistoryTests
{
	[Fact]
	public void A_change_of_hands_in_play_is_logged_at_its_hour_and_none_while_the_game_is_being_made()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		CProvince warsaw = state.Province(4);

		state.IsBeingMade = true;
		CProvinceHands.SetController(state, warsaw, ScriptWorld.Ger);
		Assert.Empty(warsaw.History);

		state.IsBeingMade = false;
		CProvinceHands.ChangeOwner(state, warsaw, ScriptWorld.Ita);
		// Its controller first, then its owner, at the same hour.
		Assert.Equal(new[] { CProvinceHistory.Controller, CProvinceHistory.Owner }, warsaw.History.Select(change => change.Key));
		Assert.All(warsaw.History, change => Assert.Equal(state.TotalHours, change.Hour));
		Assert.All(warsaw.History, change => Assert.Equal(ScriptWorld.Ita, change.Country));
		// An owner given again is no change.
		CProvinceHands.ChangeOwner(state, warsaw, ScriptWorld.Ita);
		Assert.Equal(2, warsaw.History.Count);
	}

	[Fact]
	public void A_core_is_logged_only_where_the_list_changes()
	{
		CGameState state = ScriptWorld.State();
		CProvince warsaw = state.Province(4);
		var scope = CEventScope.Start(ScriptWorld.Ger, 4);

		CEffects.Run(state, ScriptWorld.Effects("add_core = GER add_core = GER remove_core = ITA remove_core = GER"), scope);

		Assert.Equal(new[] { CProvinceHistory.AddCore, CProvinceHistory.RemoveCore }, warsaw.History.Select(change => change.Key));
	}

	[Fact]
	public void A_history_is_written_as_the_original_writes_it_a_dated_block_a_change()
	{
		// Berlin and 9987 as BlackICE's save of March 1936 and Their Finest Hour's of 7 January write them.
		const string history = "\thistory=\r\n\t{\r\n\t\t1936.1.3.0=\r\n\t\t{\r\n\t\t\tmanpower=4.800\r\n\t\t}\r\n" +
			"\t\t1936.1.7.19=\r\n\t\t{\r\n\t\t\tcontroller=\"ITA\"\r\n\t\t}\r\n\t}\r\n";
		CGameState state = ScriptWorld.State();
		CProvince warsaw = state.Province(4);
		Assert.True(CSaveValues.TryHours("1936.1.3.0", out long third));
		Assert.True(CSaveValues.TryHours("1936.1.7.19", out long seventh));
		warsaw.History.Add(new CProvinceChange(third, CProvinceHistory.Manpower, null, CFixedPoint.Parse("4.8")));
		warsaw.History.Add(new CProvinceChange(seventh, CProvinceHistory.Controller, ScriptWorld.Ita, CFixedPoint.Zero));

		Assert.Contains(history, CSaveWriter.Write(state));
	}
}
