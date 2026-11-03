using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One row of the outliner: a section's title, a unit at its depth in the tree, a battle or bombing, or an allied
/// objective's province and which way it was asked. Two rows are the same where they stand for the same thing, which is
/// what tells the outliner its rows need not be made again. See <c>docs/mechanics/outliner.md</c>, section 4.
/// </summary>
public sealed record COutlinerRow(EOutlinerSection Section, bool IsTitle = false, CUnit? Unit = null, int Depth = 0,
	CCombat? Combat = null, int Province = 0, bool AskedOfPlayer = false);
