using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the reorganise window says: a row's strength and organisation bar, and a fleet's positioning penalty and its
/// ratio of capitals, screens and transports, as the original writes them. See <c>docs/mechanics/reorganise.md</c>,
/// section 2.
/// </summary>
public static class CReorgText
{
	public const string WindowName = "reorg_window";
	public const string LeftEntry = "reorg_entry";
	public const string RightEntry = "reorg_entry_right";
	public const string LeftList = "left_list";
	public const string RightList = "right_list";
	public const string LeftName = "unitname_1";
	public const string RightName = "unitname_2";
	public const string PositioningLeft = "positioning_left";
	public const string PositioningRight = "positioning_right";
	public const string RatioLeft = "ships_ratio_left";
	public const string RatioRight = "ships_ratio_right";

	/// <summary>The row's strength, as the unit panel's row writes it (<see cref="CUnitPanelText.BrigadeMen"/>).</summary>
	public static string Strength(CSubUnit subUnit)
	{
		return CUnitPanelText.BrigadeMen(subUnit);
	}

	/// <summary>The organisation bar: the brigade's organisation over its type's own <c>default_organisation</c>, not its ceiling.</summary>
	public static int OrganisationBar(CSubUnit subUnit)
	{
		long most = Math.Max(subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw, subUnit.Organisation.Raw);
		return most <= 0 ? 0 : (int)(100 * subUnit.Organisation.Raw / most);
	}

	/// <summary>The strength bar: the strength over its most, held to a whole.</summary>
	public static int StrengthBar(CSubUnit subUnit)
	{
		long most = Math.Max(subUnit.Stats[CSubUnitType.MaxStrength].Raw, subUnit.Strength.Raw);
		return most <= 0 ? 0 : (int)(100 * subUnit.Strength.Raw / most);
	}

	/// <summary>Whether the window shows a fleet's two lines: where either unit is one.</summary>
	public static bool ShowsFleetLines(CUnit left, CUnit right)
	{
		return left.Kind == EUnitKind.Navy || right.Kind == EUnitKind.Navy;
	}

	/// <summary>
	/// "Positioning Penalty:" and the fleet's own crowding penalty, green at nought and red otherwise, in thousandths with
	/// three decimals - without the hundredfold the unit panel gives it, as the original prints it.
	/// </summary>
	public static string Positioning(Func<string, string> localise, CGameState state, CUnit unit)
	{
		long penalty = CNavalBattle.StackingPenalty(state, new[] { unit });
		// Green below one thousandth: only at nought.
		string colour = penalty < 1 ? "§G" : "§R";
		// With the fix the hundredfold of a percentage, to one place, as a battle's window writes the same figure.
		string figure = state.Rules.Has(OpenHOI3.Sim.Rules.EFix.ShownFigures)
			? CNumberText.Format(new CFixedPoint(penalty * 100), 1)
			: CNumberText.Format(new CFixedPoint(penalty), 3);
		return CTextTemplate.Fill(localise("POSITIONING_PENALTY"), ("VAL", colour + figure + "%§!"));
	}

	/// <summary>
	/// "Units Ratio:" and capitals / screens / transports, all green: a transport first, then a capital, then anything but
	/// a submarine as a screen - a submarine counts nowhere.
	/// </summary>
	public static string Ratio(Func<string, string> localise, CUnit unit)
	{
		int capitals = 0, screens = 0, transports = 0;
		foreach (CSubUnit ship in unit.SubUnits)
		{
			if (ship.Type.Branch != EUnitBranch.Naval)
			{
				continue;
			}
			if (CShipCensus.IsTransport(ship))
			{
				transports++;
			}
			else if (CShipCensus.IsCapital(ship))
			{
				capitals++;
			}
			else if (!CShipCensus.IsSubmarine(ship))
			{
				screens++;
			}
		}
		string Green(int count) => "§G" + count.ToString(CultureInfo.InvariantCulture) + "§!";
		return CTextTemplate.Fill(localise("UNIT_RATIO"),
			("RAT", Green(capitals) + " / " + Green(screens) + " / " + Green(transports)));
	}
}
