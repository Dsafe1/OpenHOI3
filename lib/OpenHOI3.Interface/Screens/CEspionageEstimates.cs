using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the player's spies estimate of the chosen country: each figure moved at random within a spread that narrows
/// as the spies know more, on a generator of the screen's own seeded with the month and the player - so a month's
/// figures hold still - and "?" under one whole spy; then its research. See <c>docs/mechanics/espionagescreen.md</c>,
/// section 4.
/// </summary>
public sealed class CEspionageEstimates
{
	public const string Unknown = "?";

	/// <summary>The goods' boxes in the order the original fills them.</summary>
	public static readonly (string Element, EGoods Goods)[] GoodsBoxes =
	{
		("energy_number", EGoods.Energy), ("metal_number", EGoods.Metal), ("raremat_number", EGoods.RareMaterials),
		("oil_number", EGoods.CrudeOil), ("fuel_number", EGoods.Fuel), ("supplies_number", EGoods.Supplies),
		("money_number", EGoods.Money),
	};

	/// <summary>Above this a stock is written in thousands.</summary>
	private const long MostWritten = 99999000;

	/// <summary>The half thousandth added before a figure is cut to thousandths: the float 0.0005, widened.</summary>
	private const double HalfThousandth = (double)0.0005f;

	private CEspionageEstimates()
	{
	}

	/// <summary>Each box's text, by its element's name.</summary>
	public Dictionary<string, string> Boxes { get; } = new(StringComparer.Ordinal);

	public List<string> CurrentResearch { get; } = new();

	public List<string> TopTechnology { get; } = new();

	public List<string> WeakTechnology { get; } = new();

	/// <summary>The month of the year, 0 for January, and the player's number in the original's list, which counts its placeholder.</summary>
	public static int MonthKey(CGameState state, CCountry player) => state.Date.Month - 1 + player.Index + 1;

	public static CEspionageEstimates Make(Func<string, string> localise, CGameState state, CCountry player, CCountry chosen)
	{
		var made = new CEspionageEstimates();
		var random = new CRandom((uint)MonthKey(state, player));
		float factor = (float)(CCountryIntel.Factor(player, chosen) / 1000.0);
		// The console's debug spy shows the true figures.
		bool noIntel = !state.SpyCheat && CCountryIntel.HasNone(player, chosen);
		float uncertainty = state.SpyCheat ? 0f : Math.Clamp((float)(1.0 - factor + 0.1), 0.1f, 1.0f);
		float bonus = (float)(state.Defines["country", "SPY_INTEL_MISSION_BONUS"].Raw / 1000.0);
		float industry = (float)(player.Modifier["offmap_industry_intel"].Raw / 1000.0);

		CSpyPresence home = CGameState.Presence(chosen, chosen);
		long counter = Clamped(random, home.Level * home.Share(ESpyMission.CounterEspionage) / 1000, 0, CSpyPresence.MostLevel,
			uncertainty);
		made.Boxes["counter_intel"] = localise("SPYMISSION_COUNTER_ESPIONAGE_TITLE") + ": "
			+ (noIntel ? Unknown : CNumberText.Format(new CFixedPoint(counter), 0));

		float goodsSpread = Math.Clamp(uncertainty - industry, 0f, 1f) * (1f - bonus);
		foreach ((string element, EGoods goods) in GoodsBoxes)
		{
			made.Boxes[element] = noIntel ? Unknown : Stock(random, state, chosen, goods, goodsSpread);
		}
		made.Boxes["ic_number"] = noIntel ? "?/?/?" : Ic(chosen);
		long dissent = Clamped(random, chosen.Dissent.Raw, 0, 100000, uncertainty * (1f - bonus));
		made.Boxes["dissent_number"] = noIntel ? Unknown : CNumberText.Format(new CFixedPoint(dissent), 2);
		long unity = Clamped(random, chosen.NationalUnity.Raw, 0, 100000, uncertainty);
		made.Boxes["national_unity_number"] = noIntel ? Unknown : CNumberText.Format(new CFixedPoint(unity), 1);
		long influence = AtLeast(random, chosen.DiplomaticInfluence.Raw, 0, 100000, uncertainty);
		made.Boxes["diplomacy_number"] = noIntel ? Unknown : CNumberText.Format(new CFixedPoint(influence), 0);
		long manpower = AtLeast(random, chosen.Manpower.Raw, 0, 100000, uncertainty);
		made.Boxes["manpower_number"] = noIntel ? Unknown : CNumberText.Format(new CFixedPoint(manpower), 0);
		long leadership = AtLeast(random, chosen.TotalLeadership.Raw, 0, 100000, uncertainty);
		made.Boxes["leadership_number"] = noIntel ? Unknown : CNumberText.Trimmed(new CFixedPoint(leadership));
		long spies = AtLeast(random, chosen.FreeSpies.Raw, 0, 10000, uncertainty);
		made.Boxes["espionage_number"] = noIntel ? Unknown : (spies / 1000).ToString(CultureInfo.InvariantCulture);

		made.Research(localise, state, chosen, random, uncertainty, factor, industry);
		return made;
	}

