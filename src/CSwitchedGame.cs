using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3;

/// <summary>
/// A game a developer switch says to play - <c>--country=</c>, <c>--load=</c> or <c>--replay=</c> - made without the front
/// end, with the recording <c>--replay=</c> plays or <c>--record=</c> writes. Made on the loading screen's thread: nothing in
/// it touches the engine (<c>docs/mechanics/loadingscreen.md</c>).
/// </summary>
public sealed record CSwitchedGame(CGameState State, CMap Map, CLocalisation Localisation, CAiScripts AiScripts,
	CReplayFile? ReplayFile)
{
	/// <summary>The country played where a developer switch starts a game and none says which.</summary>
	public const string DefaultPlayer = "GER";

	/// <summary>The game the switches say, made from the install with the localisation read already.</summary>
	public static CSwitchedGame Make(CDirectorySettings directories, CLocalisation localisation)
	{
		var clock = Stopwatch.StartNew();
		CCountries countries = CCountries.Load(directories, localisation);
		CMap map = CMap.Load(directories);
		// The install's Lua lives as long as the session: a new game asks it for its relations.
		var aiScripts = new CAiScripts(directories);
		// Who plays is known before the game is made: the AI's first look at the laws leaves that country alone. A game
		// replaying a recording is played by whoever played it when it was recorded, which its header says
		// (docs/replay.md, sections 6 and 8).
		string? replayFrom = CSession.Argument("--replay=");
		string? recordTo = CSession.Argument("--record=");
		string asked = CSession.PlayerArgument() ?? DefaultPlayer;
		IReadOnlyList<CCountryTag> played;
		if (replayFrom is not null)
		{
			played = CReplayFile.ReadHeader(replayFrom).Played;
			if (CSession.PlayerArgument() is not null)
			{
				GD.Print($"--replay: the recording says who plays, so --country={asked} is left aside.");
			}
		}
		else
		{
			if (countries.Find(asked) is null)
			{
				GD.Print($"--country: there is no country '{asked}', so {DefaultPlayer} is played.");
				asked = DefaultPlayer;
			}
			played = new[] { new CCountryTag(asked) };
		}
		CGameState state;
		// --load= a save of the original's: the game it holds, as far as the loader reads it (docs/mechanics/savegame.md),
		// played by --country= or else by the country the save was played by.
		if (CSession.Argument("--load=") is { } loadFrom)
		{
			CCountryTag? loadAs = CSession.PlayerArgument() is { } tag ? new CCountryTag(tag) : null;
			(state, Content.Saves.CSaveReport _) = Content.Saves.CSaveLoader.Load(directories, map, localisation, countries,
				loadFrom, loadAs, aiScripts);
			played = state.Played.ToList();
			GD.Print($"--load: {loadFrom}, {string.Join(", ", played)} played.");
		}
		else
		{
			state = CGameStateLoader.LoadStart(directories, map, localisation, countries, aiScripts: aiScripts, played: played);
		}
		GD.Print($"Loaded {map.ProvinceCount - 1} provinces and {countries.Count} countries " +
			$"for {state.Date} in {clock.ElapsedMilliseconds} ms.");

		// The recording takes the AI's place, and the players', from the first hour, entering the game having run as it did
		// when recorded; or the game is recorded from then. A replay takes no order of the player's: its screens look on.
		CReplayFile? replayFile = null;
		if (replayFrom is not null)
		{
			replayFile = CReplayFile.Open(replayFrom, state);
			state.Replay = CReplay.Replaying(replayFile);
			string player = played.Count > 0 ? played[0].Value : "";
			GD.Print($"Replaying {replayFrom}, " + (played.Count == 0 ? "nobody playing." : $"{player} looked on and given no orders."));
		}
		else if (recordTo is not null)
		{
			replayFile = CReplayFile.Create(recordTo, new CReplayHeader(played, state.Seed));
			state.Replay = CReplay.Recording(replayFile.Write);
			GD.Print($"Recording to {recordTo}, {(played.Count > 0 ? played[0].Value : "")} playing.");
		}
		return new CSwitchedGame(state, map, localisation, aiScripts, replayFile);
	}
}
