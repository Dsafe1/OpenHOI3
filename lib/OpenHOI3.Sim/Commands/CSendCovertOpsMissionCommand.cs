using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A covert operation sent into a province, from the province window: its cost taken off the actor's points about the
/// province's controller - with no floor and no test that they are there - and its effect run on the province with
/// FROM the actor. See <c>docs/mechanics/espionage.md</c>, section 11.
/// </summary>
public sealed class CSendCovertOpsMissionCommand : CCommand
{
	public CSendCovertOpsMissionCommand(CCountryTag actor, int province, string operation)
	{
		Actor = actor;
		Province = province;
		Operation = operation;
	}

	public CCountryTag Actor { get; }

	public int Province { get; }

	/// <summary>The operation's name in <c>common/covert_ops.txt</c>.</summary>
	public string Operation { get; }

	public override bool MayExecute(CGameState state) =>
		state.Country(Actor) is not null && Province > 0 && Province < state.Provinces.Count && Find(state) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Actor) is { } actor && Find(state) is { } operation && Province > 0 && Province < state.Provinces.Count)
		{
			CCovertOps.Send(state, actor, state.Province(Province), operation);
		}
	}

	private CCovertOperation? Find(CGameState state) => state.CovertOperations.Find(each => each.Name == Operation);
}
