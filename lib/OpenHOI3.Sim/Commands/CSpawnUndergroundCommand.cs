using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A province's underground spreads at once, where it may. See <c>docs/mechanics/undergrounds.md</c>, section 6.
/// </summary>
public sealed class CSpawnUndergroundCommand : CCommand
{
	public CSpawnUndergroundCommand(int province)
	{
		Province = province;
	}

	public int Province { get; }

	public override bool MayExecute(CGameState state)
	{
		return Province > 0 && Province < state.Provinces.Count && state.Province(Province).UndergroundOwner is { } owner
			&& CUndergrounds.MaySpread(state, owner, state.Province(Province));
	}

	public override void Execute(CGameState state)
	{
		CUndergrounds.SpreadFrom(state, state.Province(Province));
	}
}
