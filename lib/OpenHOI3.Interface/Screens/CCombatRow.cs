using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One row of the list in the window of an air battle or a bombing: a sub unit, a
/// building of the province, or what the province holds or makes of a good. See
/// <c>docs/mechanics/combatview.md</c>, section 11.
/// </summary>
public sealed record CCombatRow(ECombatRowKind Kind, CSubUnit? SubUnit = null, CBuilding? Building = null,
	EGoods Goods = EGoods.Supplies);
