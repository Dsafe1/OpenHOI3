using System;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A command that may carry the number of a unit made by the same AI pass, taken as a stand-in while the countries' runs go
/// side by side and given its real number once they are over (<see cref="Ai.CAiRun"/>).
/// </summary>
public interface INewUnitIds
{
	/// <summary>Every number it carries put through <paramref name="real"/>, which answers a real number as itself.</summary>
	void GiveIds(Func<int, int> real);
}
