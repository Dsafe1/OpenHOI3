namespace OpenHOI3.Sim.Commands;

/// <summary>
/// What a province's underground is to do: 0 raise partisans, 1 spread, 2 build up strength. Valid for any province,
/// and kept on the province for whatever underground it has next. See <c>docs/mechanics/undergrounds.md</c>, section 6.
/// </summary>
public sealed class CChangeUndergroundMissionCommand : CCommand
{
	public CChangeUndergroundMissionCommand(int province, int action)
	{
		Province = province;
		Action = action;
	}

	public int Province { get; }

	public int Action { get; }

	public override bool MayExecute(CGameState state) => Province > 0 && Province < state.Provinces.Count;

	public override void Execute(CGameState state)
	{
		state.Province(Province).UndergroundAction = Action;
	}
}
