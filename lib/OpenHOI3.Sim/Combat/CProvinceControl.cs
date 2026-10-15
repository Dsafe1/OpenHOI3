using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A province changes hands to whoever walked into it. Who that is, is not always the
/// one who walked: ground taken back from an enemy goes to the friend who owns it. See
/// <c>docs/mechanics/landcombat.md</c>, section 7.
/// </summary>
public static class CProvinceControl
{
	/// <summary>
	/// A country's unit stands in a province with no enemy beside it. Nothing changes
	/// where whoever holds the province is no enemy of that country. Otherwise it goes
	/// to the province's owner where the owner is no enemy either and not in exile, and
	/// else to the country - or to its master, up the chain while each master is at war
	/// with the holder; for a government in exile, to the head of its faction where the
	/// faction has two or more members and the head is an enemy of the owner. And where
	/// it is still the country's own and the country holds none of the province's
	/// neighbours, to the last neighbour's holder who is no puppet, on whose ground the
	/// country may operate and who is an enemy of the holder.
	///
	/// The change goes through <see cref="CProvinceHands.SetController"/> with its
	/// consequences, as every change of hands by force does. Answers whether the province
	/// changed hands. See <c>docs/mechanics/landcombat.md</c>, section 7.
	/// </summary>
	public static bool Take(CGameState state, CProvince province, CCountryTag by)
	{
		if (province.IsSea || province.Owner is not { } owner || !CMovementRules.IsEnemy(state, province.Controller, by))
		{
			return false;
		}
		CCountryTag to = by;
		while (state.Country(to) is { Overlord: { } overlord } && overlord != province.Controller
			&& CMovementRules.IsEnemy(state, overlord, province.Controller))
		{
			to = overlord;
		}
		if (state.Country(to) is { InExile: true } exile)
		{
			var members = CCountryLists.FactionMembers(state, exile.Faction);
			if (members.Count >= 2 && members[0] != exile && CMovementRules.IsEnemy(state, members[0].Tag, owner))
			{
				to = members[0].Tag;
			}
		}
		if (!CMovementRules.IsEnemy(state, owner, by) && state.Country(owner) is not { InExile: true })
		{
			to = owner;
		}
		else if (to == by && to != owner && Neighbour(state, province, to) is { } neighbour)
		{
			to = neighbour;
		}
		if (province.Controller == to)
		{
			return false;
		}
		CProvinceHands.SetController(state, province, to, consequences: true);
		// A battle's capture makes the province's modifier again at once, where a plain change of hands does not
		// (modifiers.md, section 3).
		Modifiers.CProvinceModifiers.RebuildOne(state, province);
		return true;
	}

	/// <summary>
	/// The holder of the province's neighbours a country taking it hands it to: none where
	/// the country holds one of them; otherwise the last, in the order of the province's
	/// ways out, whose holder is no puppet and on whose ground the country may operate -
	/// and that one only if it is an enemy of the province's holder.
	/// </summary>
	private static CCountryTag? Neighbour(CGameState state, CProvince province, CCountryTag to)
	{
		if (state.Country(to) is not { } taker)
		{
			return null;
		}
		CCountryTag? found = null;
		foreach (CProvinceEdge edge in province.Edges)
		{
			CCountryTag? holder = state.Province(edge.To).Controller;
			if (holder == to)
			{
				return null;
			}
			if (state.Country(holder) is { Overlord: null } other && CSupplyRelations.CanOperateIn(state, taker, other))
			{
				found = holder;
			}
		}
		return CMovementRules.IsEnemy(state, found, province.Controller) ? found : null;
	}
}
