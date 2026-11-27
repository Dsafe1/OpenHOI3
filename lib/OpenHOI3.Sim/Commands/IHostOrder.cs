namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A command only the host of a game between several machines may give: one that changes the game for everybody and
/// is no country's order, as a change of its rules is. The host drops one a client sends
/// (<see cref="Multiplayer.COrderCheck"/>); in a game on one machine anybody playing may give it
/// (<c>docs/multiplayer.md</c>, section 4.1).
/// </summary>
public interface IHostOrder
{
}
