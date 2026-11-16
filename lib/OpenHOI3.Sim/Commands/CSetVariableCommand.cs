using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country's variable set by a script's Lua - <c>CSetVariableCommand(tag, name, value)</c>, the original's command: the
/// variable of that name, matched without case as every variable is, made where it is missing and given the value. It
/// does nothing for a tag no country has. See <c>docs/mechanics/effects.md</c>, section 3, and <c>docs/mechanics/lua.md</c>,
/// section 4.4.
/// </summary>
public sealed class CSetVariableCommand : CCommand
{
	public CSetVariableCommand(CCountryTag country, string name, CFixedPoint value)
	{
		Country = country;
		Name = name;
		Value = value;
	}

	public CCountryTag Country { get; }

	public string Name { get; }

	public CFixedPoint Value { get; }

	public override bool MayExecute(CGameState state) => state.Country(Country) is not null;

	public override void Execute(CGameState state)
	{
		if (state.Country(Country) is { } country)
		{
			country.Variables[Name] = Value;
		}
	}
}
