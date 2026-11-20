using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Console;

/// <summary>
/// Runs a console line: the first word found in the table, exactly and with its case, by its name or an alias, the rest handed
/// to the command as the original hands them - counted and not typed, a number that is none reading as nought. Each answer is the
/// original's. See <c>docs/mechanics/console.md</c>.
/// </summary>
public sealed class CConsoleRunner
{
	private const string CountDeclined = "Invalid arguments count.";
	private const string Amount = "Please specify the amount.";
	private const string DateFormat = "Please specify the date in format yyyy.mm.dd.hh";
	private const string RenderRule = "----------------------------------\n";

	/// <summary>The map's layers as <c>debug render</c> names them, each with its bit.</summary>
	private static readonly (string Name, int Bit)[] RenderLayers =
	{
		("Trees", 0x1), ("Borders", 0x2), ("Text", 0x8), ("Overlay", 0x10), ("Objects", 0x20), ("MapObjects", 0x40), ("Rivers", 0x80),
		("Water", 0x100), ("WaterBorders", 0x200), ("Effects", 0x400),
	};

	private readonly IConsoleHost _host;

	public CConsoleRunner(IConsoleHost host)
	{
		_host = host;
	}

	private CGameState State => _host.State;

	public CConsoleReply Run(string line)
	{
		string[] tokens = line.Trim().Split(' ');
		if (tokens[0].Length == 0)
		{
			return new CConsoleReply(true, "");
		}
		CConsoleCommandDef? row = Find(tokens[0]);
		if (row is null)
		{
			return Fail("Unknown command");
		}
		if (!row.InRelease)
		{
			return Fail("Command available only for developers.");
		}
		_refused = false;
		CConsoleReply reply = Execute(row.Name, tokens.Skip(1).ToArray());
		// Ours: a command that would change the game says so where the game takes none from here - a replay, and a game
		// between several machines (docs/multiplayer.md, section 6) - where it would otherwise say it had been done.
		return _refused ? Fail("Not available in this game.") : reply;
	}

	/// <summary>Whether the command being run tried to change a game that takes no command from the console.</summary>
	private bool _refused;

	/// <summary>A command that changes the game, posted - or refused, where the game takes none from here.</summary>
	private void Post(OpenHOI3.Sim.Commands.CCommand command)
	{
		if (!_host.TakesCommands)
		{
			_refused = true;
			return;
		}
		_host.Post(command);
	}

	/// <summary>A row by its name or an alias, exactly.</summary>
	public static CConsoleCommandDef? Find(string word)
	{
		return CConsoleTable.Commands.FirstOrDefault(row => row.Name == word || row.Aliases.Contains(word));
	}

	/// <summary>Every command <c>help</c> lists, a line each: the developers' are left out.</summary>
	public static string ListAll()
	{
		return string.Join("\n", CConsoleTable.Commands.Where(row => row.InRelease).Select(row => row.Format()));
	}

