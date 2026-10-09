using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One line of the window of orders a right click with a fleet or an air unit brings
/// up: what it is called, which order it gives, and whether it can be given.
/// </summary>
/// <param name="NameKey">The localisation key of the order's name.</param>
/// <param name="Order">The order it gives, or none for one of the original's that is not built.</param>
/// <param name="MayGive">Whether its box can be ticked.</param>
public readonly record struct COrderRow(string NameKey, EUnitOrder Order, bool MayGive);
