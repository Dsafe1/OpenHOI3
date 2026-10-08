namespace OpenHOI3.Sim;

/// <summary>
/// The passing of one hour. Time is a command like any other, so that the hour turns
/// at the same point in the stream of commands on every machine.
/// </summary>
public sealed class CHourlyTickCommand : CCommand
{
	public override void Execute(CGameState state)
	{
		CTick.RunHour(state);
	}
}
