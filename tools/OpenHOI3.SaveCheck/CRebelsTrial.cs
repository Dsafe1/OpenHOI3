using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

/// <summary>
/// A trial, not a check: no reference save has a rising in it. On a new game the occupier is put at war
/// with the occupied and given ten of its provinces away from the capital; then a rising of the roll's three
/// brigades is made in the first province it holds of the occupied's, and another in its own land where nobody stands; and
/// the days are run, telling each faction's armies and land, and every rising the roll makes:
///
///   OpenHOI3.SaveCheck rebels &lt;install folder&gt; [occupier] [occupied] [days] [seed]
///
/// Germany and Poland for thirty days with nothing given. See <c>docs/mechanics/rebels.md</c>.
/// </summary>
internal static class CRebelsTrial
{
	/// <summary>How many of the occupied's provinces the occupier is given.</summary>
	private const int Held = 10;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck rebels <install folder> [occupier] [occupied] [days] [seed]");
			return 2;
		}
		string[] tags = args.Skip(2).Where(a => a.Length == 3 && !int.TryParse(a, out _)).ToArray();
		int[] numbers = args.Skip(2).Select(a => int.TryParse(a, out int n) ? n : -1).Where(n => n >= 0).ToArray();
		CCountryTag occupierTag = new(tags.Length > 0 ? tags[0] : "GER");
		CCountryTag occupiedTag = new(tags.Length > 1 ? tags[1] : "POL");
		int days = numbers.Length > 0 ? numbers[0] : 30;

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		if (numbers.Length > 1)
		{
			state.Seed = (uint)numbers[1];
		}
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		if (state.Country(occupierTag) is not { } occupier || state.Country(occupiedTag) is not { } occupied)
		{
			Console.Error.WriteLine("no such country");
			return 2;
		}
		string Name(CCountryTag? tag) => state.Country(tag)?.Name ?? tag?.Value ?? "nobody";

		Console.WriteLine($"rebel types: {string.Join(", ", state.RebelTypes.Select(Describe))}");

		// The war, the occupied's units gone, and its land held but for the capital and the land beside it.
		CDeclarations.Apply(state, occupier, occupied, true, null);
		foreach (CUnit unit in occupied.UnitsInOrder())
		{
			OpenHOI3.Sim.Combat.CCombatRetreat.RemoveFromGame(state, unit);
		}
		CProvince capital = state.Province(occupied.Capital);
		var kept = new HashSet<int>(capital.Edges.Select(edge => edge.To)) { capital.Id };
		// Ten of them, so that the occupied does not surrender.
		var held = state.Provinces.Where(p => p.Owner == occupiedTag && !kept.Contains(p.Id)).Take(Held).ToList();
		foreach (CProvince province in held)
		{
			CProvinceHands.SetController(state, province, occupierTag, consequences: true);
		}
		OpenHOI3.Sim.Modifiers.CProvinceModifiers.Rebuild(state);
		Console.WriteLine($"{occupierTag} holds {held.Count} of {occupiedTag}'s provinces, at {OpenHOI3.Sim.Occupation.COccupationPolicies.Of(state, occupier, occupied).Name}");
		foreach (CProvince province in held.Take(3).Append(state.Province(occupier.Capital)))
		{
			CRebelType? likely = CRebelChoice.TypeFor(state, province);
			CCountryTag? freed = likely is { Independence: true } ? CRebelChoice.IndependenceOf(state, likely, province) : null;
			Console.WriteLine($"   {province.Id} {province.Name}: likely {likely?.Name ?? "none"}{(freed is { } f ? $", freeing {f}" : "")}");
		}

		if (held.Count > 0)
		{
			Rise(state, held[0], Name, told);
		}
		// The second in the occupier's own land, where no unit stands and some type would rise.
		CProvince? home = state.Provinces.FirstOrDefault(p => p.Owner == occupierTag && p.Controller == occupierTag
			&& state.UnitsIn(p.Id).Count == 0 && CRebelChoice.TypeFor(state, p) is not null && CRebelFactions.AreaOf(state, p.Id) is not null);
		Rise(state, home ?? state.Province(occupier.Capital), Name, told);

		int factionsMade = state.LastRebelId;
		for (int day = 1; day <= days; day++)
		{
			CDate today = state.Date;
			while (state.Date == today)
			{
				CTick.RunHour(state);
				var hour = new List<CCommand>(state.Outbox);
				CCommand.DropRefused(state, hour);
				foreach (CCommand command in hour)
				{
					if (command.MayExecute(state))
					{
						command.Execute(state);
					}
				}
				state.Outbox.Clear();
			}
			if (day == 1 || day % 5 == 0 || day == days)
			{
				Console.WriteLine($"{state.Date}:");
				foreach (CRebelFaction faction in state.RebelFactions)
				{
					var armies = faction.Armies.Select(id => CRebelFactions.FindUnit(state, id)).Where(u => u is not null)
						.Select(u => $"{u!.Name} at {u.Location}{(u.IsMoving ? $" to {string.Join(",", u.Path)} ({u.MovementProgress})" : "")} org {u.SubUnits[0].Organisation}{(u.IsFighting ? " fighting" : "")} ({u.SubUnits.Count})");
					Console.WriteLine($"   {faction.Name}: holds {(faction.Provinces.Count == 0 ? "nothing" : string.Join(" ", faction.Provinces))}; {string.Join("; ", armies)}");
				}
			}
		}
		Console.WriteLine($"factions made by the roll in {days} days: {state.LastRebelId - factionsMade}; standing {state.RebelFactions.Count}");
		Console.WriteLine($"provinces the rebels hold: {state.Provinces.Count(p => CRebelFactions.IsRebels(p.Controller))}");
		Console.WriteLine($"told: {string.Join(", ", told.GroupBy(m => m.We).Select(g => $"{g.Key} x{g.Count()}"))}");
		return 0;
	}

	private static string Describe(CRebelType type)
	{
		var keys = new List<string> { $"factor {type.SpawnChance.Days}" };
		if (type.Independence)
		{
			keys.Add("independence");
		}
		if (type.IsTriggeredOnly)
		{
			keys.Add("triggered only");
		}
		return $"{type.Name} ({string.Join(", ", keys)}, {type.Unit})";
	}

	private static void Rise(CGameState state, CProvince province, Func<CCountryTag?, string> name, List<CPactMessage> told)
	{
		int heard = told.Count;
		CCountryTag? holder = province.Controller;
		CRebelType? type = CRebelChoice.TypeFor(state, province);
		CRisings.Rise(state, province, type, CRevoltRoll.Brigades);
		Console.WriteLine();
		Console.WriteLine($"a rising of {type?.Name ?? "nobody"} in {province.Id} {province.Name}, held by {name(holder)}:");
		// The faction there: the one holding the province or with an army in it.
		if ((CRebelFactions.Holding(state, province.Id) ?? state.RebelFactions.LastOrDefault()) is not { } made)
		{
			Console.WriteLine("   nothing made");
			return;
		}
		Console.WriteLine($"   faction {made.Name}: country {made.Country}, targets {string.Join(" ", made.Targets)}, frees {made.Independence?.Value ?? "nobody"}, government {made.Government?.Name ?? "none"}");
		foreach (int id in made.Armies)
		{
			if (CRebelFactions.FindUnit(state, id) is not { } army)
			{
				continue;
			}
			Console.WriteLine($"   {army.Name}, {army.Owner}'s, {(army.IsFighting ? "fighting" : "not fighting")}: " +
				string.Join("; ", army.SubUnits.Select(b => $"{b.Name} exp {b.Experience} str {b.Strength} org {b.Organisation} tech {string.Join(",", b.TechnologyLevels.Values)}")));
		}
		Console.WriteLine($"   the province now held by {name(province.Controller)}; told: {string.Join(", ", told.Skip(heard).Select(m => $"{m.We} to {m.Told}"))}");
	}
}
