using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

/// <summary>
/// A look at how many units a command may hold on a new game (<c>docs/mechanics/attaching.md</c>, section 8): whether
/// the game is an enhanced one, the four defines, and for a country - Germany with none given - each level's limit
/// with what its modifier and its technology give it, the triggered modifiers it has that give any, and how many of
/// its commands hold more than they may. Then the same for every country whose limits are not the defines'.
///
///   OpenHOI3.SaveCheck commandlimits &lt;install folder&gt; [tag] [tech=NAME:LEVEL ...] [mod=NAME]
///
/// <c>tech=</c> gives the country that level of a technology first, and its day's modifiers are made again.
/// </summary>
internal static class CCommandLimitLook
{
	private static readonly (EUnitKind Kind, string Name, string Modifier, string Define)[] Levels =
	{
		(EUnitKind.Corps, "corps", CCommandLimit.CorpsKind, CCommandLimit.CorpsDefine),
		(EUnitKind.Army, "army", CCommandLimit.ArmyKind, CCommandLimit.ArmyDefine),
		(EUnitKind.ArmyGroup, "army group", CCommandLimit.ArmyGroupKind, CCommandLimit.ArmyGroupDefine),
	};

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck commandlimits <install folder> [tag] [tech=NAME:LEVEL ...]");
			return 2;
		}
		string[] rest = args.Skip(2).ToArray();
		CCountryTag tag = new(rest.FirstOrDefault(a => a.Length == 3 && !a.Contains('=')) ?? "GER");

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		if (state.Country(tag) is not { } country)
		{
			Console.Error.WriteLine("no such country");
			return 2;
		}

		Console.WriteLine($"rules: {(state.Rules.Enhanced ? state.Rules.Text : "the faithful game's - five at every level")}");
		foreach ((_, string name, _, string define) in Levels)
		{
			Console.WriteLine($"  {define}: " + (state.Defines.Has(CManpower.MilitarySection, define)
				? state.Defines[CManpower.MilitarySection, define].ToString() : "not given, so " + CCommandLimit.Original));
		}
		Console.WriteLine($"  {CCommandLimit.LoneBrigadeDefine}: {(CCommandLimit.SparesLoneBrigades(state) ? "on" : "off")}");
		int ofOwn = state.TriggeredModifiers.Count(each => CCommandLimit.Kinds.Any(kind => each.Modifier[kind].Raw != 0));
		Console.WriteLine($"  triggered modifiers that give a limit: {ofOwn} of {state.TriggeredModifiers.Count}");

		foreach (string given in rest.Where(a => a.StartsWith("tech=", StringComparison.Ordinal)))
		{
			string[] parts = given["tech=".Length..].Split(':');
			if (parts.Length != 2 || !int.TryParse(parts[1], out int level) || !state.Technologies.ContainsKey(parts[0]))
			{
				Console.Error.WriteLine($"'{given}' names no technology and level");
				return 2;
			}
			country.TechnologyLevels[parts[0]] = level;
			Console.WriteLine($"{tag} given {parts[0]} at level {level}");
		}
		CCountryModifiers.RebuildDaily(state);

		Print(state, country);
		Console.WriteLine();
		Console.WriteLine("every other country whose limits are not the defines':");
		int others = 0;
		foreach (CCountry other in state.Countries)
		{
			if (!ReferenceEquals(other, country) && Levels.Any(level => Part(other, level.Modifier) != 0))
			{
				others++;
				Console.WriteLine($"  {other.Tag}: " + string.Join(", ", Levels.Select(level =>
					$"{level.Name} {Limit(state, other, level.Kind)}")));
			}
		}
		Console.WriteLine($"  {others} of {state.Countries.Count}");
		return 0;
	}

	private static void Print(CGameState state, CCountry country)
	{
		Console.WriteLine($"{country.Tag}:");
		foreach ((EUnitKind kind, string name, string modifier, _) in Levels)
		{
			Console.WriteLine($"  {name}: {Limit(state, country, kind)} - modifier {country.Modifier[modifier]}, " +
				$"technology {country.Technology[modifier]}");
		}
		foreach (CTriggeredModifier triggered in country.TriggeredModifiers)
		{
			string gives = string.Join(", ", CCommandLimit.Kinds.Where(kind => triggered.Modifier[kind].Raw != 0)
				.Select(kind => $"{kind} {triggered.Modifier[kind]}"));
			if (gives.Length != 0)
			{
				Console.WriteLine($"  has {triggered.Name}: {gives}");
			}
		}
		int commands = 0;
		int over = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Kind is not (EUnitKind.Corps or EUnitKind.Army or EUnitKind.ArmyGroup))
			{
				continue;
			}
			commands++;
			int counted = CCommandLimit.Counted(state, unit);
			int limit = CCommandLimit.Of(state, unit);
			if (counted > limit)
			{
				over++;
				if (over <= 8)
				{
					Console.WriteLine($"  {unit.Name} holds {counted}, may hold {limit}");
				}
			}
		}
		Console.WriteLine($"  commands holding more than they may: {over} of {commands}");
	}

	private static long Part(CCountry country, string kind)
	{
		return country.Modifier[kind].Raw + country.Technology[kind].Raw;
	}

	/// <summary>The limit of a level for a country, asked as of a commander of its own.</summary>
	private static int Limit(CGameState state, CCountry country, EUnitKind kind)
	{
		return CCommandLimit.Of(state, new CUnit(kind, country.Tag));
	}
}