	/// <summary>A number as C's <c>atoi</c> reads one: its leading digits, and nought for none.</summary>
	public static int Atoi(string text)
	{
		int at = 0;
		while (at < text.Length && char.IsWhiteSpace(text[at]))
		{
			at++;
		}
		int start = at;
		if (at < text.Length && text[at] is '-' or '+')
		{
			at++;
		}
		while (at < text.Length && char.IsAsciiDigit(text[at]))
		{
			at++;
		}
		return int.TryParse(text[start..at], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ? value : 0;
	}

	private static CConsoleReply Ok(string text) => new(true, text);

	private static CConsoleReply Fail(string text) => new(false, text);

	private CCountry? Tag(string word) => CCountryTag.TryParse(word.ToUpperInvariant(), out CCountryTag tag) ? State.Country(tag) : null;

	private CProvince? Province(string word)
	{
		int id = Atoi(word);
		return id > 0 && id < State.Provinces.Count ? State.Province(id) : null;
	}

	private void Cheat(ECheat cheat, CCountryTag? country = null, CCountryTag? other = null, long amount = 0, EGoods good = EGoods.Money,
		int province = 0, string text = "")
	{
		Post(new CCheatCommand(cheat, country, other, amount, good, province, text));
	}

	private CConsoleReply Execute(string name, string[] args)
	{
		CCountry? player = _host.Player;
		switch (name)
		{
			case "money": return Goods(args, EGoods.Money, "money");
			case "supplies": return Goods(args, EGoods.Supplies, "supplies");
			case "fuel": return Goods(args, EGoods.Fuel, "fuel");
			case "crudeoil": return Goods(args, EGoods.CrudeOil, "crude oil");
			case "metal": return Goods(args, EGoods.Metal, "metal");
			case "energy": return Goods(args, EGoods.Energy, "energy");
			case "rarematerials": return Goods(args, EGoods.RareMaterials, "rare materials");
			case "givemeall":
				if (args.Length == 0)
				{
					return Fail(Amount);
				}
				Cheat(ECheat.AddEveryGood, player?.Tag, amount: Thousands(args[0]));
				return Ok("Added " + Atoi(args[0]).ToString(CultureInfo.InvariantCulture) + " of everything.");
			case "addmanpower": return Added(args, ECheat.AddManpower, "manpower");
			case "nukes": return Added(args, ECheat.AddNukes, "nukes");
			case "addofficers": return Added(args, ECheat.AddOfficers, "officers");
			case "noneutrality":
				if (args.Length != 0)
				{
					return Fail(CountDeclined);
				}
				Cheat(ECheat.NoNeutrality, player?.Tag);
				return Ok("No longer neutral");
			case "election":
				if (args.Length != 0)
				{
					return Fail(CountDeclined);
				}
				Cheat(ECheat.Election, player?.Tag);
				return Ok("Election started");
			case "setnationalunity": return NationalUnity(args);
			case "reorg": return Reorganise(args);
			case "surrender": return Surrender(args);
			case "guarantee": return Guarantee(args);
			case "conquerall": return ConquerAll(args);
			case "changeowner": return ChangeHands(args, ECheat.ChangeOwner, " now belongs to ");
			case "changecontroller": return ChangeHands(args, ECheat.ChangeController, " is now controlled by ");
			case "event": return Event(args);
			case "revolt": return Revolt(args);
			case "instantresearch":
				return Toggle(args, ECheat.ToggleInstantResearch, State.InstantResearch, "Instant research ON", "Instant research OFF", "Invalid arguments.");
			case "instantconstruction":
				return Toggle(args, ECheat.ToggleInstantConstruction, State.InstantConstruction, "Instant construction ON", "Instant construction OFF", "Invalid arguments.");
			case "alwayspartisants":
				return Toggle(args, ECheat.ToggleAlwaysPartisans, State.IgnorePartisanSpawnLimit, "Always underground spawn ON", "Always underground spawn OFF", "Invalid arguments.");
			case "tooltipdebug":
				if (args.Length != 0)
				{
					return Fail(CountDeclined);
				}
				_host.TooltipDebug = !_host.TooltipDebug;
				return Ok(_host.TooltipDebug ? "Tooltip debug ENABLED" : "Tooltip debug DISABLED");
			case "cpaint":
				if (args.Length != 0)
				{
					return Fail("Invalid arguments count");
				}
				_host.ContinentPainting = !_host.ContinentPainting;
				return Ok(_host.ContinentPainting ? "Now painting continent. Shift-click to painting the map." : "Painting disabled");
			case "tag": return PlayAs(args);
			case "aipause": return AiPause(args);
			case "teleportselectionto": return Teleport(args);
			case "date": return Date(args);
			case "detailedlog":
				if (args.Length == 0)
				{
					return Fail("Please specify file path and name.");
				}
				_host.WriteDetailedLog(args[0]);
				return Ok("See logs/" + args[0] + ".log for result.");
			case "reloadinterface":
				if (args.Length != 0)
				{
					return Fail("Invalid arguments count");
				}
				_host.ReloadInterface();
				return Ok("Interface reloaded.");
			case "nextsong":
				// The same pick as a track's end, played at once; the original answers with the path, nothing where none
				// could be picked (docs/mechanics/music.md, section 2.4).
				if (args.Length != 0)
				{
					return Fail("Invalid arguments count.");
				}
				return Ok("Now playing " + _host.NextSong());
			case "reloadfx": return ReloadFx(args);
			case "fullscreen":
				if (args.Length != 0)
				{
					return Fail("Invalid arguments.");
				}
				_host.Fullscreen = !_host.Fullscreen;
				return Ok(_host.Fullscreen ? "Fullscreen mode ON." : "Fullscreen mode OFF.");
			case "printtech": return PrintTech(args);
			case "copyprov": return CopyProvinces(args);
			case "debug": return Debug(args);
			case "help": return Help(args);
			case "helplog":
				if (args.Length != 0)
				{
					return Fail(CountDeclined);
				}
				_host.Log("== HELP LOG ==\n" + ListAll());
				return Ok("See game.log for full help details.");
			default:
				return Fail("Not built.");
		}
	}

	private static long Thousands(string word) => Atoi(word) * (long)CFixedPoint.Scale;

	private CConsoleReply Goods(string[] args, EGoods good, string words)
	{
		if (args.Length == 0)
		{
			return Fail(Amount);
		}
		Cheat(ECheat.AddGood, _host.Player?.Tag, amount: Thousands(args[0]), good: good);
		return Ok("Added " + Atoi(args[0]).ToString(CultureInfo.InvariantCulture) + " " + words + ".");
	}

	private CConsoleReply Added(string[] args, ECheat cheat, string words)
	{
		if (args.Length == 0)
		{
			return Fail(Amount);
		}
		Cheat(cheat, _host.Player?.Tag, amount: Thousands(args[0]));
		return Ok("Added " + Atoi(args[0]).ToString(CultureInfo.InvariantCulture) + " " + words + ".");
	}

	private CConsoleReply Toggle(string[] args, ECheat cheat, bool on, string turnedOn, string turnedOff, string refused)
	{
		if (args.Length != 0)
		{
			return Fail(refused);
		}
		Cheat(cheat);
		return Ok(on ? turnedOff : turnedOn);
	}

	private CConsoleReply NationalUnity(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify the country tag and new national unity.");
		}
		if (args.Length == 1)
		{
			return Fail("Please specify the new national unity.");
		}
		if (Tag(args[0]) is not { } country)
		{
			return Fail("Invalid country tag.");
		}
		Cheat(ECheat.SetNationalUnity, country.Tag, amount: Thousands(args[1]));
		return Ok("National Unity of " + country.Name + " changed to " + Atoi(args[1]).ToString(CultureInfo.InvariantCulture));
	}

