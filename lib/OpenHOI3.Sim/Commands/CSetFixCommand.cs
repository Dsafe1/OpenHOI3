using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// One fix of an enhanced game switched on or off while the game runs: the rules it carries are changed, and with them
/// what a save of it writes, what its hash ends in and what machines playing it together compare
/// (<c>docs/enhanced.md</c>, <i>The shape</i>). It is the host's alone to give (<see cref="IHostOrder"/>).
///
/// Refused in a faithful game, which has no fixes; for a name that is no fix; and for a fix that acts only as the
/// files are read (<see cref="CFixes.IsReadWithTheFiles"/>), which a running game cannot take back or bring in. A
/// rule asked from then on answers by the new state; a figure made once a day or once a month follows at its next
/// making.
/// </summary>
public sealed class CSetFixCommand : CCommand, IHostOrder
{
	private readonly string _fix;
	private readonly bool _on;

	public CSetFixCommand(string fix, bool on)
	{
		_fix = fix;
		_on = on;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Rules.Enhanced && CFixes.Named(_fix) is { } fix && !CFixes.IsReadWithTheFiles(fix);
	}

	public override void Execute(CGameState state)
	{
		state.Rules.Set(CFixes.Named(_fix)!.Value, _on);
	}
}
