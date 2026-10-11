using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the production screen's elements say in a tooltip, as the original's screen writes it.
/// See <c>docs/mechanics/productionscreen.md</c>, section 8. Its sliders' are
/// <see cref="CShareTooltips"/>.
/// </summary>
public static class CProductionTooltips
{
	private const string White = "§W";
	private const string Infrastructure = "infrastructure";
	private const string LocalInfrastructure = "local_infrastructure";
	private const string GlobalInfrastructure = "global_infrastructure";
	private const string NuclearProduction = "nuclear_production";

	/// <summary>Each resource's icon, label and figure, and the good each is the ledger of.</summary>
	private static readonly Dictionary<string, EGoods> Ledgers = new(StringComparer.OrdinalIgnoreCase)
	{
		["energy"] = EGoods.Energy, ["metal"] = EGoods.Metal, ["rm"] = EGoods.RareMaterials,
		["oil"] = EGoods.CrudeOil, ["supplies"] = EGoods.Supplies, ["money"] = EGoods.Money, ["fuel"] = EGoods.Fuel,
	};

	/// <summary>The elements that have a tooltip of their own by their name alone.</summary>
	public static IEnumerable<string> Elements()
	{
		foreach (string goods in Ledgers.Keys)
		{
			yield return goods + "_icon";
			yield return goods + "_label";
			yield return goods + "_value";
		}
		yield return "nuke_icon";
		yield return "nukes_label";
		yield return "nukes_value";
		yield return "theatres_calc_button";
		yield return "theatres_assign_button";
		yield return "convoy";
	}

	/// <summary>The tooltip of an element of <see cref="Elements"/>, or an empty one.</summary>
	public static CTooltip For(Func<string, string> localise, CCountry country, string element)
	{
		int cut = element.LastIndexOf('_');
		if (cut > 0 && Ledgers.TryGetValue(element[..cut], out EGoods goods)
			&& element[(cut + 1)..] is "icon" or "label" or "value")
		{
			return new CTooltip { Text = CTopBarTooltips.Ledger(localise, country, goods) };
		}
		return element.ToLowerInvariant() switch
		{
			"nuke_icon" or "nukes_label" or "nukes_value" => new CTooltip { Text = Nukes(localise, country) },
			"theatres_calc_button" => new CTooltip { Text = localise("THEATRE_RECALC_DESC") },
			"theatres_assign_button" => new CTooltip { Text = localise("THEATRE_ASSIGN_TROOPS_DESC") },
			"convoy" => new CTooltip { Text = localise("CONVOYS") },
			_ => CTooltip.Empty,
		};
	}

	/// <summary>The bombs a month brings, or that none can be made.</summary>
	public static string Nukes(Func<string, string> localise, CCountry country)
	{
		CFixedPoint amount = country.Technology[NuclearProduction];
		return amount.Raw == 0
			? localise("NO_NUKE_PRODUCTION")
			: CTextTemplate.Fill(localise("NUKE_PRODUCTION"), ("AMOUNT", CNumberText.Format(amount, 2)));
	}

	/// <summary>
	/// Whether the country may build escorts: whether any technology it has allows it. The
	/// button is disabled until one does.
	/// </summary>
	public static bool MayBuildEscorts(CCountry country)
	{
		return country.Technology[CTechnology.AllowEscorts].Raw > 0;
	}

	/// <summary>
	/// The escort button: its name, and where the button is disabled the technology needed -
	/// the first that allows escorts, in the order the technologies were read.
	/// </summary>
	public static string Escort(Func<string, string> localise, CGameState state, CCountry country)
	{
		string text = localise("LEDGER_ESCORTS");
		if (MayBuildEscorts(country))
		{
			return text;
		}
		CTechnology? first = state.Technologies.Values
			.FirstOrDefault(technology => technology.EffectPerLevel[CTechnology.AllowEscorts].Raw > 0);
		return first is null ? text : text + "\n" + localise("NEED") + " §Y" + localise(first.Name);
	}

	/// <summary>
	/// A building's button: its name and a line break. Where the button is disabled the original
	/// adds the technology that unlocks it; nothing disables one here.
	/// </summary>
	public static string Building(Func<string, string> localise, CBuilding building)
	{
		return localise(building.Key) + "\n";
	}

