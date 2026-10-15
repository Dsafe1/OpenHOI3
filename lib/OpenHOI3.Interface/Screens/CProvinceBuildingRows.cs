using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The province view's building rows: which buildings, in which column, each one's keyboard shortcut and label, whether
/// its button may be pressed and what its pips show. See <c>docs/mechanics/provinceview.md</c>, section 4.
/// </summary>
public static class CProvinceBuildingRows
{
	/// <summary>What the player must know of a province to see its buildings' levels.</summary>
	public const int LevelsIntel = 6;

	/// <summary>A pip hidden.</summary>
	public const int Hidden = 0;

	/// <summary>A pip given the first frame and left shown or hidden as it was.</summary>
	public const int FrameOnly = -1;

	/// <summary>Infrastructure under this much, in thousandths of its modifier, has no pips at no intelligence: two levels.</summary>
	private const long FewInfrastructure = 200;

	private const string Infrastructure = "infrastructure";

	/// <summary>
	/// The two columns: the file's buildings behind an empty first place, split at <c>(n - 1) / 2</c> of that place and
	/// them together, each keeping only those with <c>show_for_province</c>.
	/// </summary>
	public static (List<CBuilding> Left, List<CBuilding> Right) Columns(IReadOnlyList<CBuilding> buildings)
	{
		int half = buildings.Count / 2;                  // (n - 1) / 2, n counting the empty first place
		var left = new List<CBuilding>();
		var right = new List<CBuilding>();
		for (int index = 0; index < buildings.Count; index++)
		{
			if (buildings[index].ShowForProvince)
			{
				(index + 1 <= half ? left : right).Add(buildings[index]);
			}
		}
		return (left, right);
	}

	/// <summary>
	/// Each row's shortcut and label, the left column first: the first character of its name no row before it took,
	/// matched as written, and the name with that character in yellow; none and the plain name where all are taken.
	/// </summary>
	public static List<(string Shortcut, string Label)> Shortcuts(IEnumerable<string> names)
	{
		var taken = new HashSet<char>();
		var rows = new List<(string, string)>();
		foreach (string name in names)
		{
			int at = -1;
			for (int index = 0; index < name.Length; index++)
			{
				if (taken.Add(name[index]))
				{
					at = index;
					break;
				}
			}
			rows.Add(at < 0
				? ("", name)
				: (name[at].ToString(), name[..at] + "§Y" + name[at] + "§W" + name[(at + 1)..]));
		}
		return rows;
	}

	/// <summary>
	/// Whether a row's button may be pressed: with several provinces selected, where any of them the player holds may take
	/// one more; otherwise where the player holds the province and it may.
	/// </summary>
	public static bool Enabled(CGameState state, CCountry player, CBuilding building, CProvince province,
		IReadOnlyList<CProvince> selected)
	{
		if (selected.Count >= 2)
		{
			foreach (CProvince each in selected)
			{
				if (each.Controller == player.Tag && CBuildingLines.IsAllowed(state, player, building, each))
				{
					return true;
				}
			}
			return false;
		}
		return province.Controller == player.Tag && CBuildingLines.IsAllowed(state, player, building, province);
	}

	/// <summary>
	/// The pips, one a level of <c>max_level</c>: <see cref="Hidden"/>, a frame - 1 green working, 2 orange built and not
	/// working, 3 grey queued by the player - or <see cref="FrameOnly"/>. The levels are shown from intelligence 6, or 1
	/// for infrastructure; below, only whether anything is built, and infrastructure at none under two levels nothing.
	/// </summary>
	public static int[] Pips(CGameState state, CCountry player, CBuilding building, CProvince province)
	{
		var pips = new int[building.MaxLevel];
		int intel = CProvinceIntel.Level(state, province, player.Tag);
		bool infrastructure = building.ModifierKey == Infrastructure;
		CFixedPoint most = province.BuildingMaxLevel(building);
		if (intel >= LevelsIntel || (infrastructure && intel >= 1))
		{
			long built = most.Raw / CFixedPoint.Scale;
			long working = province.BuildingLevel(building).Raw;
			long queued = CBuildingLines.Queued(player, building, province.Id);
			for (int pip = 1; pip <= pips.Length; pip++)
			{
				pips[pip - 1] = pip > built + queued ? Hidden
					: pip > built ? 3
					: pip * CFixedPoint.Scale > working ? 2
					: 1;
			}
			return pips;
		}
		if (infrastructure && most.Raw * building.ModifierValue.Raw / CFixedPoint.Scale < FewInfrastructure)
		{
			return pips;
		}
		if (pips.Length > 0)
		{
			pips[0] = most > CFixedPoint.Zero ? FrameOnly : Hidden;
		}
		return pips;
	}
}
