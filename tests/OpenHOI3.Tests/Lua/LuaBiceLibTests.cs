using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Lua.Api;
using OpenHOI3.Lua.BiceLib;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Mods;
using OpenHOI3.Sim.Units;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// <c>OpenHOI3.BiceLib</c>, what BlackICE's DLL gives the original's Lua as this engine gives it, against
/// <c>docs/mechanics/bicelib.md</c>: every function there, the unbuilt reported once and answering nil, the switches the
/// scripts set as they load reaching each game, a limit in a game posted as a command, and the day's call back.
/// </summary>
public sealed class LuaBiceLibTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-bicelib-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaBiceLibTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string autoexec = "")
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), autoexec);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	[Fact]
	public void Every_function_BiceLib_registers_is_a_function_of_the_table()
	{
		using CAiScripts scripts = Load();
		CGameState state = ScriptWorld.State();
		foreach (CBiceLibFunction function in CBiceLibFunctions.All)
		{
			Assert.Equal("function", scripts.Ask(state, $"type(OpenHOI3.BiceLib.{function.Path})"));
		}
		Assert.Equal(58, CBiceLibFunctions.All.Count);
	}

	[Fact]
	public void A_function_not_built_does_nothing_answers_nil_and_is_reported_once_by_name()
	{
		using CAiScripts scripts = Load(
			"BiceLib = OpenHOI3.BiceLib\n" +
			"A = BiceLib.Tooltips.activateManpowerBreakdown()\n" +
			"BiceLib.Tooltips.activateManpowerBreakdown()\n" +
			"B = BiceLib.GameInfo.getCountryFlags('GER')\n");
		Assert.Null(scripts.Lua.GetGlobal("A"));
		Assert.Null(scripts.Lua.GetGlobal("B"));
		Assert.Equal(new[]
		{
			CGameApi.UnbuiltPrefix + "BiceLib.Tooltips.activateManpowerBreakdown",
			CGameApi.UnbuiltPrefix + "BiceLib.GameInfo.getCountryFlags",
		}, _reports);
	}

	[Fact]
	public void What_has_nothing_to_do_here_or_is_so_already_says_nothing()
	{
		using CAiScripts scripts = Load(
			"BiceLib = OpenHOI3.BiceLib\n" +
			"BiceLib.setModuleBase() BiceLib.startConsole() BiceLib.Overlay.enable()\n" +
			"R = BiceLib.Commands.registerBiceCommands()\n" +
			"P = BiceLib.Commands.postExampleCommand({ 1, 2 })\n" +
			"BiceLib.EffectTexts.activateTriggerIndent() BiceLib.BytePatches.fixMinisterTechDecay()\n" +
			"BiceLib.Events.rebuildCandidates()\n" +
			"M = BiceLib.ComplexPatches.fixMaxInfrastructure()\n");
		Assert.Equal(true, scripts.Lua.GetGlobal("R"));
		Assert.Equal(false, scripts.Lua.GetGlobal("P"));
		Assert.Equal(true, scripts.Lua.GetGlobal("M"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void What_the_scripts_turn_on_as_they_load_is_their_own_and_each_game_takes_a_copy()
	{
		using CAiScripts scripts = Load(
			"local B = OpenHOI3.BiceLib\n" +
			"OK = B.Leaders.activateOobLeaderRanks()\n" +
			"B.Leaders.activateHistoryLeaderRanks()\n" +
			"B.Units.setCorpsUnitLimit(4, '---')\n" +
			"B.Units.setArmyUnitLimit(7, 'GER')\n" +
			"B.Units.addCommandLimitTrait('organiser', 1)\n");
		CBiceLibSwitches switches = scripts.BiceLibSwitches;
		Assert.Equal(true, scripts.Lua.GetGlobal("OK"));
		Assert.True(switches.IsOn(CBiceLibSwitches.OobLeaderRanks));
		Assert.True(switches.IsOn(CBiceLibSwitches.HistoryLeaderRanks));
		Assert.True(switches.IsOn(CBiceLibSwitches.UnitLimits));
		Assert.Equal(4, switches.UnitLimit(EUnitKind.Corps, ScriptWorld.Ita));
		Assert.Equal(7, switches.UnitLimit(EUnitKind.Army, ScriptWorld.Ger));
		Assert.Equal(CBiceLibSwitches.GameUnitLimit, switches.UnitLimit(EUnitKind.Army, ScriptWorld.Ita));
		Assert.Equal(1, switches.CommandLimitTrait("organiser"));

		CBiceLibSwitches copy = switches.Copy();
		copy.SetUnitLimit(EUnitKind.Corps, null, 9);
		Assert.Equal(4, switches.UnitLimit(EUnitKind.Corps, ScriptWorld.Ita));
		Assert.Equal(new[] { CBiceLibSwitches.OobLeaderRanks, CBiceLibSwitches.HistoryLeaderRanks, CBiceLibSwitches.UnitLimits }
			.OrderBy(s => s), copy.On.OrderBy(s => s));
	}

	[Fact]
	public void In_a_game_a_limit_is_a_command_and_one_the_game_holds_is_not_posted_again()
	{
		using CAiScripts scripts = Load();
		CGameState state = ScriptWorld.State();

		scripts.Ask(state, "OpenHOI3.BiceLib.Units.setCorpsUnitLimit(6, 'GER')");
		Assert.False(state.BiceLib.IsOn(CBiceLibSwitches.UnitLimits));
		CCommand command = Assert.Single(state.Outbox);
		Assert.IsType<CSetUnitLimitCommand>(command);
		command.Execute(state);
		state.Outbox.Clear();
		Assert.Equal(6, state.BiceLib.UnitLimit(EUnitKind.Corps, ScriptWorld.Ger));

		scripts.Ask(state, "OpenHOI3.BiceLib.Units.setCorpsUnitLimit(6, 'GER')");
		scripts.Ask(state, "OpenHOI3.BiceLib.Units.setCorpsUnitLimit(6, 'XXX')");
		Assert.Empty(state.Outbox);

		scripts.Ask(state, "OpenHOI3.BiceLib.Leaders.activateHistoryLeaderRanks()");
		Assert.IsType<CBiceLibSwitchCommand>(Assert.Single(state.Outbox));
	}

	[Fact]
	public void The_days_call_back_is_made_at_one_oclock_first_with_one_and_then_with_nought()
	{
		using CAiScripts scripts = Load(
			"CALLS = ''\n" +
			"function BiceLibDailyPeriodics(firstDay) CALLS = CALLS .. firstDay return { bicelib_loaded = true } end\n");
		CGameState state = ScriptWorld.State();
		state.AiScripts = scripts;

		for (int hour = 0; hour < 50; hour++)
		{
			state.AdvanceOneHour();
			CBiceLibDaily.RunHourly(state);
		}

		Assert.Equal("100", scripts.Lua.GetGlobal("CALLS"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void Without_the_function_the_days_call_does_nothing()
	{
		using CAiScripts scripts = Load();
		CGameState state = ScriptWorld.State();
		state.AiScripts = scripts;
		state.AdvanceOneHour();

		CBiceLibDaily.RunHourly(state);

		Assert.Empty(_reports);
		Assert.Empty(state.Outbox);
	}
}
