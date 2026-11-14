using System.Linq;
using OpenHOI3.Sim.Supply;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the status of a selected unit says in a tooltip: where it is and is supplied from,
/// its attrition broken into its causes, its supply and fuel, its weight and width, the marks
/// of what holds for it, its speed and how far its headquarters are. See
/// <c>docs/mechanics/unitpanel.md</c>, section 10.
/// </summary>
public static class CUnitStatusTooltips
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// The status's element. <paramref name="shortcut"/> is the shortcut of the button that
	/// finds the unit on the map, and <paramref name="suppliedFrom"/> the text the element
	/// that names the supply's source shows.
	/// </summary>
	private static CFixedPoint Uses(CGameState state, CCountry owner, CUnit unit, EGoods goods) =>
		goods == EGoods.Fuel ? CUnitSupply.FuelConsumption(state, owner, unit) : CUnitSupply.SupplyConsumption(state, owner, unit);

	/// <summary>What the unit uses and got, and how many days of it lie where it draws for everyone standing there (unitpanel.md, section 10).</summary>
	private static CTooltip Supplied(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit, EGoods goods, string key, CFixedPoint received)
	{
		CProvince province = state.Province(CSupplyDraw.ProvinceOf(owner, unit));
		long used = state.Positions.Of(province.Id).Sum(there => state.Country(there.Owner) is { } country ? Uses(state, country, there, goods).Raw : 0);
		long days = used < 1 ? 0 : province.Pool[goods].Raw / used;
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise(key), ("FROM", province.Name), ("DAYS", days.ToString(CultureInfo.InvariantCulture)),
				("VAL", CNumberText.Format(new CFixedPoint(received.Raw * 100), 0)), ("CON", CNumberText.Format(Uses(state, owner, unit, goods), 2))),
		};
	}

	public static CTooltip For(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit, string element,
		string shortcut, string suppliedFrom)
	{
		switch (element)
		{
			case "unitattrition":
				return unit.SubUnits.Count == 0 || unit.Location <= 0
					? CTooltip.Empty
					: new CTooltip { Text = Attrition(localise, state, state.Province(unit.Location), unit.SubUnits[0]) };
			case "supply_status":
				return Supplied(localise, state, owner, unit, EGoods.Supplies, "UW_SUPPLY_STATUS2", unit.SuppliesReceived);
			case "fuel_status":
				return Supplied(localise, state, owner, unit, EGoods.Fuel, "UW_SUPPLY_FUEL2", unit.FuelReceived);
			case "unitlocation":
				return new CTooltip
				{
					Text = localise("UW_LOC_IRO"),
					DelayedExtra = shortcut.Length > 0 ? localise("SHORTCUT") + shortcut : "",
				};
			case "unitbase":
				return CMovementSpeed.IsLand(unit)
					? new CTooltip { Text = localise("UW_SUPPLIES_FROM"), Delayed = suppliedFrom }
					: new CTooltip { Text = localise("UW_BASED_FROM"), Delayed = BaseSupply(localise, state, unit) };
			case "unit_weight":
				return new CTooltip { Text = localise("TRANSPORT_WEIGHT") };
			case "unit_cw_strip":
				return new CTooltip { Text = Width(localise, CUnitPanelText.Width(unit)) };
			case "unitstatus_delay":
				// A strategic redeployment's wait is called that; any other an attack delay.
				return new CTooltip
				{
					Text = CUnitHeaderTooltips.AttackDelay(localise, state, unit,
						unit.Order == EUnitOrder.StrategicRedeployment ? "UW_STRATDEPLOYMENT_DELAY" : "UW_ATTACK_DELAY"),
				};
			case "unitstatus_nuke":
				return new CTooltip { Text = localise("UW_CARRYING_NUKE") };
			case "unitstatus_dugin":
				return new CTooltip
				{
					Text = CTextTemplate.Fill(localise("UW_DUGIN"),
						("CURR", CNumberText.Format(unit.DigIn, 0)),
						("CAP", CNumberText.Format(CDigIn.CapOf(state, owner), 0))),
				};
			case "unitstatus_combinedarms":
				return new CTooltip { Text = CombinedArms(localise, state, owner, unit) };
			case "unitstatus_moving":
				return unit.IsMoving && !unit.IsFighting
					? new CTooltip { Text = CUnitHeaderTooltips.Arrival(localise, state, owner, unit) }
					: CTooltip.Empty;
			case "speed":
				return new CTooltip { Text = Speed(localise, state, owner, unit) };
			case "unitstatus_distance":
				return new CTooltip { Text = Distance(localise, state, unit) };
			default:
				return CTooltip.Empty;
		}
	}

	/// <summary>
	/// <c>unitattrition</c>: a brigade's attrition in a province broken into its causes. Nothing for a brigade that is not
	/// land; in a friend's province away from the front, <c>IN_FRIENDLY_FOR_ATT</c> alone. Otherwise the front's or the enemy's
	/// line - the front's counting each cause at a tenth - then the province's own sources of attrition, what its
	/// infrastructure lacks, the weather where above nothing, the ground where above nothing under the terrain's name, the
	/// technology's cut as a percentage, and last the brigade's whole figure. The weather here is not held at nothing, as
	/// the figure's is.
	/// </summary>
	public static string Attrition(Func<string, string> localise, CGameState state, CProvince province, CSubUnit brigade)
	{
		if (brigade.Type.Branch != EUnitBranch.Land || brigade.Unit is not { } unit || state.Country(unit.Owner) is not { } owner)
		{
			return "";
		}
		var text = new StringBuilder();
		long scale = Scale;
		if (CAttrition.IsFriendsGround(state, province, unit.Owner))
		{
			if (!Sim.Theatres.CTheatreFigures.IsFrontProvince(state, province, false))
			{
				return localise("IN_FRIENDLY_FOR_ATT");
			}
			text.Append(localise("IN_FRONT_PROV_ONLY_10"));
			scale = Scale / 10;
		}
		else
		{
			text.Append(localise("IN_HOSTILE_FOR_ATT"));
		}
		text.Append('\n').Append(Modifiers.CModifierText.Contributors(localise, province.Modifier, province.ModifierSources,
			CAttrition.Modifier, scale));
		if (text.Length > 0 && text[^1] != '\n')
		{
			text.Append('\n');
		}
		long infrastructure = (Scale - CAttrition.InfrastructureShare(province)) * scale / Scale;
		if (infrastructure != 0)
		{
			text.Append(localise("ATTR_INFRA")).Append(": §R").Append(infrastructure > 0 ? "+" : "")
				.Append(Two(infrastructure)).Append("§!\n");
		}
		long weather = CAttrition.WeatherBeforeFloor(state, province, owner, unit) * scale / Scale;
		if (weather > 0)
		{
			text.Append(localise("ATTR_WEATHER")).Append(": §R+").Append(Two(weather)).Append("§!\n");
		}
		long ground = CAttrition.Ground(province, brigade) * scale / Scale;
		if (ground > 0 && province.Terrain is { } terrain)
		{
			text.Append(localise(terrain.Name)).Append(": §R+").Append(Two(ground)).Append("§!\n");
		}
		long technology = owner.Technology[CAttrition.Technology].Raw;
		if (technology != 0)
		{
			text.Append('\n').Append(localise("ATTR_TECH")).Append(": ").Append(technology < 0 ? "§G" : "§R+")
				.Append(Two(technology * 100)).Append("%§!\n");
		}
		long figure = CAttrition.Of(state, province, brigade);
		text.Append("\n§Y").Append(localise("TERRAIN_ATTRITION")).Append("§!").Append(figure > 0 ? "§R+" : "")
			.Append(Two(figure)).Append("%§!");
		return text.ToString();
	}

	private static string Two(long thousandths) => CNumberText.Format(new CFixedPoint(thousandths), 2);

	/// <summary>
	/// <c>unitbase</c>'s second half for a fleet or an air unit: <c>UW_BASE_NAVAL_SUPPLY_INFO</c> or
	/// <c>UW_BASE_AIR_SUPPLY_INFO</c> - the base's province, the share of its work it does, as a percentage to one place,
	/// how many ships or wings are based there and what it has room for, to two places. Nothing for a unit with no base.
	/// </summary>
	public static string BaseSupply(Func<string, string> localise, CGameState state, CUnit unit)
	{
		if (new CBaseCrowding(state).Describe(unit) is not { } supply)
		{
			return "";
		}
		bool isNavy = unit.Kind == EUnitKind.Navy;
		return CTextTemplate.Fill(localise(isNavy ? "UW_BASE_NAVAL_SUPPLY_INFO" : "UW_BASE_AIR_SUPPLY_INFO"),
			("NAME", state.Province(supply.Province).Name),
			("VAL", CNumberText.Format(new CFixedPoint(supply.Share * 100), 1)),
			(isNavy ? "SHIPS" : "PLANES", CNumberText.Format(new CFixedPoint(supply.Based), 0)),
			("DAILY", CNumberText.Format(new CFixedPoint(supply.Room), 2)));
	}

	/// <summary>A width's line: <c>UNIT_COMBAT_WIDTH: §Y1.0§W</c> and a line break.</summary>
	public static string Width(Func<string, string> localise, CFixedPoint width)
	{
		return localise("UNIT_COMBAT_WIDTH") + ": §Y" + CNumberText.Format(width, 1) + "§W\n";
	}

	/// <summary>
	/// The combined arms bonus and where it comes from: in all, the mix's own, what technology
	/// adds and what the leaders' traits add, the last two only where they add something.
	/// </summary>
	public static string CombinedArms(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				types.Add(subUnit.Type);
			}
		}
		long mix = CCombinedArms.BaseBonus(state, types).Raw;
		if (mix <= 0)
		{
			return "";
		}
		long technology = CCombinedArms.Bonus(state, owner, types).Raw - mix;
		long trait = CCommandReach.Effect(state, unit, ETraitEffect.CombinedArmsBonus);
		var text = new StringBuilder();
		text.Append(Percent(localise, "CA_BONUS_DETAILS_TOTAL", mix + technology + trait));
		text.Append('\n').Append(Percent(localise, "CA_BONUS_DETAILS_BASE", mix));
		if (technology > 0)
		{
			text.Append('\n').Append(Percent(localise, "CA_BONUS_DETAILS_TECH", technology));
		}
		if (trait > 0)
		{
			text.Append('\n').Append(Percent(localise, "CA_BONUS_DETAILS_TRAIT", trait));
		}
		return text.ToString();
	}

	private static string Percent(Func<string, string> localise, string key, long thousandths)
	{
		return CTextTemplate.Fill(localise(key), ("VAL", CNumberText.Format(new CFixedPoint(thousandths * 100), 2)));
	}

	/// <summary>
	/// <c>speed</c>: what the speed is, the speed the unit has where something scales it, each
	/// brigade's own, and how much of its speed it has, with the terms that make that up
	/// written one by one, as the original's tooltip works them out (<see cref="SpeedModifier"/>).
	/// </summary>
	public static string Speed(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		var text = new StringBuilder();
		text.Append(localise("UW_SPEED")).Append('\n');
		long modifier = CMovementSpeed.Modifier(state, owner, unit);
		if (modifier != Scale)
		{
			text.Append(" / ").Append(CBuilderText.ExactSpeed(CUnitPanelText.ShownSpeed(state, owner, unit))).Append(localise("KPH"));
		}
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			text.Append('\n').Append(CUnitHeaderTooltips.BrigadeName(subUnit)).Append(" - ")
				.Append(CBuilderText.ExactSpeed(subUnit.Type.Stats[CMovementSpeed.MaximumSpeed])).Append(' ').Append(localise("KPH"));
		}
		text.Append('\n');
		if (modifier != Scale)
		{
			text.Append(SpeedModifier(localise, modifier, CMovementSpeed.Terms(state, owner, unit)));
		}
		return text.ToString();
	}

	/// <summary>
	/// The original's breakdown of the speed's multiplier: <c>SPEED_MODIFIER</c> with the whole, then for each term not
	/// one, in its order, a line break, <c>- </c> and the term's key with it - each a percentage to two places.
	/// </summary>
	public static string SpeedModifier(Func<string, string> localise, long modifier, CSpeedTerms terms)
	{
		var text = new StringBuilder(CTextTemplate.Fill(localise("SPEED_MODIFIER"), ("PERC", Multiplier(modifier))));
		foreach ((string key, long term) in new[]
		{
			("SPEED_MODIFIER_COMBAT", terms.Combat), ("SPEED_MODIFIER_FUEL", terms.Fuel), ("SPEED_MODIFIER_TERRAIN", terms.Terrain),
			("SPEED_MODIFIER_TRAIT", terms.Trait), ("SPEED_MODIFIER_ADJACIENCY", terms.Adjacency),
			("SPEED_MODIFIER_WEATHER", terms.Weather), ("SPEED_MODIFIER_LOCAL_MOD", terms.LocalModifier),
			("SPEED_MODIFIER_MOTORIZED_BONUS", terms.MobileBonus), ("SPEED_MODIFIER_RETREATING", terms.Retreating),
		})
		{
			if (term != Scale)
			{
				text.Append("\n- ").Append(CTextTemplate.Fill(localise(key), ("PERC", Multiplier(term))));
			}
		}
		return text.ToString();
	}

	/// <summary>A multiplier as a percentage: green above one, red below, and exactly one yellow.</summary>
	public static string Multiplier(long thousandths)
	{
		string colour = thousandths > Scale ? "§G" : thousandths < Scale ? "§R" : "§Y";
		return colour + CNumberText.Format(new CFixedPoint(thousandths * 100), 2) + "%§!";
	}

	/// <summary>
	/// <c>unitstatus_distance</c>: that the unit is out of a headquarters' range, how far a
	/// headquarters reaches in full, and how far each headquarters above is from the unit below
	/// it, coloured by how much of its command gets through - yellow where none does, as the
	/// original has it.
	/// </summary>
	public static string Distance(Func<string, string> localise, CGameState state, CUnit unit)
	{
		var text = new StringBuilder();
		if (unit.Parent is not null && CCommandReach.IsOutOfRange(state, unit))
		{
			text.Append(localise("UW_DIS_OUT_OF_RANGE")).Append('\n');
		}
		int level = CUnitHeaderTooltips.LevelOf(unit);
		if (level <= 3)
		{
			text.Append(CTextTemplate.Fill(localise("UW_EFFECTIVE_RADIO_STRENGTH"),
				("HQ", CUnitHeaderTooltips.LevelName(localise, level)),
				("DISTANCE", CCommandReach.Radius(state, unit).ToString(CultureInfo.InvariantCulture)))).Append('\n');
		}
		CUnit lower = unit;
		for (CUnit? headquarters = unit.Parent; headquarters is not null; headquarters = headquarters.Parent)
		{
			long reach = CCommandReach.From(state, lower, headquarters);
			string colour = reach < 0 ? "§R" : reach < CCommandReach.Full ? "§Y" : "§G";
			long distance = CMapDistance.Between(state.Province(headquarters.Location), state.Province(lower.Location), state.MapWidth);
			text.Append(CTextTemplate.Fill(localise("UW_DIST_TO"),
				("LOCATION", state.Province(headquarters.Location).Name),
				("HQ", CUnitHeaderTooltips.LevelName(localise, CUnitHeaderTooltips.LevelOf(headquarters))),
				("NAME", headquarters.Name),
				("LOWER", lower.Name),
				("DISTANCE", colour + distance.ToString(CultureInfo.InvariantCulture)))).Append('\n');
			lower = headquarters;
		}
		return text.ToString();
	}
}
