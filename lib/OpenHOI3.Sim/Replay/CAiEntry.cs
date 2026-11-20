namespace OpenHOI3.Sim.Replay;

/// <summary>
/// What the game makes ready before any step of the AI's runs - a step of its own or a decision a pass of the game calls -
/// whether it is recorded, played back or neither. The supply network is kept between askings and made again only when
/// a province has changed hands since; the AI asks for it all the time, and a step that made it would leave the game the
/// network of that moment, where a replay, the step not running, has it made later by whatever the game asks next. The
/// network hangs on more than the provinces' holders - infrastructure, naval bases, capitals, puppets - so the two can
/// differ, and with them what the day carries out of cut-off land. Made here, the network is made at the same moments
/// in both, and a step never makes it (<c>docs/replay.md</c>, section 3).
/// </summary>
public static class CAiEntry
{
	/// <summary>Brings the game's own caches up to date before an AI step reads them.</summary>
	public static void Prepare(CGameState state)
	{
		Supply.CSupplyNetworks.Refresh(state);
	}
}
