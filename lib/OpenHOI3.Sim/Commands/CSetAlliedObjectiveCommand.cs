using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The original's <c>set_allied_objective</c>, from the province view's allied-objectives panel: the requester asks the
/// country to take the province, or takes the asking back. Valid where the province is not below nought and both are
/// countries; nothing else is asked. See <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public sealed class CSetAlliedObjectiveCommand : CCommand
{
	public CSetAlliedObjectiveCommand(CCountryTag country, CCountryTag requester, int province, bool active)
	{
		Country = country;
		Requester = requester;
		Province = province;
		Active = active;
	}

	/// <summary>The ally asked.</summary>
	public CCountryTag Country { get; }

	public CCountryTag Requester { get; }

	public int Province { get; }

	/// <summary>Asked, or the asking taken back.</summary>
	public bool Active { get; }

	public override bool MayExecute(CGameState state) =>
		Province >= 0 && state.Country(Country) is not null && state.Country(Requester) is not null;

	public override void Execute(CGameState state)
	{
		state.AlliedObjectives.Set(Country, Requester, Province, Active);
	}
}
