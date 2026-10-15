using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Partisans raised from a province's underground at once, where it may raise them. See
/// <c>docs/mechanics/undergrounds.md</c>, section 6.
/// </summary>
public sealed class CSpawnPartisanCommand : CCommand
{
	public CSpawnPartisanCommand(int province)
	{
		Province = province;
	}

	public int Province { get; }

	public override bool MayExecute(CGameState state)
	{
		return Province > 0 && Province < state.Provinces.Count && state.Province(Province).UndergroundOwner is { } owner
			&& CUndergrounds.MayRaisePartisans(state, owner, state.Province(Province));
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(state.Province(Province).UndergroundOwner) is { } owner)
		{
			CUndergrounds.RaiseFrom(state, owner, state.Province(Province));
		}
	}
}
