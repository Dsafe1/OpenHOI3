using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The resources map mode's icons, made once as a game is entered from what each province can yield then, and drawn only in
/// that mode and only near, under a camera height of 500. A good's icon stands where a building does: the building of
/// buildings.txt whose place, counting the original's own <c>nobuilding</c> first, is the good's in the goods pool - the 3rd
/// to the 6th - on that building's point, and where positions.txt gives the province none, on the middle of the province, its
/// label point, by the maintainer's word of 2026-11-07. The strategic resource's always stands on the label point. See <c>docs/mechanics/mapmodes.md</c>, section 5, and
/// <c>reversing/findings/FINDINGS-resourceicons.md</c>.
/// </summary>
public static class CResourceMapIcons
{
	/// <summary>The camera height from which the icons are not drawn [exe].</summary>
	public const float Highest = 500f;

	/// <summary>The four goods with an icon, and the end of each icon's name, in the goods pool's order.</summary>
	private static readonly (EGoods Good, string Name)[] Goods =
	{
		(EGoods.CrudeOil, "oil"), (EGoods.Metal, "metal"), (EGoods.Energy, "energy"), (EGoods.RareMaterials, "raremat"),
	};

	public static List<CResourceMapIcon> Build(CGameState state, IReadOnlyList<CBuildingPosition[]> placed)
	{
		var icons = new List<CResourceMapIcon>();
		for (int id = 1; id < state.Provinces.Count && id < placed.Count; id++)
		{
			// Every province here is simulated: which the original's are not was not read (provinceview.md, section 1).
			CProvince province = state.Provinces[id];
			foreach ((EGoods good, string name) in Goods)
			{
				if (province.MaxProducing[good] <= CFixedPoint.Zero)
				{
					continue;
				}
				// The pool's slot is the database's, whose first entry is the original's nobuilding and not in the file.
				int slot = (int)good - 1;
				string building = slot < state.Buildings.Count ? state.Buildings[slot].Key : "";
				(float x, float y) = PlaceOf(placed[id], building) ?? LabelPoint(province);
				icons.Add(new CResourceMapIcon(id, "resource_" + name, x, y));
			}
			if (province.StrategicResource.Length > 0)
			{
				(float x, float y) = LabelPoint(province);
				icons.Add(new CResourceMapIcon(id, "STRATEGIC_" + province.StrategicResource, x, y));
			}
		}
		return icons;
	}

	/// <summary>A building's point in a province, a world point as the file writes it, or null where positions.txt gives it none.</summary>
	public static (float X, float Y)? PlaceOf(CBuildingPosition[] placed, string building)
	{
		foreach (CBuildingPosition position in placed)
		{
			if (position.Building.Equals(building, System.StringComparison.OrdinalIgnoreCase))
			{
				return (position.X, position.Y);
			}
		}
		return null;
	}

	/// <summary>
	/// The middle of the province's box, which positions.txt does not move: a world point, as a building's place is, which the
	/// 2D map draws through <see cref="CWorldPoint"/>.
	/// </summary>
	private static (float X, float Y) LabelPoint(CProvince province) => (province.CentreX, province.CentreY);
}
