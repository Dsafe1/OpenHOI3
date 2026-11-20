using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the map shows of every plan of the player's while a headquarters of its under an AI is selected: a marker on each objective
/// and an arrow along each land unit's path, the selected unit's own in one style and the others' in another. See
/// <c>docs/mechanics/mapunits.md</c>, section 12.
/// </summary>
public static class CPlanMarkers
{
	public const string OwnArrow = "AutomationArrow";
	public const string OtherArrow = "OtherAutomationArrow";
	public const string Picture = "icon_objective_strip";

	/// <summary>How far past the last province an arrow runs, as a share of its last leg.</summary>
	private const float Overrun = 0.25f;

	public sealed record CMarkers(List<(int Province, bool Own)> Objectives, List<(List<int> Path, bool Own)> Arrows);

	public static CMarkers Of(CGameState state, CCountry? player, CUnit? selected)
	{
		var made = new CMarkers(new(), new());
		if (player is null || selected is null || selected.Owner != player.Tag || CAgents.HighestAutomated(selected) is null)
		{
			return made;
		}
		foreach (CUnit unit in player.AllUnits())
		{
			if (CAgents.HighestAutomated(unit) is null)
			{
				continue;
			}
			bool own = ReferenceEquals(unit, selected);
			made.Objectives.AddRange(unit.Plan.Objectives.Select(objective => (objective.Province, own)));
			if (CMovementRules.IsLandUnit(unit) && unit.Plan.Path.Count > 0)
			{
				made.Arrows.Add((new List<int>(unit.Plan.Path), own));
			}
		}
		return made;
	}

	/// <summary>An arrow's points: each province's centre on the path, and a last a quarter of the last leg further on, in whole pixels.</summary>
	public static List<(int X, int Y)> ArrowPoints(IReadOnlyList<(int X, int Y)> centres)
	{
		var points = new List<(int X, int Y)>(centres);
		if (centres.Count > 0)
		{
			(int x, int y) = centres[^1];
			(int lastX, int lastY) = centres.Count > 1 ? centres[^2] : centres[^1];
			points.Add((x + (int)((x - lastX) * Overrun), y + (int)((y - lastY) * Overrun)));
		}
		return points;
	}
}
