using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>One row of the order of battle browser's list: a unit and how deep in the tree it stands.</summary>
public readonly record struct COobRow(CUnit Unit, int Level);
