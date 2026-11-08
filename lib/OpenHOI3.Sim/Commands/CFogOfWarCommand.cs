using OpenHOI3.Sim.Intelligence;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Turns the fog of war off or on, as the original console's <c>debug fow</c> does - for the countries played alone, where the
/// original's is for every country (<c>docs/compatibility.md</c>, entry 21): with it off they know every province at 9 and
/// their fleets find an enemy's without a roll. See <c>docs/mechanics/intelligence.md</c>, section 3. *Ours* too: what
/// everyone knows is made again at once, where the original's flag waits for the day's rebuild.
/// </summary>
public sealed class CFogOfWarCommand : CCommand
{
	private readonly bool _on;

	public CFogOfWarCommand(bool on)
	{
		_on = on;
	}

	public override void Execute(CGameState state)
	{
		state.FogOfWar = _on;
		CProvinceIntel.Rebuild(state);
	}
}
