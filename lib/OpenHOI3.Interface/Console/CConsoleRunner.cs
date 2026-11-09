using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
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
		return Execute(row.Name, tokens.Skip(1).ToArray());
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
		_host.Post(new CCheatCommand(cheat, country, other, amount, good, province, text));
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
		_host.Post(command);
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
				_host.Post(new CFogOfWarCommand(on));
				return Ok(on ? "Fog of war turned ON" : "Fog of war turned OFF");
			case "nopopup":
				Cheat(ECheat.ToggleNoPopups);
				return Ok(State.NoPopups ? "Blocking all popups" : "Stopped blocking all popups");
			case "minzoom":
				if (args.Length > 1 && Atoi(args[1]) is > 0 and int height)
				{
					_host.SetLeastHeight(height);
					return Ok("Zoom limit set to " + height.ToString(CultureInfo.InvariantCulture));
				}
				return Fail("Please specify some arguments.");
			case "info" or "profile" or "assert" or "log_profile" or "textures" or "spy" or "yesmen" or "wireframe" or "render" or "color" or "save":
				return Fail("Not built.");
			default:
				return Ok("");
		}
	}

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