	/// <summary>
	/// The IC, exact: what goes to waste, the provinces' IC before any bonus, and the IC it has - the first two red or
	/// green.
	/// </summary>
	public static string Ic(CCountry country)
	{
		int wasted = CProduction.Wasted(country);
		return CColouredText.In(wasted > 0 ? CColouredText.Red : CColouredText.Green) + wasted.ToString(CultureInfo.InvariantCulture)
			+ "/" + CColouredText.In(country.BaseIc > country.TotalIc ? CColouredText.Red : CColouredText.Green)
			+ country.BaseIc.ToString(CultureInfo.InvariantCulture) + "/" + country.TotalIc.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>A stock within half the spread of itself either way, floored at nought, in thousands with a K above 99999.</summary>
	private static string Stock(CRandom random, CGameState state, CCountry country, EGoods goods, float spread)
	{
		long stock = state.Stockpile(country)?[goods].Raw ?? 0;
		long value = AtLeast(random, stock, 0, stock, spread);
		return value <= MostWritten
			? CNumberText.Format(new CFixedPoint(value), 0)
			: CNumberText.Format(new CFixedPoint(value / 1000), 0) + "K";
	}

	/// <summary>Section 4's research lists, on the same generator after the figures.</summary>
	private void Research(Func<string, string> localise, CGameState state, CCountry chosen, CRandom random, float uncertainty,
		float factor, float industry)
	{
		float known = Math.Clamp(factor + industry, 0f, 1f);
		float spread = Math.Clamp(uncertainty - industry, 0f, 1f);

		int rows = (int)(CLeadership.ResearchSlots(chosen) * known + 0.5f);
		foreach (string name in chosen.ResearchQueue.Take(Math.Max(rows, 0)))
		{
			double progress = chosen.ResearchProgress.GetValueOrDefault(name).Raw / (double)CFixed15.Scale;
			long percent = (long)Math.Floor((progress + HalfThousandth) * 1000) * 100000 / 1000 / 1000;
			percent += (long)((Draw(random) - 0.5f) * spread * 99f);
			percent = Math.Clamp(percent, 0, 99);
			CurrentResearch.Add(localise(name) + " (" + percent.ToString(CultureInfo.InvariantCulture) + "%)");
		}

		// The categories by their level, the first of equals first; the top from the highest, the weak from the lowest.
		List<(string Name, long Level)> categories = state.TechnologyCategories
			.Select(category => (category.Name, chosen.Ability(category.Name).Raw))
			.OrderBy(entry => entry.Raw)
			.Select(entry => (entry.Name, entry.Raw))
			.ToList();
		int count = Math.Min((int)(known * 5f), categories.Count);
		long previous = 999999000;
		for (int i = 0; i < count; i++)
		{
			(string name, long level) = categories[categories.Count - 1 - i];
			long raw = AtLeast(random, level, 0, 5000, spread);
			TopTechnology.Add(Category(localise, name, Math.Min(previous, raw)));
			previous = raw;
		}
		previous = -99999000;
		for (int i = 0; i < count; i++)
		{
			(string name, long level) = categories[i];
			long raw = AtLeast(random, level, 0, 5000, spread);
			WeakTechnology.Add(Category(localise, name, Math.Max(previous, raw)));
			previous = raw;
		}
	}

	private static string Category(Func<string, string> localise, string name, long level)
	{
		return localise(name) + " (" + CNumberText.Trimmed(new CFixedPoint(level)) + ")";
	}

	/// <summary>The original's RandomPerturbAtLeast: the base moved by up to half the spread of the scale either way, floored.</summary>
	public static long AtLeast(CRandom random, long value, long floor, long scale, float spread)
	{
		long moved = value + Step(random, spread) * scale / 1000;
		return moved > floor ? moved : floor;
	}

	/// <summary>The original's RandomPerturbClamped: as <see cref="AtLeast"/>, the ceiling also the scale.</summary>
	public static long Clamped(CRandom random, long value, long floor, long ceiling, float spread)
	{
		long moved = value + Step(random, spread) * ceiling / 1000;
		return moved < floor ? floor : moved > ceiling ? ceiling : moved;
	}

	/// <summary>A draw over 2^31, in single precision.</summary>
	private static float Draw(CRandom random) => (float)((random.Next() & 0x7FFFFFFF) * Math.Pow(2, -31));

	/// <summary>The step in thousandths: floor(((r - 0.5) x spread + 0.0005) x 1000), each product in single precision.</summary>
	private static long Step(CRandom random, float spread)
	{
		float centred = (float)(Draw(random) - 0.5);
		float spreaded = (float)(centred * spread);
		float raised = (float)(spreaded + HalfThousandth);
		return (long)Math.Floor((float)(raised * 1000.0));
	}
}
