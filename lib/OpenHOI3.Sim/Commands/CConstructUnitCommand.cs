using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The AI's unit order, the original's <c>CConstructUnitCommand</c> as the production script makes it: one line of
/// <c>size</c> units one after another, built at the country's own levels, the men of all of them taken at once. Refused
/// only for no brigades - not for want of men, technology or a type's cap, which the script asks for itself; a size of 0
/// is a line that takes no men and still delivers one unit. The player's windows post <see cref="COrderUnitCommand"/>,
/// which asks what the windows ask. See <c>docs/mechanics/ministers.md</c>, section 7.
/// </summary>
public sealed class CConstructUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly string[] _types;
	private readonly COrderUnitCommand _order;

	/// <param name="types">Each brigade's type, in the order the script appended them.</param>
	/// <param name="target">The number of the headquarters the line is for; 0 for none.</param>
	public CConstructUnitCommand(CCountryTag country, IEnumerable<string> types, int size, bool isReserve, int target = 0)
	{
		_country = country;
		_types = types.ToArray();
		_order = new COrderUnitCommand(country, _types.Select(type => (type, "")), size: size, isReserve: isReserve, target: target);
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is not null && _types.Length > 0 && _types.All(state.SubUnitTypes.ContainsKey);
	}

	public override void Execute(CGameState state)
	{
		_order.Execute(state);
	}
}