	private CConsoleReply Reorganise(string[] args)
	{
		if (args.Length == 0 || Province(args[0]) is not { } province || _host.Player is not { } player)
		{
			return Fail("Please specify the province id.");
		}
		int count = player.AllUnits().Where(unit => unit.Location == province.Id).Sum(unit => unit.SubUnits.Count);
		if (count == 0)
		{
			return Fail("No friendly subunits found in province");
		}
		Cheat(ECheat.Reorganise, player.Tag, province: province.Id);
		return Ok(count.ToString(CultureInfo.InvariantCulture) + " friendly subunits reorganized.");
	}

	private CConsoleReply Surrender(string[] args)
	{
		if (args.Length < 2)
		{
			return Fail("Please specify 2 country tags.");
		}
		if (Tag(args[0]) is not { } loser)
		{
			return Fail("Unknown country " + args[0]);
		}
		if (Tag(args[1]) is not { } winner)
		{
			return Fail("Unknown country " + args[1]);
		}
		Cheat(ECheat.Surrender, loser.Tag, winner.Tag);
		return Ok("Country " + winner.Name + " annexed " + loser.Name);
	}

	private CConsoleReply Guarantee(string[] args)
	{
		if (args.Length < 2)
		{
			return Fail("Please specify 2 country tags.");
		}
		if (Tag(args[0]) is not { } guarantor || Tag(args[1]) is not { } recipient)
		{
			return Fail("Unknown country tag");
		}
		bool had = State.Guarantees.Exists(pact => pact.Guarantor == guarantor.Tag && pact.Guaranteed == recipient.Tag);
		Cheat(ECheat.ToggleGuarantee, guarantor.Tag, recipient.Tag);
		return Ok(guarantor.Name + (had ? " is no longer guaranteeing " : " is now guaranteeing ") + recipient.Name);
	}

