using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's build queue and what is waiting to be placed, as a save writes them: each line under its kind's key in
/// queue order - <c>military_construction</c>, <c>building_construction</c>, <c>convoy_construction</c> - with its cost,
/// days and progress as they stood, never priced again; a finished unit in a <c>unit_deployment</c>, a finished
/// building in a <c>building_deployment</c>; and the saved division templates. See <c>docs/mechanics/savegame.md</c>,
/// section 5.
/// </summary>
public static class CSaveLines
{
	/// <summary>The keys every line has, read here for all three kinds.</summary>
	private static readonly HashSet<string> Shared = new(System.StringComparer.Ordinal)
	{
		"id", "size", "cost", "duration", "progress", "status", "location", "country",
	};

	public static void ReadMilitary(CGameState state, CCountry country, ClausewitzNode block, CSaveIds ids, CSaveReport report)
	{
		var line = new CConstruction(country.Tag);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key || ReadShared(line, entry, ids))
			{
				continue;
			}
			if (entry.IsBlock && state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type))
			{
				line.Brigades.Add(ReadBrigade(type, entry, ids, report));
				continue;
			}
			switch (key)
			{
				case "name":
					line.Name = entry.Value?.Trim('"') ?? "";
					break;
				case "unit":
					line.UnitId = ids.Of(entry);
					break;
				case "manpower":
					line.Manpower = CSaveValues.Fixed(entry.Value);
					break;
				case "target":
					line.Target = ids.Of(entry);
					break;
				case "is_reserve":
					line.IsReserve = entry.Value == "yes";
					break;
				case "factor":
					line.ReservesFactor = CSaveValues.Fixed(entry.Value);
					break;
				// The original's own spelling.
				case "accumulated_experiance":
					line.AccumulatedExperience = CSaveValues.Fixed(entry.Value);
					break;
				case "accumulated_progress":
					line.AccumulatedProgress = CSaveValues.Fixed(entry.Value);
					break;
				default:
					report.Unread("military_construction", key);
					break;
			}
		}
		// A line with no unit's number gets new ones, as the original's gives it.
		if (line.UnitId == 0)
		{
			line.UnitId = ++state.LastUnitId;
		}
		foreach (CBrigadeOrder brigade in line.Brigades.Where(brigade => brigade.Id == 0))
		{
			brigade.Id = ++state.LastUnitId;
		}
		Enqueue(state, country, line);
	}

	public static void ReadBuilding(CGameState state, CCountry country, ClausewitzNode block, CSaveIds ids, CSaveReport report)
	{
		// The building by its place in the file, counted from one.
		long index = CSaveValues.Whole(block, "building") - 1;
		if (index < 0 || index >= state.Buildings.Count)
		{
			report.Unread("building_construction", "a line of no building known, left out");
			return;
		}
		var line = new CConstruction(country.Tag) { Building = state.Buildings[(int)index] };
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key || ReadShared(line, entry, ids) || key == "building")
			{
				continue;
			}
			report.Unread("building_construction", key);
		}
		Enqueue(state, country, line);
	}

	public static void ReadConvoy(CGameState state, CCountry country, ClausewitzNode block, CSaveIds ids, CSaveReport report)
	{
		// Its second status says whether it builds escorts.
		List<ClausewitzNode> statuses = block.All("status").ToList();
		bool escorts = statuses.Count > 1 && statuses[^1].Value == "yes";
		var line = new CConstruction(country.Tag) { IsConvoy = true, IsEscorts = escorts };
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key)
			{
				continue;
			}
			if (key == "status" && entry.Value is "yes" or "no")
			{
				continue;
			}
			if (!ReadShared(line, entry, ids))
			{
				report.Unread("convoy_construction", key);
			}
		}
		Enqueue(state, country, line);
	}

	/// <summary>A finished unit waiting to be placed: its number, then the one unit, which has no location.</summary>
	public static void ReadUnitDeployment(CCountry country, ClausewitzNode block, CSaveIds ids, CSaveUnitReader units,
		CSaveReport report)
	{
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key || key == "id")
			{
				continue;
			}
			if (entry.IsBlock && CSaveUnitReader.UnitKinds.TryGetValue(key, out EUnitKind kind))
			{
				if (units.Read(entry, kind, country, parent: null, carriedBy: null, placed: false) is { } unit)
				{
					unit.DeploymentId = ids.Of(block.Get("id"));
					country.Deployments.Add(unit);
				}
				continue;
			}
			report.Unread("unit_deployment", key);
		}
	}

	public static void ReadBuildingDeployment(CGameState state, CCountry country, ClausewitzNode block, CSaveIds ids,
		CSaveReport report)
	{
		long index = CSaveValues.Whole(block, "building") - 1;
		if (index < 0 || index >= state.Buildings.Count)
		{
			report.Unread("building_deployment", "a building of none known, left out");
			return;
		}
		country.BuildingDeployments.Add(new CBuildingDeployment(ids.Of(block.Get("id")), country.Tag, state.Buildings[(int)index]));
	}

	/// <summary>
	/// The saved templates, in place of the country file's: each a name and its brigades' types. The original pads the
	/// list to twelve with empty <c>PW_TEMPLATE_</c> entries, which are left out here as a new game here has none.
	/// </summary>
	public static void ReadTemplates(CCountry country, ClausewitzNode block)
	{
		country.Templates.Clear();
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || !entry.IsBlock || entry.Children.Count == 0)
			{
				continue;
			}
			var template = new CDivisionTemplate(entry.Key);
			foreach (ClausewitzNode type in entry.Children)
			{
				if (type.Value is { Length: > 0 } name)
				{
					template.Brigades.Add(name);
				}
			}
			country.Templates.Add(template);
		}
	}

	/// <summary>The original pads a country's templates to this many with empty ones.</summary>
	private const int TemplateSlots = 12;

	/// <summary>
	/// The queue, each line under its kind's key in queue order: the keys every line has, then a unit's name, number,
	/// manpower, reserve, brigades and what it has gathered, a building line's building counted from one, and a convoy
	/// line's second status, whether it builds escorts.
	/// </summary>
	public static void WriteQueue(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		foreach (CConstruction line in country.Constructions)
		{
			writer.Begin(line.IsBuilding ? "building_construction" : line.IsConvoy ? "convoy_construction" : "military_construction");
			writer.IdPair("id", CSaveUnitWriter.Pair(state, line.Id));
			writer.Int("size", line.Size);
			writer.Fixed("cost", line.Cost);
			writer.Fixed("duration", line.Duration);
			writer.Fixed("progress", line.Progress);
			writer.Fixed("status", line.Status);
			if (line.Location > 0)
			{
				writer.Int("location", line.Location);
			}
			writer.Quoted("country", line.Country.Value);
			if (line.Building is { } building)
			{
				writer.Int("building", BuildingNumber(state, building));
			}
			else if (line.IsConvoy)
			{
				writer.Bool("status", line.IsEscorts);
			}
			else
			{
				WriteUnitLine(writer, state, line);
			}
			writer.End();
		}
	}

	private static void WriteUnitLine(CSaveTextWriter writer, CGameState state, CConstruction line)
	{
		// A name of one letter is not written.
		if (line.Name.Length >= 2)
		{
			writer.Quoted("name", line.Name);
		}
		writer.IdPair("unit", CSaveUnitWriter.Pair(state, line.UnitId));
		writer.Fixed("manpower", line.Manpower);
		if (line.Target != 0)
		{
			writer.IdPair("target", CSaveUnitWriter.Pair(state, line.Target));
		}
		if (line.IsReserve)
		{
			writer.Bool("is_reserve", true);
			writer.Fixed("factor", line.ReservesFactor);
		}
		foreach (CBrigadeOrder brigade in line.Brigades)
		{
			writer.Begin(brigade.Type.Key);
			writer.IdPair("id", CSaveUnitWriter.Pair(state, brigade.Id));
			if (brigade.Name.Length > 0)
			{
				writer.Quoted("name", brigade.Name);
			}
			if (brigade.Model.Count > 0)
			{
				writer.IndentedList("model", brigade.Model.Select(level => level.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
			if (brigade.Experience.Raw > 0)
			{
				writer.Fixed("experience", brigade.Experience);
			}
			writer.End();
		}
		writer.Fixed("accumulated_experiance", line.AccumulatedExperience);
		writer.Fixed("accumulated_progress", line.AccumulatedProgress);
	}

	/// <summary>
	/// What waits to be placed: a finished unit with no location, and a finished building with its number and its
	/// building. A unit's deployment's own number is written where it has one (<c>savegame.md</c>, section 9).
	/// </summary>
	public static void WriteDeployments(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		foreach (CUnit unit in country.Deployments)
		{
			writer.Begin("unit_deployment");
			if (unit.DeploymentId > 0)
			{
				writer.IdPair("id", CSaveUnitWriter.Pair(state, unit.DeploymentId));
			}
			CSaveUnitWriter.Write(writer, state, unit);
			writer.End();
		}
		foreach (CBuildingDeployment deployment in country.BuildingDeployments)
		{
			writer.Begin("building_deployment");
			writer.IdPair("id", CSaveUnitWriter.Pair(state, deployment.Id));
			writer.Int("building", BuildingNumber(state, deployment.Building));
			writer.End();
		}
	}

	/// <summary>
	/// The templates, each its brigades' types at column 0, padded to twelve with empty <c>PW_TEMPLATE_</c> entries
	/// named by their place, as the original's list always is.
	/// </summary>
	public static void WriteTemplates(CSaveTextWriter writer, CCountry country)
	{
		writer.Begin("saved_templates");
		foreach (CDivisionTemplate template in country.Templates)
		{
			if (template.Brigades.Count > 0)
			{
				writer.Words(template.Name, template.Brigades);
			}
			else
			{
				writer.Begin(template.Name);
				writer.End();
			}
		}
		for (int slot = country.Templates.Count; slot < TemplateSlots; slot++)
		{
			writer.Begin("PW_TEMPLATE_" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
			writer.End();
		}
		writer.End();
	}

	/// <summary>A building as a save names it: its place in the file, counted from one.</summary>
	private static int BuildingNumber(CGameState state, CBuilding building)
	{
		for (int index = 0; index < state.Buildings.Count; index++)
		{
			if (state.Buildings[index] == building)
			{
				return index + 1;
			}
		}
		return 0;
	}

	/// <summary>A line's shared key; answers whether it was one.</summary>
	private static bool ReadShared(CConstruction line, ClausewitzNode entry, CSaveIds ids)
	{
		if (!Shared.Contains(entry.Key!))
		{
			return false;
		}
		switch (entry.Key)
		{
			case "id":
				line.Id = ids.Of(entry);
				break;
			case "size":
				line.Size = (int)System.Math.Max(1, CSaveValues.Whole(entry.Value));
				break;
			case "cost":
				line.Cost = CSaveValues.Fixed(entry.Value);
				break;
			case "duration":
				line.Duration = CSaveValues.Fixed(entry.Value);
				break;
			case "progress":
				line.Progress = CSaveValues.Fixed(entry.Value);
				break;
			case "status":
				line.Status = CSaveValues.Fixed(entry.Value);
				break;
			case "location":
				line.Location = (int)CSaveValues.Whole(entry.Value);
				break;
			case "country":
				break;
		}
		return true;
	}

	private static CBrigadeOrder ReadBrigade(CSubUnitType type, ClausewitzNode block, CSaveIds ids, CSaveReport report)
	{
		var order = new CBrigadeOrder(type);
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
					break;
				case "id":
					order.Id = ids.Of(entry);
					break;
				case "name":
					order.Name = entry.Value?.Trim('"') ?? "";
					break;
				case "experience":
					order.Experience = CSaveValues.Fixed(entry.Value);
					break;
				// The levels themselves, one for each of its type's technologies.
				case "model":
					order.Model.Clear();
					foreach (ClausewitzNode level in entry.Children)
					{
						order.Model.Add((int)CSaveValues.Whole(level.Value));
					}
					break;
				default:
					report.Unread("a brigade under construction", entry.Key);
					break;
			}
		}
		return order;
	}

	/// <summary>A line read is put at the end of its country's queue, as the original enqueues it.</summary>
	private static void Enqueue(CGameState state, CCountry country, CConstruction line)
	{
		if (line.Id == 0)
		{
			line.Id = ++state.LastUnitId;
		}
		country.Constructions.Add(line);
	}
}
