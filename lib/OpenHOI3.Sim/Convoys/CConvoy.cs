using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// A standing run between two harbours. It is not a unit and has no ships of its own to
/// see: two counts, of transports and of escorts, a route, and a list of the goods it
/// carries. See <c>docs/mechanics/convoys.md</c>, section 1.
/// </summary>
public sealed class CConvoy
{
	public required int Id { get; init; }

	public required CCountryTag Owner { get; init; }

	/// <summary>The province it loads in.</summary>
	public required int Start { get; init; }

	/// <summary>The province it unloads in.</summary>
	public required int End { get; init; }

	/// <summary>The hour it was made.</summary>
	public long StartHour { get; init; }

	/// <summary>
	/// The provinces it goes through, the start first and the end last. Empty where no
	/// way was found.
	/// </summary>
	public List<int> Path { get; } = new();

	/// <summary>Which goods it carries.</summary>
	public bool[] Carries { get; } = new bool[CGoodsPool.Count];

	/// <summary>
	/// It serves a trade between two countries (<c>docs/mechanics/trade.md</c>, section 5); a route
	/// cancelled takes it away, and the day's look at the convoys then removes the convoy.
	/// </summary>
	public bool IsTrade { get; set; }

	/// <summary>
	/// It serves a receiver's lend-lease from <see cref="LendLeaseFrom"/>: it carries nothing, and its
	/// efficiency is the share of what is sent that arrives (<c>docs/mechanics/aid.md</c>, section 3).
	/// </summary>
	public bool IsLendLease { get; init; }

	/// <summary>The giver a lend-lease convoy serves.</summary>
	public CCountryTag? LendLeaseFrom { get; init; }

	/// <summary>
	/// A lend-lease convoy's own efficiency, in thousandths: whole when made, lowered by its transports sunk and regained
	/// a little each day, and multiplied into what it delivers (<c>docs/mechanics/aid.md</c>, section 3). The original
	/// does not save it, so a loaded game's convoys are whole again (<c>docs/enhanced.md</c>, a whole game's replay).
	/// </summary>
	public long LendLeaseEfficiency { get; set; } = CFixedPoint.Scale;

	/// <summary>The transports on it.</summary>
	public int Transports { get; set; }

	/// <summary>The escorts with it.</summary>
	public int Escorts { get; set; }

	/// <summary>The transports it would have, as the sharing out last worked it out.</summary>
	public int TransportsWanted { get; set; }

	/// <summary>The escorts it would have, as the sharing out last worked it out.</summary>
	public int EscortsWanted { get; set; }

	/// <summary>What <see cref="LastAttackHour"/> is for a convoy nothing has attacked.</summary>
	public const long NeverAttacked = long.MinValue;

	/// <summary>The hour a raid last sank a ship of it.</summary>
	public long LastAttackHour { get; set; } = NeverAttacked;

	/// <summary>What it carried when it last sailed.</summary>
	public CGoodsPool Daily { get; } = new();

	/// <summary>What the land it loads in yields in a day.</summary>
	public CGoodsPool LoadingProduction { get; } = new();

	/// <summary>It carries supplies or fuel for the country's own units.</summary>
	public bool IsSupply => !IsLendLease && !IsTrade
		&& (Carries[(int)EGoods.Supplies] || Carries[(int)EGoods.Fuel]);

	/// <summary>It brings home what land across the sea yields.</summary>
	public bool IsResource => !IsSupply;

	public override string ToString()
	{
		return $"{Owner} {Start}->{End} {(IsSupply ? "supplies" : "resources")} {Transports}/{TransportsWanted}";
	}
}
