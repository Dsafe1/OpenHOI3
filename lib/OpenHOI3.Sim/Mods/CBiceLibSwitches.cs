using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Mods;

/// <summary>
/// What a mod's Lua has turned on of the rules BiceLib - BlackICE's DLL in the original - changes, for one game. The
/// Lua's <c>OpenHOI3.BiceLib</c> sets them: as the scripts load, before any game is made, into the scripts' own copy,
/// which each game made with those scripts takes as its own (<c>CGameStateLoader.BuildWorld</c>); and while a game is
/// played, by a command. So nothing here is static, a session's every game starts from what the Lua said as it loaded,
/// and a saved game loaded is given them again the same way - the original's save keeps none of them, and its DLL is
/// switched on again by the same script each session. Ours; <c>docs/mechanics/bicelib.md</c>.
/// </summary>
public sealed class CBiceLibSwitches
{
	/// <summary>A leader an order of battle puts on a land unit of his own country is given the rank it takes.</summary>
	public const string OobLeaderRanks = "Leaders.activateOobLeaderRanks";

	/// <summary>A leader whose history first gives him a rank after the start comes into service at that rank.</summary>
	public const string HistoryLeaderRanks = "Leaders.activateHistoryLeaderRanks";

	/// <summary>Every country's war exhaustion is put back to nought each month, and never paid back into neutrality.</summary>
	public const string WarExhaustionNeutralityReset = "BytePatches.disableWarExhaustionNeutralityReset";

	/// <summary>A brigade queued with a historical model gets a level of nought for each technology the model leaves out.</summary>
	public const string QueuedHistoricalModel = "ComplexPatches.fixQueuedHistoricalModel";

	/// <summary>The switches a Lua function of the same name turns on, with nothing else to it.</summary>
	public static IReadOnlyList<string> Simple { get; } = new[]
	{
		OobLeaderRanks, HistoryLeaderRanks, WarExhaustionNeutralityReset, QueuedHistoricalModel,
	};

	/// <summary>How many units a headquarters takes is the Lua's, per level and country, with leader traits.</summary>
	public const string UnitLimits = "Units.limits";

	/// <summary>What the game's rule gives every headquarters short of a theatre, where nothing else is set.</summary>
	public const int GameUnitLimit = 5;

	private readonly HashSet<string> _on = new(StringComparer.Ordinal);
	private readonly Dictionary<EUnitKind, int> _defaultLimits = new();
	private readonly Dictionary<(EUnitKind Level, string Tag), int> _countryLimits = new();
	private readonly Dictionary<string, int> _commandLimitTraits = new(StringComparer.Ordinal);

	/// <summary>
	/// Whether the day's call into the mod's Lua, <c>BiceLibDailyPeriodics</c>, has been made in this game - the first call
	/// after a new game or a load is told it is the first. Not a switch: the original's DLL keeps it for a session, and a
	/// loaded game starts without it, as here.
	/// </summary>
	public bool DailyCalled { get; set; }

	/// <summary>The switches turned on, by the Lua function's name under <c>BiceLib</c>.</summary>
	public IReadOnlyCollection<string> On => _on;

	public bool IsOn(string name) => _on.Contains(name);

	public void TurnOn(string name) => _on.Add(name);

	/// <summary>
	/// The most land units a headquarters of <paramref name="level"/> takes for <paramref name="owner"/>'s units: its own
	/// figure where one is set, else the default for the level, else the game's five.
	/// </summary>
	public int UnitLimit(EUnitKind level, CCountryTag owner)
	{
		if (_countryLimits.TryGetValue((level, owner.Value), out int own))
		{
			return own;
		}
		return _defaultLimits.TryGetValue(level, out int limit) ? limit : GameUnitLimit;
	}

	/// <summary>
	/// A level's limit: for every country without its own where <paramref name="tag"/> is null, else that country's - and
	/// a country's 0 goes back to the default, as the original's DLL takes it. Turns the limits on.
	/// </summary>
	public void SetUnitLimit(EUnitKind level, CCountryTag? tag, int limit)
	{
		_on.Add(UnitLimits);
		if (tag is null)
		{
			_defaultLimits[level] = limit;
		}
		else if (limit == 0)
		{
			_countryLimits.Remove((level, tag.Value.Value));
		}
		else
		{
			_countryLimits[(level, tag.Value.Value)] = limit;
		}
	}

	/// <summary>The country's own limit for a level, or 0 where it has none.</summary>
	public int CountryUnitLimit(EUnitKind level, CCountryTag tag) =>
		_countryLimits.TryGetValue((level, tag.Value), out int own) ? own : 0;

	/// <summary>What a leader with this trait adds to the limit of the headquarters he commands; the first set stands.</summary>
	public void AddCommandLimitTrait(string trait, int effect)
	{
		_on.Add(UnitLimits);
		_commandLimitTraits.TryAdd(trait, effect);
	}

	/// <summary>What a trait adds to a headquarters' limit, 0 for one that adds nothing.</summary>
	public int CommandLimitTrait(string trait) => _commandLimitTraits.TryGetValue(trait, out int effect) ? effect : 0;

	/// <summary>Whether any trait changes a limit.</summary>
	public bool HasCommandLimitTraits => _commandLimitTraits.Count > 0;

	/// <summary>A copy for a game to take as its own: everything set, and the day's call not yet made.</summary>
	public CBiceLibSwitches Copy()
	{
		var copy = new CBiceLibSwitches();
		copy._on.UnionWith(_on);
		foreach ((EUnitKind level, int limit) in _defaultLimits)
		{
			copy._defaultLimits[level] = limit;
		}
		foreach (((EUnitKind, string) key, int limit) in _countryLimits)
		{
			copy._countryLimits[key] = limit;
		}
		foreach ((string trait, int effect) in _commandLimitTraits)
		{
			copy._commandLimitTraits[trait] = effect;
		}
		return copy;
	}
}
