using System.Collections.Generic;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// One country's line of a history the original keeps for graphs it never draws: under the country's tag, a figure for
/// each year it was taken - in <c>nation_size_statistics</c>, the provinces it owned as each year began. Kept for the save
/// and nothing else. See <c>docs/mechanics/statistics.md</c>, section 5.
/// </summary>
public sealed class CStatisticsSeries
{
	public CStatisticsSeries(string name)
	{
		Name = name;
	}

	/// <summary>The country's tag, <c>---</c> for the null country.</summary>
	public string Name { get; }

	/// <summary>The years, in the order taken.</summary>
	public List<int> Years { get; } = new();

	/// <summary>The figure of each year, beside it.</summary>
	public List<long> Values { get; } = new();
}
