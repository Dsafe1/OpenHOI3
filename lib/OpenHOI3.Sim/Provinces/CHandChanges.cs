namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// How many times a province of one game has changed owner or controller. The game state hands one
/// to each of its provinces as it is made, and a province counts here itself whenever either
/// changes, so whatever keeps a picture of who holds what - the supply network, a country's facts -
/// sees the map move however it moved. A change made before the game exists, as the history is
/// read, counts nothing: nothing has been worked out from the map yet.
/// </summary>
public sealed class CHandChanges
{
	public int Count { get; private set; }

	internal void Add()
	{
		Count++;
	}
}