	/// <summary>
	/// A strategic resource's icon: its name, what the country has of its modifier, and a line
	/// for each province that gives the resource to the country, with how much of that
	/// province's infrastructure is standing.
	/// </summary>
	public static string StrategicResource(Func<string, string> localise, CGameState state, CCountry country, string key)
	{
		CModifier? copy = country.ModifierSources
			.Select(source => source.Modifier)
			.FirstOrDefault(modifier => modifier.Name.Equals(key, StringComparison.OrdinalIgnoreCase)
				&& state.FindStrategicResource(key) is { } resource && !ReferenceEquals(modifier, resource));
		string effect = copy is null ? "" : CModifierText.AllValues(localise, copy);

		var from = new StringBuilder();
		foreach (CProvince province in ProvincesGiving(state, country, key))
		{
			long share = InfrastructureShare(province);
			char colour = share < 330 ? CColouredText.Red : share < 990 ? CColouredText.Yellow : CColouredText.Green;
			from.Append("\n@").Append(province.Controller!.Value.Value).Append("  ").Append(province.Name).Append(" - ")
				.Append(CColouredText.In(colour)).Append(CNumberText.Format(new CFixedPoint(share * 100), 2)).Append('%').Append(White);
		}

		return CTextTemplate.Fill(localise("STRAT_DET_RES_TT"),
			("NAME", localise(key)), ("EFFECT", effect), ("FROM", from.ToString()));
	}

	/// <summary>
	/// The provinces whose resource reaches the country: those of the resource whose
	/// controller gives the country its resources, in the order of the provinces, for a country
	/// that holds any land.
	/// </summary>
	public static IEnumerable<CProvince> ProvincesGiving(CGameState state, CCountry country, string key)
	{
		if (!CManpower.Landowners(state).Contains(country.Tag.Value))
		{
			yield break;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Equals(key, StringComparison.OrdinalIgnoreCase)
				&& state.Country(province.Controller) is { } holder && CStrategicResources.GetsFrom(state, country, holder))
			{
				yield return province;
			}
		}
	}

	/// <summary>
	/// A province's infrastructure as a share of what it was built to, in thousandths, as the
	/// tooltip works it out: what stands is held between a hundredth and the whole, and a
	/// province built to nothing is -1.
	/// </summary>
	public static long InfrastructureShare(CProvince province)
	{
		long built = province.Modifier[Infrastructure].Raw;
		long standing = built * (CFixedPoint.Scale + province.Modifier[LocalInfrastructure].Raw
			+ province.Modifier[GlobalInfrastructure].Raw) / CFixedPoint.Scale;
		standing = Math.Clamp(standing, 10, CFixedPoint.Scale);
		return built == 0 ? -1 : standing * CFixedPoint.Scale / built;
	}

	/// <summary>
	/// A line of the build queue, by which of its elements is under the mouse: what its buttons
	/// do, the brigades it builds with their experience, or how fast it goes.
	/// </summary>
	public static CTooltip QueueLine(Func<string, string> localise, CConstruction construction, string element)
	{
		switch (element.ToLowerInvariant())
		{
			case "cancel":
				return new CTooltip
				{
					Text = localise("PEN_CANCEL_PRODU"),
					Delayed = localise("PEN_CANCEL_PRODU_DRO") + "\n\n" + localise("CANCEL_MULTI_CONSTRUCTIONS_DESC"),
				};
			case "max_prio":
				return new CTooltip { Text = localise("PEN_MAX_PRIO") };
			case "prio":
				return new CTooltip { Text = localise("PEN_PRIO") };
			case "non_prio":
				return new CTooltip { Text = localise("PEN_NON_PRIO") };
			case "max_non_prio":
				return new CTooltip { Text = localise("PEN_MAX_NON_PRIO") };
			case "counter_type" or "production_queue_division" or "production_queue_air" or "production_queue_naval"
				or "series_info" or "unit_target":
				return new CTooltip { Text = Brigades(localise, construction) };
			default:
				return new CTooltip { Text = Speed(localise, construction) };
		}
	}

	/// <summary>
	/// What a line builds: a counter and a name for each brigade, and the experience the unit
	/// has gathered. Nothing is upgraded through the queue here, so a line is never an upgrade.
	/// </summary>
	public static string Brigades(Func<string, string> localise, CConstruction construction)
	{
		if (construction.Brigades.Count == 0)
		{
			return "";
		}
		var text = new StringBuilder();
		for (int index = 0; index < construction.Brigades.Count; index++)
		{
			CBrigadeOrder brigade = construction.Brigades[index];
			text.Append(CCounterIcons.Mark).Append(brigade.Type.Key).Append(' ').Append(brigade.Name);
			if (index < construction.Brigades.Count - 1)
			{
				text.Append('\n');
			}
		}
		text.Append('\n').Append(CTextTemplate.Fill(localise("BRIGADE_MIL_CON_CUMULATED_EXP"),
			("EXP", CNumberText.Format(construction.AccumulatedExperience, 2))));
		return text.ToString();
	}

	/// <summary>How fast a line went the last time the queue was worked through.</summary>
	public static string Speed(Func<string, string> localise, CConstruction construction)
	{
		if (construction.Status >= CFixedPoint.One)
		{
			return localise("PEN_FULL_SPEED");
		}
		if (construction.Status.Raw > 0)
		{
			return CTextTemplate.Fill(localise("PEN_PARTIAL_PROGRESS"),
				("PERC", CNumberText.Format(new CFixedPoint(construction.Status.Raw * 100), 0)));
		}
		return localise("PEN_NO_PROGRESS");
	}
}
