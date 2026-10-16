using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What a country's AI makes of every other: its personality, the provinces it means to take and to hold, and whom it
/// fears, antagonises, courts, protects, takes as a rival or wants as a puppet - made again from nothing by
/// <see cref="CAiStrategies.Make"/>. The Lua's <c>CAIStrategy</c> reads it. See <c>docs/mechanics/aistrategy.md</c>.
/// </summary>
public sealed class CAiStrategy
{
	public EAiPersonality Personality { get; set; }

	/// <summary>It has been made at least once: the original's <c>initialized</c>.</summary>
	public bool IsMade { get; set; }

	/// <summary>The hour it was last made, which the ten days are counted from; long ago where never.</summary>
	public long MadeHour { get; set; } = long.MinValue / 2;

	public CStrategyList<int> ConquerProvinces { get; } = new();

	public CStrategyList<int> DefendProvinces { get; } = new();

	public CStrategyList<CCountryTag> Threat { get; } = new();

	public CStrategyList<CCountryTag> Antagonize { get; } = new();

	public CStrategyList<CCountryTag> Befriend { get; } = new();

	public CStrategyList<CCountryTag> Protect { get; } = new();

	public CStrategyList<CCountryTag> Rival { get; } = new();

	public CStrategyList<CCountryTag> Vassal { get; } = new();

	public bool IsMilitarist => Personality == EAiPersonality.Military;

	/// <summary>Every list emptied, as each making starts.</summary>
	public void Clear()
	{
		ConquerProvinces.Clear();
		DefendProvinces.Clear();
		Threat.Clear();
		Antagonize.Clear();
		Befriend.Clear();
		Protect.Clear();
		Rival.Clear();
		Vassal.Clear();
	}
}
