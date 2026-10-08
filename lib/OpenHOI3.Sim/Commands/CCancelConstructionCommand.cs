using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country taking a line out of its build queue. The men of every unit of it still to
/// come go back to the pool; the IC spent on it is gone. See
/// <c>docs/mechanics/buildqueue.md</c>, section 6.
/// </summary>
public sealed class CCancelConstructionCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _construction;

	public CCancelConstructionCommand(CCountryTag country, int construction)
	{
		_country = country;
		_construction = construction;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is not null;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CConstruction construction = Find(country)!;
		country.Manpower = CFixedPoint.Max(CFixedPoint.Zero, country.Manpower + construction.Manpower * construction.Size);
		country.Constructions.Remove(construction);
		CBuildQueue.UpdateCost(country);
	}

	private CConstruction? Find(CCountry country)
	{
		return country.Constructions.Find(construction => construction.Id == _construction);
	}
}
