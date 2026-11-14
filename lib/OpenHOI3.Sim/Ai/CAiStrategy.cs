using System.Collections.Generic;
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

	/// <summary>Whose land it wants to march through, by weight: kept as a save holds it, made by nothing here yet.</summary>
	public CStrategyList<CCountryTag> MilitaryAccess { get; } = new();

	/// <summary>
	/// The original's <c>consolidate</c> and <c>static</c>, which scripts set: kept as a save holds them and acted on by
	/// nothing here yet (<c>docs/mechanics/aistrategy.md</c>, section 12).
	/// </summary>
	public bool Consolidate { get; set; }

	public bool IsStatic { get; set; }

	/// <summary>
	/// The production shares the original's AI works out - its land, air and naval shares, its armour bias and the most
	/// brigades it means to have: kept as a save holds them, worked out by nothing here yet
	/// (<c>reversing/findings/FINDINGS-aistrategy.md</c>, section C).
	/// </summary>
	public int MaxSubunits { get; set; }

	public CFixedPoint LandShare { get; set; }

	public CFixedPoint AirShare { get; set; }

	public CFixedPoint NavalShare { get; set; }

	public CFixedPoint ArmorBias { get; set; }

	public bool IsMilitarist => Personality == EAiPersonality.Military;

	/// <summary>
	/// The country's theatres, which its making leaves alone: they are made by <see cref="Theatres.CTheatres"/>
	/// (<c>docs/mechanics/theatres.md</c>).
	/// </summary>
	public List<Theatres.CTheatre> Theatres { get; } = new();

	/// <summary>Its theatres are to be made again in its next hour - as every country's are as a game is made.</summary>
	public bool TheatresDirty { get; set; } = true;

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
		MilitaryAccess.Clear();
	}
}
