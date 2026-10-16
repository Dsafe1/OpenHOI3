using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CChangeLendLeaseDistributionCommand</c> as a script makes it: its giver, and the receivers and weights
/// <c>SetData</c> hands it, which become <see cref="CChangeLendLeaseDistributionCommand"/> when it is posted.
/// </summary>
public sealed class CLuaLendLeaseDistribution
{
	public CLuaLendLeaseDistribution(CCountryTag giver)
	{
		Giver = giver;
	}

	public CCountryTag Giver { get; }

	public List<(CCountryTag Receiver, long Weight)> Weights { get; } = new();

	public CCommand ToCommand() => new CChangeLendLeaseDistributionCommand(Giver, Weights);
}
