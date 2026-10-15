using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// Ours, for trials: an underground finished in a province for a country as a line of the build queue would finish one,
/// the queue not holding buildings yet. Nothing is asked. See <c>docs/mechanics/undergrounds.md</c>, section 2.
/// </summary>
public sealed class CPlaceUndergroundCommand : CCommand
{
	public CPlaceUndergroundCommand(CCountryTag builder, int province)
	{
		Builder = builder;
		Province = province;
	}

	public CCountryTag Builder { get; }

	public int Province { get; }

	public override bool MayExecute(CGameState state) =>
		Province > 0 && Province < state.Provinces.Count && state.Country(Builder) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Builder) is { } builder)
		{
			CUndergrounds.Finish(state, state.Province(Province), builder);
		}
	}
}
