using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country keeping a design as one of its division templates, in place of what that
/// template was. See <c>docs/mechanics/builders.md</c>, section 5.
///
/// It is refused for a template the country has not got and for a unit type that does
/// not exist.
/// </summary>
public sealed class CSetTemplateCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _index;
	private readonly string[] _brigades;

	/// <param name="index">Which of the country's templates, counted from nothing.</param>
	/// <param name="brigades">The unit types of the design, by key.</param>
	public CSetTemplateCommand(CCountryTag country, int index, IEnumerable<string> brigades)
	{
		_country = country;
		_index = index;
		_brigades = brigades.ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || _index < 0 || _index >= country.Templates.Count)
		{
			return false;
		}
		return _brigades.All(state.SubUnitTypes.ContainsKey);
	}

	public override void Execute(CGameState state)
	{
		CDivisionTemplate template = state.Country(_country)!.Templates[_index];
		template.Brigades.Clear();
		template.Brigades.AddRange(_brigades);
	}
}
