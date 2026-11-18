namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// How many times a province of one game has changed owner or controller. The game state hands one
/// to each of its provinces as it is made, and a province counts here itself whenever either
/// changes, so whatever keeps a picture of who holds what - the supply network, a country's facts -
/// sees the map move however it moved. A change made before the game exists, as the history is
/// read, counts nothing: nothing has been worked out from the map yet.
///
/// Beside it, and apart from it, how many times what is built in a province has been set: a country's facts ask
/// whether it has a harbour, which a naval base built or lost changes without any change of hands.
/// </summary>
public sealed class CHandChanges
{
	public int Count { get; private set; }

	/// <summary>How many times a building's built level has been set in a province. It moves no <see cref="Count"/>.</summary>
	public int BuiltCount { get; private set; }

	internal void Add()
	{
		Count++;
	}

	internal void AddBuilt()
	{
		BuiltCount++;
	}
}
