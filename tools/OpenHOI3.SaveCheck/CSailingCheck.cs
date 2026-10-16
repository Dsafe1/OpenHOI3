using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

/// <summary>
/// The part of the march check that is for fleets and air units. One of those goes
/// through several provinces in an hour, so it is not its gain on one edge that is
/// compared: every fleet and air unit with a path in the earlier save is given its
/// ships or wings, its fuel, its place and its path, is moved here hour by hour for the
/// hours between the saves, and is then set against the later save - the province it is
/// in, the path it has left and how far along the next edge it is. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 6.
/// </summary>
internal static class CSailingCheck
{
	private const string Information = "for information: ";

	private sealed record Saved(ClausewitzNode Node, CCountry Country, EUnitKind Kind, int Location, int Base,
		List<int> Path, CFixedPoint Progress);

	public static string Run(CGameState state, ClausewitzNode earlier, ClausewitzNode later, long hours,
		IReadOnlyDictionary<int, CSubUnit> subUnits, Action<string, bool, Func<string>> compare)
	{
		Dictionary<int, Saved> before = Units(state, earlier);
		Dictionary<int, Saved> after = Units(state, later);

		int moving = 0;
		int gone = 0;
		int busy = 0;
		int unknown = 0;
		int carrying = 0;
		foreach ((int id, Saved saved) in before.OrderBy(entry => entry.Key))
		{
			// A land unit aboard stands where its fleet does (transport.md, section 5).
			foreach (ClausewitzNode aboard in Passengers(saved.Node))
			{
				carrying++;
				compare("a land unit aboard a fleet stands where the fleet does", aboard.GetInt("location") == saved.Location,
					() => $"{saved.Country.Tag} {id}: in {saved.Location}, {aboard.GetString("name")} in {aboard.GetInt("location")}");
			}
			if (saved.Path.Count == 0)
			{
				continue;
			}
			moving++;
			string kind = saved.Kind == EUnitKind.Navy ? "fleet" : "air unit";
			string who = $"{saved.Country.Tag} {id} {saved.Node.GetString("name")}";

			var unit = new CUnit(saved.Kind, saved.Country.Tag)
			{
				Id = id,
				Location = saved.Location,
				Base = saved.Base,
				MovementProgress = saved.Progress,
				FuelReceived = Number(saved.Node.GetString("fuel")),
			};
			unit.Path.AddRange(saved.Path);
			bool known = true;
			foreach (ClausewitzNode part in saved.Node.All(saved.Kind == EUnitKind.Navy ? "ship" : "wing"))
			{
				if (subUnits.TryGetValue(part.Get("id")?.GetInt("id") ?? 0, out CSubUnit? subUnit))
				{
					unit.SubUnits.Add(subUnit);
				}
				else
				{
					known = false;
				}
			}
			if (!known || unit.SubUnits.Count == 0)
			{
				unknown++;
				continue;
			}

			CProvince here = state.Province(saved.Location);
			CProvince next = state.Province(saved.Path[0]);
			compare($"a moving {kind}'s next province is one its own has an edge to", here.EdgeTo(next.Id) is not null,
				() => $"{who}: in {here.Id} {here.Name}, bound for {next.Id} {next.Name}");

			List<int>? route = CRouteFinder.Find(state, saved.Country, unit, saved.Location, saved.Path[^1]);
			compare(Information + $"the way found here for a {kind} is the way it has",
				route is not null && route.SequenceEqual(saved.Path),
				() => $"{who}: from {here.Id}, save [{string.Join(' ', saved.Path)}], " +
					$"ours [{(route is null ? "none" : string.Join(' ', route))}]");

			if (saved.Base > 0 && saved.Base < state.Provinces.Count)
			{
				CProvince end = state.Province(saved.Path[^1]);
				CFixedPoint range = CUnitOrders.RangeOf(unit);
				CFixedPoint distance = CUnitOrders.DistanceFromBase(state, unit, end);
				compare(Information + $"where a {kind} is going is within its range of its base, or is its base",
					end.Id == saved.Base || range >= distance,
					() => $"{who}: base {saved.Base}, bound for {end.Id} {end.Name}, range {range}, distance {distance}");
			}

			if (!after.TryGetValue(id, out Saved? then))
			{
				gone++;
				continue;
			}
			if (IsBusy(saved.Node) || IsBusy(then.Node) || Number(then.Node.GetString("fuel")) != unit.FuelReceived)
			{
				busy++;
				continue;
			}
			// A new order in between shows as a path that is not the tail of the old one.
			bool sameWay = then.Path.Count <= saved.Path.Count
				&& then.Path.SequenceEqual(saved.Path.Skip(saved.Path.Count - then.Path.Count));
			if (!sameWay)
			{
				gone++;
				continue;
			}

			// Its passengers come aboard as the save has them, in its order.
			var passengers = new List<(CUnit Unit, string Name)>();
			foreach (ClausewitzNode aboard in Passengers(saved.Node))
			{
				var passenger = new CUnit(EUnitKind.Division, saved.Country.Tag)
				{
					Id = aboard.Get("id")?.GetInt("id") ?? 0,
					Location = aboard.GetInt("location"),
					Previous = aboard.GetInt("previous"),
				};
				OpenHOI3.Sim.Transport.CTransport.Board(unit, passenger);
				passengers.Add((passenger, aboard.GetString("name") ?? ""));
			}

			long speed = CMovementSpeed.Speed(state, saved.Country, unit).Raw;
			for (long hour = 0; hour < hours; hour++)
			{
				CMovement.RunHour(state, saved.Country, unit);
			}
			foreach ((CUnit passenger, string name) in passengers)
			{
				ClausewitzNode? still = Passengers(then.Node).FirstOrDefault(node => (node.Get("id")?.GetInt("id") ?? 0) == passenger.Id);
				compare($"where {hours} hours took a land unit aboard a fleet",
					still is not null && passenger.Transport == unit && passenger.Location == still.GetInt("location")
						&& passenger.Previous == still.GetInt("previous"),
					() => $"{who}: {name} - the save {(still is null ? "has it ashore" : $"has it in {still.GetInt("location")}, previous {still.GetInt("previous")}")}, " +
						$"ours {(passenger.Transport is null ? "ashore" : "aboard")} in {passenger.Location}, previous {passenger.Previous}");
			}
			string types = string.Join('+', unit.SubUnits.Select(subUnit => subUnit.Type.Key).Distinct());
			string detail = $"{who} [{types}] from {saved.Location} at {saved.Progress}, {new CFixedPoint(speed)} an hour " +
				$"(top speed {CMovementSpeed.SlowestBrigade(unit)}, fuel {unit.FuelReceived}): " +
				$"the save in {then.Location} at {then.Progress} with {then.Path.Count} to go, " +
				$"ours in {unit.Location} at {unit.MovementProgress} with {unit.Path.Count} to go";
			compare($"where {hours} hours took a {kind}", unit.Location == then.Location && unit.Path.SequenceEqual(then.Path),
				() => detail);
			if (unit.Location == then.Location)
			{
				compare($"how far along its next edge a {kind} is after {hours} hours",
					unit.MovementProgress == then.Progress, () => detail);
			}
		}
		return $"{before.Count} fleets and air units in the earlier save, {moving} with a path, {carrying} land units aboard; not compared: " +
			$"{gone} gone or under another order by the later save, {busy} in a battle or with their fuel changed, " +
			$"{unknown} with a ship or a wing a new game does not start with";
	}