	private CConsoleReply ConquerAll(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify enemy country tag.");
		}
		if (Tag(args[0]) is not { } enemy)
		{
			return Fail("Unknown country tag");
		}
		int count = State.Provinces.Count(province => province.Owner == enemy.Tag && province.Controller == enemy.Tag);
		Cheat(ECheat.ConquerAll, enemy.Tag, _host.Player?.Tag);
		return Ok(count.ToString(CultureInfo.InvariantCulture) + " more provinces are under our control");
	}

	private CConsoleReply ChangeHands(string[] args, ECheat cheat, string words)
	{
		if (args.Length < 2)
		{
			return Fail("Please specify country tag and province id");
		}
		if (Tag(args[0]) is not { } country)
		{
			return Fail("Unknown country.");
		}
		if (Province(args[1]) is not { } province)
		{
			return Fail("Invalid province specified");
		}
		Cheat(cheat, country.Tag, province: province.Id);
		return Ok(province.Name + words + country.Name);
	}

	private CConsoleReply Event(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify event id");
		}
		int id = Atoi(args[0]);
		CCountryTag? whose = _host.Player?.Tag;
		int province = 0;
		if (args.Length > 1)
		{
			if (Atoi(args[1]) is > 0 and int number)
			{
				province = number;
			}
			else if (Tag(args[1]) is { } country)
			{
				whose = country.Tag;
			}
			else
			{
				return Fail("Target tag is invalid.");
			}
		}
		if (whose is not { } scope)
		{
			return Fail("Target tag is invalid.");
		}
		var command = new CFireEventCommand(id, scope, province);
		if (!command.MayExecute(State))
		{
			return Fail("There is no event with ID #" + id.ToString(CultureInfo.InvariantCulture));
		}
		Post(command);
		return Ok("Executed #" + id.ToString(CultureInfo.InvariantCulture));
	}

	private CConsoleReply Revolt(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify province id and rebel type.");
		}
		string type = "";
		string where = args[0];
		if (args.Length > 1)
		{
			if (!State.RebelTypes.Any(rebel => rebel.Name == args[0]))
			{
				return Fail("Unknown rebel type");
			}
			type = args[0];
			where = args[1];
		}
		if (Province(where) is not { } province)
		{
			return Fail("Please specify province id and rebel type.");
		}
		Cheat(ECheat.Revolt, province: province.Id, text: type);
		return Ok("Revolt created.");
	}

	private CConsoleReply PlayAs(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify the country tag");
		}
		if (args[0].Length == 0)
		{
			return Fail("empty tag. check your spaces");
		}
		if (Tag(args[0]) is not { } country || CCountryLists.OwnedCount(State, country.Tag) == 0)
		{
			return Fail("Such country does not exist.");
		}
		if (_host.Player is not { } player || !_host.TakesCommands)
		{
			return Fail("Can't change the country right now.");
		}
		Cheat(ECheat.SetPlayed, country.Tag, player.Tag);
		_host.ShowCountryPlayed();
		return Ok("Now Playing - " + country.Name);
	}

	private CConsoleReply AiPause(string[] args)
	{
		if (args.Length == 0)
		{
			return Ok(PausedList());
		}
		if (args.Length != 1)
		{
			return Fail("Please specify country tag, or \"all\" argument");
		}
		if (args[0] == "all")
		{
			bool anyPaused = State.Countries.Any(country => country.AiPaused);
			Cheat(ECheat.SetEveryAiPause, amount: anyPaused ? 0 : 1);
			return Ok(anyPaused ? "AI Unpaused for all" : "AI Paused for all");
		}
		if (Tag(args[0]) is not { } chosen)
		{
			return Fail("Invalid country tag");
		}
		Cheat(ECheat.ToggleAiPause, chosen.Tag);
		return Ok(chosen.Name + (chosen.AiPaused ? " AI is now Paused" : " AI is now Unpaused"));
	}

	/// <summary>The countries whose AI is paused, six to a line.</summary>
	private string PausedList()
	{
		var text = new StringBuilder("AI is paused for:\n");
		int count = 0;
		foreach (CCountry country in State.Countries.Where(country => country.AiPaused))
		{
			text.Append(country.Tag.Value).Append(", ");
			if (++count % 6 == 0)
			{
				text.Append('\n');
			}
		}
		return count == 0 ? text.Append("None").ToString() : text.ToString();
	}

	private CConsoleReply Teleport(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify the province id.");
		}
		if (_host.SelectedUnit is not { } unit || (CMovementRules.IsLandUnit(unit) && unit.Transport is not null))
		{
			return Fail("Select transportable unit first!");
		}
		// A number that is no province leaves the answer empty.
		if (Province(args[0]) is not { } province)
		{
			return Ok("");
		}
		Cheat(ECheat.Teleport, unit.Owner, amount: unit.Id, province: province.Id);
		return Ok("Unit moved.");
	}

	private CConsoleReply Date(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail(DateFormat);
		}
		string[] parts = args[0].Split('.');
		int hour = parts.Length > 3 ? Atoi(parts[3]) : 0;
		// Ours: the original takes anything and walks the date backwards off it.
		if (parts.Length < 3 || hour is < 0 or >= CGameState.HoursPerDay
			|| !CDate.TryParse(Atoi(parts[0]) + "." + Atoi(parts[1]) + "." + Atoi(parts[2]), out CDate date))
		{
			return Fail(DateFormat);
		}
		Cheat(ECheat.SetDate, amount: (long)date.DayNumber * CGameState.HoursPerDay + hour);
		return Ok("Date changed.");
	}

	private CConsoleReply ReloadFx(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify effect name");
		}
		string? reply = args[0] switch
		{
			"map" => "Map reloaded",
			"front" => "Front reloaded",
			"hierarchy" => "LineHierarchy reloaded",
			"masked3dFlag" => "MaskedFlag reloaded",
			_ => _host.EffectExists(args[0]) ? args[0] + " reloaded" : null,
		};
		if (reply is null)
		{
			return Fail("Reload failed");
		}
		_host.ReloadInterface();
		return Ok(reply);
	}

	private CConsoleReply PrintTech(string[] args)
	{
		if (_host.Player is { } player)
		{
			var text = new StringBuilder("===== ").Append(player.Tag.Value).Append(" Technology Levels =====");
			foreach (string name in State.Technologies.Keys)
			{
				int level = player.TechnologyLevels.GetValueOrDefault(name);
				if (level != 0 || args.Length == 0)
				{
					text.Append('\n').Append(name).Append(" = ").Append(level.ToString(CultureInfo.InvariantCulture));
				}
			}
			_host.Log(text.ToString());
		}
		return Ok("See game.log for result.");
	}

	private CConsoleReply CopyProvinces(string[] args)
	{
		if (args.Length != 0)
		{
			return Fail("Invalid arguments");
		}
		IReadOnlyList<int> provinces = _host.SelectedProvinces;
		if (provinces.Count == 0)
		{
			return Fail("Select some provinces first.");
		}
		_host.CopyToClipboard(string.Join(" ", provinces.Select(id => id.ToString(CultureInfo.InvariantCulture))));
		return Ok(provinces.Count.ToString(CultureInfo.InvariantCulture) + " provinces copied to clipboard.");
	}

	private CConsoleReply Debug(string[] args)
	{
		if (args.Length == 0)
		{
			return Fail("Please specify some arguments.");
		}
		switch (args[0])
		{
			case "tooltips":
				_host.TooltipsOn = !_host.TooltipsOn;
				return Ok(_host.TooltipsOn ? "Tooltips are now turned ON." : "Tooltips are now turned OFF.");
			case "fow":
				bool on = !State.FogOfWar;
				Post(new CFogOfWarCommand(on));
				return Ok(on ? "Fog of war turned ON" : "Fog of war turned OFF");
			case "nopopup":
				Cheat(ECheat.ToggleNoPopups);
				return Ok(State.NoPopups ? "Blocking all popups" : "Stopped blocking all popups");
			case "minzoom":
				if (args.Length < 2)
				{
					return Fail("Please specify zoomlevel value.");
				}
				if (Atoi(args[1]) > 0)
				{
					_host.SetLeastHeight(Atoi(args[1]));
				}
				return Ok("Zoom limit set to " + Atoi(args[1]).ToString("F3", CultureInfo.InvariantCulture));
			case "spy":
				Cheat(ECheat.ToggleSpy);
				return Ok(State.SpyCheat ? "Superior espionage turned ON." : "Superior espionage turned OFF.");
			case "yesmen":
				Cheat(ECheat.ToggleYesmen);
				return Ok(State.AiYesmen ? "AI now always responds favorably" : "AI now uses normal responses.");
			case "wireframe":
				_host.Wireframe = !_host.Wireframe;
				return Ok(_host.Wireframe ? "Wireframe ON" : "Wireframe OFF");
			case "render": return Render(args);
			case "color": return Colour(args);
			case "info" or "profile" or "assert" or "log_profile" or "textures" or "save":
				return Fail("Not built.");
			default:
				return Ok("");
		}
	}

	/// <summary>Alone it lists what is drawn, the map mode's layers and the console's together; with a layer's name it turns that one over.</summary>
	private CConsoleReply Render(string[] args)
	{
		if (args.Length == 1)
		{
			int shown = _host.ModeLayers & _host.RenderLayers;
			var text = new StringBuilder(RenderRule).Append("Render options:\n");
			foreach ((string name, int bit) in RenderLayers)
			{
				text.Append("\t\t").Append(name).Append(":\t").Append((shown & bit) != 0 ? "yes\n" : "no\n");
			}
			return Ok(text.Append(RenderRule).ToString());
		}
		foreach ((string name, int bit) in RenderLayers)
		{
			if (name.ToUpperInvariant() == args[1].ToUpperInvariant())
			{
				_host.RenderLayers ^= bit;
			}
		}
		return Ok("");
	}

	private CConsoleReply Colour(string[] args)
	{
		if (args.Length != 5)
		{
			return Fail("4 more arguments needed: country tag, red, green, blue");
		}
		if (Tag(args[1]) is not { } country)
		{
			return Fail("Invalid country Tag");
		}
		long colour = Channel(args[2]) << 16 | Channel(args[3]) << 8 | Channel(args[4]);
		Cheat(ECheat.SetColour, country.Tag, amount: colour);
		_host.RefreshMapColours();
		return Ok("Color applied to " + country.Name);
	}

	private static long Channel(string word) => Math.Clamp(Atoi(word), 0, 255);

	private static CConsoleReply Help(string[] args)
	{
		if (args.Length > 1)
		{
			return Fail(CountDeclined);
		}
		if (args.Length == 0)
		{
			return Ok(ListAll());
		}
		// A row of the developers' is described like any other.
		return Find(args[0]) is { } row ? Ok(row.Format() + "\n - " + row.Help) : Fail("Unknown command");
	}
}