	/// <summary>The land units a fleet's block holds, each a block of its own under its level's key.</summary>
	private static IEnumerable<ClausewitzNode> Passengers(ClausewitzNode fleet)
	{
		return fleet.Children.Where(child => child.IsBlock
			&& child.Key is "theatre" or "armygroup" or "army" or "corps" or "division");
	}

	private static bool IsBusy(ClausewitzNode unit)
	{
		return unit.Get("combat") is not null || unit.Get("attack_delay") is not null;
	}

	private static Dictionary<int, Saved> Units(CGameState state, ClausewitzNode save)
	{
		var found = new Dictionary<int, Saved>();
		foreach (ClausewitzNode node in save.Children)
		{
			if (node.Key is not null && node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag)
				&& state.Country(tag) is { } country)
			{
				Collect(state, node, country, found);
			}
		}
		return found;
	}

	private static void Collect(CGameState state, ClausewitzNode node, CCountry country, Dictionary<int, Saved> found)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (!child.IsBlock || child.Key is null)
			{
				continue;
			}
			if (child.Key is "navy" or "air")
			{
				int id = child.Get("id")?.GetInt("id") ?? 0;
				int location = child.GetInt("location");
				var path = new List<int>();
				foreach (ClausewitzNode step in child.Get("path")?.Children ?? new List<ClausewitzNode>())
				{
					if (int.TryParse(step.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int province))
					{
						path.Add(province);
					}
				}
				if (id > 0 && location > 0 && location < state.Provinces.Count
					&& path.All(province => province > 0 && province < state.Provinces.Count))
				{
					found[id] = new Saved(child, country, child.Key == "navy" ? EUnitKind.Navy : EUnitKind.Air, location,
						child.GetInt("base"), path, Number(child.GetString("movement_progress")));
				}
				continue;
			}
			if (child.Key is "theatre" or "armygroup" or "army" or "corps" or "division")
			{
				Collect(state, child, country, found);
			}
		}
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
