using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the panel of a selected unit says: the figures, the texts and the frames of its
/// rows, each as the original works it out. See <c>docs/mechanics/unitpanel.md</c>.
///
/// An icon's frames are counted from one, as the original counts them.
/// </summary>
public static class CUnitPanelText
{
	public const string NoLeaderKey = "NO_LEADER";
	public const string SuppliedFromKey = "SUPPLIED_FROM";
	public const string SpeedUnitKey = "KPH";
	public const string MoveOrderKey = "MOVE_ORDER_D";
	public const string NoOrderKey = "NO_ORDERS";
	public const string RebaseKey = "ORDER_REBASE";
	public const string AirRebaseKey = "ORDER_AIR_REBASE_SHORT_DESC";
	public const string LandCountKey = "LAND_COUNT";
	public const string NavalCountKey = "NAVAL_COUNT";
	public const string AirCountKey = "AIR_COUNT";

	/// <summary>The header's activity strip: nothing, and what it shows for a unit on the march and for one that cannot act.</summary>
	public const int ActivityNone = 1;
	public const int ActivityRetreating = 2;
	public const int ActivityDefending = 3;
	public const int ActivityAttacking = 4;
	public const int ActivityMoving = 5;
	public const int ActivityUnfit = 6;

	/// <summary>What the order line says of a unit in a battle or falling back from one.</summary>
	public const string AttackingKey = "ATTACKING_TO_OI";
	public const string DefendingKey = "DEFENDING_OI";
	public const string RetreatingKey = "RETREAT_TO_OI";

	/// <summary>What a subordinate's level is called in the count over the list of them, by the kind of unit.</summary>
	private static readonly (EUnitKind Kind, string Key)[] LevelKeys =
	{
		(EUnitKind.ArmyGroup, "NUM_GROUPS"), (EUnitKind.Army, "NUM_ARMIES"),
		(EUnitKind.Corps, "NUM_CORPS"), (EUnitKind.Division, "NUM_DIVISONS"),
	};

	private const long Scale = CFixedPoint.Scale;

	// ---- the header

	/// <summary>
	/// The frame of the activity strip, the first of these that holds: in a battle, the
	/// red arrow for an attacker and the star for a defender; falling back, the white
	/// arrow; held by an attack delay, the cross; on the march, the green arrow; with
	/// nothing to act with, the cross; and otherwise nothing.
	/// </summary>
	public static int ActivityFrame(CUnit unit)
	{
		if (unit.IsFighting)
		{
			return IsAttacking(unit) ? ActivityAttacking : ActivityDefending;
		}
		if (unit.IsRetreating)
		{
			return ActivityRetreating;
		}
		if (unit.AttackDelay > CFixedPoint.Zero)
		{
			return ActivityUnfit;
		}
		if (unit.IsMoving)
		{
			return ActivityMoving;
		}
		return unit.SubUnits.Count == 0 ? ActivityUnfit : ActivityNone;
	}

	/// <summary>
	/// The three marks of the status line that a battle is behind: a mix of brigades
	/// that counts as combined arms, being dug in, and an attack delay still running.
	/// </summary>
	public static bool HasCombinedArms(CGameState state, CUnit unit)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				types.Add(subUnit.Type);
			}
		}
		// The mark goes by the mix alone, whatever the country's technology adds to it.
		return CCombinedArms.BaseBonus(state, types) > CFixedPoint.Zero;
	}

	public static bool IsDugIn(CUnit unit)
	{
		return unit.DigIn > CFixedPoint.Zero;
	}

	public static bool IsDelayed(CUnit unit)
	{
		return unit.AttackDelay > CFixedPoint.Zero;
	}

	/// <summary>The frame of the radio icon: 2 for a unit out of its chain of command's radio range, else 1.</summary>
	public static int DistanceFrame(CGameState state, CUnit unit)
	{
		return CCommandReach.IsOutOfRange(state, unit) ? 2 : 1;
	}

	/// <summary>Whether a unit is on the attacking side of one of its battles.</summary>
	public static bool IsAttacking(CUnit unit)
	{
		foreach (CCombat combat in unit.Combats)
		{
			if (combat.Attacker.Units.Contains(unit))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A land unit's men: a hundred to each point of each brigade's strength, each brigade cut to a whole man.</summary>
	public static long Men(CUnit unit)
	{
		long men = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			men += subUnit.Strength.Raw * 100 / Scale;
		}
		return men;
	}

	/// <summary>
	/// The header's strength: a land unit's men, whole under a thousand and with a comma
	/// before the last three digits above; a fleet's or an air unit's count of its own ships
	/// or wings, plain - "3" - with no colour and no separator (<c>unitpanel.md</c>, section 3).
	/// </summary>
	public static string Strength(CUnit unit)
	{
		if (!CMovementSpeed.IsLand(unit))
		{
			return unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture);
		}
		long men = Men(unit);
		if (men < 1000)
		{
			return men.ToString(CultureInfo.InvariantCulture);
		}
		return (men / 1000).ToString(CultureInfo.InvariantCulture) + ","
			+ (men % 1000).ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');
	}

	/// <summary>
	/// The leader's line: his name, in red where he has not the rank for the unit
	/// (<see cref="CLeaders.HasRankFor"/>), and <c>NO_LEADER</c> with none.
	/// </summary>
	public static string Leader(CGameState state, CUnit unit, Func<string, string> localise)
	{
		if (unit.Leader is not { } leader)
		{
			return localise(NoLeaderKey);
		}
		return CLeaders.HasRankFor(state, unit) ? leader.Name : "§R" + leader.Name;
	}

	/// <summary>A row of the units under one: its name, and its leader's in brackets where it has one.</summary>
	public static string ChildName(CUnit unit)
	{
		return unit.Leader is { } leader ? $"{unit.Name} ({leader.Name})" : unit.Name;
	}

	/// <summary>Where a leader's portrait is, under the install: a file named for his picture.</summary>
	public const string PortraitFolder = "gfx/pictures/portraits/";

	/// <summary>The game path of the unit's leader's portrait, or null with no leader.</summary>
	public static string? LeaderPortrait(CUnit unit)
	{
		return unit.Leader is { } leader ? Portrait(leader) : null;
	}

	/// <summary>The game path of a leader's portrait.</summary>
	public static string Portrait(CLeader leader)
	{
		return PortraitFolder + leader.Picture + ".tga";
	}

	/// <summary>
	/// <c>leader_types</c>' frame, counted from one: his kind and one more - a general's
	/// 1, an admiral's 2, an air marshal's 3. A unit with none shows a general's, the
	/// original's empty leader being of the first kind.
	/// </summary>
	public static int LeaderTypeFrame(CUnit unit)
	{
		return (int)(unit.Leader?.Type ?? ELeaderType.Land) + 1;
	}

	/// <summary>What the original trims off both ends of a name typed: line feed, tab, carriage return and space [exe].</summary>
	private static readonly char[] NameTrimmed = { '\n', '\t', '\r', ' ' };

	/// <summary>
	/// The rename the unit panel's name box sends as its header is taken down - by a change of selection or the panel made
	/// again - or none: the text trimmed, and sent only where it is not empty and not the unit's name byte for byte, case
	/// and all (<c>reversing/findings/FINDINGS-unitrename.md</c>, section 3.2).
	/// </summary>
	public static Sim.Commands.CRenameUnitCommand? Rename(CCountryTag owner, CUnit unit, string typed)
	{
		string name = typed.Trim(NameTrimmed);
		return name.Length == 0 || string.Equals(name, unit.Name, StringComparison.Ordinal)
			? null
			: new Sim.Commands.CRenameUnitCommand(owner, unit.Id, name);
	}

	/// <summary>
	/// Whether the button that opens the window a leader is chosen in may be pressed. Not
	/// for a unit falling back, one in a battle, a fleet at sea, or one standing where an
	/// enemy of the player holds the province; the original also refuses a land unit
	/// aboard a ship or an aircraft, which nothing carries here. **Nothing asks whose unit
	/// it is**, and the original's refusal of an expeditionary force is undone by its own
	/// last test. See <c>docs/mechanics/unitpanel.md</c>, section 3.
	/// </summary>
	public static bool MayChangeLeader(CGameState state, CUnit unit, CCountryTag? player)
	{
		if (unit.Lender is not null || unit.IsRetreating || unit.Combats.Count >= 1)
		{
			return false;
		}
		CProvince province = state.Province(unit.Location);
		if (unit.Kind == EUnitKind.Navy && province.IsSea)
		{
			return false;
		}
		return !CMovementRules.IsEnemy(state, province.Controller, player);
	}

	/// <summary>
	/// The most organisation a unit's bar is drawn against: its brigades' own figures
	/// averaged, more or less by the country's modifier for its branch. Officers have no
	/// part in this one.
	/// </summary>
	public static CFixedPoint MaxOrganisation(CCountry owner, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw;
		}
		long average = sum / unit.SubUnits.Count;
		string branch = unit.Kind switch
		{
			EUnitKind.Navy => COrganisation.NavalOrganisation,
			EUnitKind.Air => COrganisation.AirOrganisation,
			_ => COrganisation.LandOrganisation,
		};
		return new CFixedPoint(average * (Scale + owner.Modifier[branch].Raw) / Scale);
	}

	/// <summary>The header's organisation bar, in whole hundredths: the brigades' average over the most, never past the full bar.</summary>
	public static int OrganisationBar(CCountry owner, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Organisation.Raw;
		}
		return Share(sum / unit.SubUnits.Count, MaxOrganisation(owner, unit).Raw);
	}

	/// <summary>The header's strength bar: the brigades' strength over what their types are made with.</summary>
	public static int StrengthBar(CUnit unit)
	{
		long strength = 0;
		long most = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
			most += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		}
		return Share(strength, most);
	}

	/// <summary>A part of a whole as a bar's hundredths, cut, with the whole raised to the part where it is the smaller.</summary>
	public static int Share(long part, long whole)
	{
		if (part <= 0)
		{
			return 0;
		}
		if (whole < part)
		{
			whole = part;
		}
		return (int)(part * 100 / whole);
	}

	// ---- the status

	/// <summary>Where it is: the name of its province.</summary>
	public static string Location(CGameState state, CUnit unit)
	{
		return unit.Location > 0 && unit.Location < state.Provinces.Count ? state.Province(unit.Location).Name : "";
	}

	/// <summary>
	/// What its province wears it down by: its first brigade's attrition there, as the original asks it of the first, whole -
	/// the figure is a percentage as it stands, 1.000 reading <c>1%</c> - and <c>?%</c> for a unit with no brigades
	/// (upkeep.md, section 3a).
	/// </summary>
	public static string Attrition(CGameState state, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return "?%";
		}
		long figure = unit.Location > 0 && unit.Location < state.Provinces.Count
			? CAttrition.Of(state, state.Province(unit.Location), unit.SubUnits[0])
			: 0;
		return (figure / CFixedPoint.Scale).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
	}

	/// <summary>
	/// Where a land unit's supplies come from: its country's capital, which is where
	/// every province is supplied from until there is a supply network - or question
	/// marks where somebody who is no friend holds it.
	/// </summary>
	public static string SuppliedFrom(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		string where = " ???";
		if (!CMovementSpeed.IsLand(unit))
		{
			// A fleet and an air unit: the base, and how far from it the unit reaches.
			if (unit.Base > 0 && unit.Base < state.Provinces.Count)
			{
				where = " " + state.Province(unit.Base).Name + "("
					+ (CUnitOrders.RangeOf(unit).Raw / Scale).ToString(CultureInfo.InvariantCulture) + "km)";
			}
			return localise(SuppliedFromKey) + where;
		}
		if (owner.Capital > 0 && owner.Capital < state.Provinces.Count)
		{
			CProvince capital = state.Province(owner.Capital);
			if (state.Country(capital.Controller) is { } holder && CCountryLists.IsFriend(state, holder, owner.Tag, withAccess: true))
			{
				where = " " + capital.Name;
			}
		}
		return localise(SuppliedFromKey) + where;
	}

	/// <summary>
	/// The speed the panel shows: the unit's top speed times everything that scales it,
	/// in the figure the unit files are written in - twenty times what an hour of the
	/// march adds, in the base game.
	/// </summary>
	public static CFixedPoint ShownSpeed(CGameState state, CCountry owner, CUnit unit)
	{
		return new CFixedPoint(CMovementSpeed.SlowestBrigade(unit).Raw * CMovementSpeed.Modifier(state, owner, unit) / Scale);
	}

	/// <summary>That speed as text, with the unit it is measured in straight after it.</summary>
	public static string Speed(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		return CBuilderText.ExactSpeed(ShownSpeed(state, owner, unit)) + localise(SpeedUnitKey);
	}

	/// <summary>A unit's width in a battle: its brigades' added up.</summary>
	public static CFixedPoint Width(CUnit unit)
	{
		return Sum(unit, CSubUnitType.CombatWidth);
	}

	/// <summary>The frame of the width strip: one for under 2, and one more for each 2 up to 8 and beyond.</summary>
	public static int WidthFrame(CFixedPoint width)
	{
		long raw = width.Raw;
		return raw < 2 * Scale ? 1 : raw < 4 * Scale ? 2 : raw < 6 * Scale ? 3 : raw < 8 * Scale ? 4 : 5;
	}

	/// <summary>What a land unit weighs aboard a ship, whole; nothing for the others.</summary>
	public static string Weight(CUnit unit)
	{
		return CMovementSpeed.IsLand(unit)
			? (Sum(unit, CSubUnitType.TransportWeight).Raw / Scale).ToString(CultureInfo.InvariantCulture)
			: "";
	}

	/// <summary>
	/// The supply bar or the fuel bar, in hundredths: what lies in the province the unit
	/// draws on against what the units drawing there ask for in the days a province
	/// holds supplies for. Full where they ask for nothing, as in a capital.
	/// </summary>
	public static int SupplyBar(CGameState state, CCountry owner, CUnit unit, EGoods goods)
	{
		CProvince province = state.Province(CSupplyDraw.ProvinceOf(owner, unit));
		long wanted = CSupplyDay.DaysHeld(state) * province.SupplyNeed[goods].Raw;
		if (wanted <= 0)
		{
			return 100;
		}
		return (int)Math.Clamp(province.Pool[goods].Raw * 100 / wanted, 0, 100);
	}

	// ---- the order

	/// <summary>
	/// What the paradrop and the rebase to carrier put in their brackets, in the original's order: returning, resting in
	/// its base's province, flying in no battle and no bombing, and fighting.
	/// </summary>
	private static string AirDoing(CUnit unit)
	{
		return unit.Mission.ReturnToBase ? "STRAT_BOMB_RETURNING"
			: unit.Location == unit.Base ? "STRAT_BOMB_WAITING"
			: !unit.IsFighting && unit.Bombing is null ? "PARA_FLYING"
			: "CAG_FIGHTING";
	}

	/// <summary>
	/// What the unit is doing: falling back to a province, attacking one, defending, or
	/// on the march to the province it will be in next - not the one at the end of its
	/// way. The original has more to say of a unit short of men, fuel or supplies; none
	/// of that is built, and what it calls a unit with nothing to do was not read.
	/// </summary>
	public static string Order(Func<string, string> localise, CGameState state, CUnit unit)
	{
		// An order says for itself what it is, and names the province it is for.
		if (unit.Order != EUnitOrder.None && unit.OrderProvince > 0 && unit.OrderProvince < state.Provinces.Count)
		{
			string where = state.Province(unit.OrderProvince).Name;
			switch (unit.Order)
			{
				case EUnitOrder.Move:
					return CTextTemplate.Fill(localise(MoveOrderKey), ("WHERE", where));
				// The land orders say themselves, a redeployment's UNIT being the province (landorders.md, sections 7 and 8).
				case EUnitOrder.SupportAttack:
					return CTextTemplate.Fill(localise("SUPPORT_ATTACK_D"), ("WHERE", where));
				case EUnitOrder.StrategicRedeployment:
					return CTextTemplate.Fill(localise("STRAT_REDEP_D"), ("UNIT", where));
				case EUnitOrder.Rebase:
					return localise(RebaseKey) + " :" + where;
				case EUnitOrder.RebaseAir:
					return CTextTemplate.Fill(localise(AirRebaseKey), ("PROV", where));
				case EUnitOrder.Patrol:
					return CTextTemplate.Fill(localise("ORDER_PATROL_S"),
						("ACTION", localise(unit.Mission.LiveTarget is null ? "PATROL_SEARCH" : "PATROL_SHADOW")));
				case EUnitOrder.Intercept:
					return CTextTemplate.Fill(localise("ORDER_INTERCEPT_S"),
						("STATE", localise(unit.Mission.LiveTarget is not null ? "INTERCEPT_ATTACK"
							: unit.Mission.ReturnToBase ? "INTERCEPT_RETURN" : "INTERCEPT_WAITING")));
				case EUnitOrder.ConvoyEscort:
					if (Sim.Missions.CConvoyEscort.Find(state, unit.Mission.Convoy) is { } convoy)
					{
						return CTextTemplate.Fill(localise("ORDER_CONVOY_ESCORT_S"),
							("START", state.Province(convoy.Start).Name), ("END", state.Province(convoy.End).Name));
					}
					return localise(COrdersText.NameKey(unit.Order)) + " :" + where;
				case EUnitOrder.Reserve:
				case EUnitOrder.Sortie:
				case EUnitOrder.ConvoyRaid:
				case EUnitOrder.Transport:
				case EUnitOrder.Invasion:
					return localise(COrdersText.NameKey(unit.Order)) + " :" + where;
				// The intercept's is the original's; what air superiority puts in the brackets
				// was not read, and it is given the intercept's three.
				case EUnitOrder.AirSuperiority:
				case EUnitOrder.AirIntercept:
					return CTextTemplate.Fill(
						localise(unit.Order == EUnitOrder.AirIntercept ? "INTERCEPT_SHORT_DESC" : "AIRSUP_SHORT_DESC"),
						("TYPE", localise(unit.Mission.ReturnToBase ? "STRAT_BOMB_RETURNING"
							: unit.Mission.LiveTarget is not null ? "INTERCEPT_ATTACK" : "INTERCEPT_WAITING")));
				// Carrying by air and the carrier (transport.md, section 13.6); carrier protection's is ours.
				case EUnitOrder.Paradrop:
					return CTextTemplate.Fill(localise("PARADROP_MISSION_SHORT_DESC"),
						("TYPE", localise(!Sim.Missions.CAirMissions.IsFitToFly(state, unit) ? "STRAT_BOMB_WAITING"
							: AirDoing(unit))));
				case EUnitOrder.Nuke:
					return CTextTemplate.Fill(localise("NUKE_MISSION_SHORT_DESC"), ("TYPE", localise(AirDoing(unit))));
				case EUnitOrder.RebaseCarrier:
					return CTextTemplate.Fill(localise("REBASE_TO_CAG_MISSION_SHORT_DESC"), ("TYPE", localise(AirDoing(unit))));
				case EUnitOrder.CarrierProtection:
					return CTextTemplate.Fill(localise("CAG_DUTY_SHORT_DESC"), ("TYPE", localise(AirDoing(unit))));
				case EUnitOrder.AirSupply:
					return CTextTemplate.Fill(localise("TRANS_SUP_MISSION_SHORT_DESC"),
						("TYPE", localise(unit.Mission.ReturnToBase ? "TS_RETURNING"
							: Sim.Missions.CAirMissions.IsAtOwnBase(unit) && !unit.IsMoving ? "TS_WAITING" : "TS_SUPPLYING")));
				// The raid on convoys from the air, read (navalmissions.md, section 8.1): returning; waiting at its base while
				// unfit to fly or with nowhere to fly at; otherwise attacking the province it flies at.
				case EUnitOrder.AirConvoyRaid:
				{
					int strike = unit.Mission.Destination;
					bool grounded = unit.Location == unit.Base && !Sim.Missions.CAirMissions.IsFitToFly(state, unit);
					string doing = unit.Mission.ReturnToBase ? localise("STRAT_BOMB_RETURNING")
						: grounded || strike <= 0 || strike >= state.Provinces.Count ? localise("STRAT_BOMB_WAITING")
						: CTextTemplate.Fill(localise("STRAT_BOMB_ATTACKING"), ("PROV", state.Province(strike).Name));
					return CTextTemplate.Fill(localise("AIR_CONVOY_SHORT_DESC"), ("UNIT", doing));
				}
				// What a bombing mission puts in the brackets was not read either: these
				// are the three texts the original has for it, by what the unit is doing.
				case EUnitOrder.StrategicBombing:
				case EUnitOrder.LogisticalStrike:
				case EUnitOrder.RunwayCratering:
				case EUnitOrder.InstallationStrike:
				case EUnitOrder.GroundAttack:
				case EUnitOrder.Interdiction:
				case EUnitOrder.PortStrike:
				case EUnitOrder.NavalStrike:
				{
					// Over a province it is bombing, a unit is attacking that province: some of
					// these missions forget where they were going once the bombing has it.
					int strike = unit.Bombing?.Province ?? unit.Mission.Destination;
					string doing = unit.Mission.ReturnToBase ? localise("STRAT_BOMB_RETURNING")
						: strike > 0 && strike < state.Provinces.Count
							? CTextTemplate.Fill(localise("STRAT_BOMB_ATTACKING"), ("PROV", state.Province(strike).Name))
							: localise("STRAT_BOMB_WAITING");
					return CTextTemplate.Fill(localise(COrdersText.NameKey(unit.Order) + "_SHORT_DESC"), ("TYPE", doing));
				}
			}
		}
		if (unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air)
		{
			if (unit.IsRetreating && unit.IsMoving)
			{
				return CTextTemplate.Fill(localise(RetreatingKey), ("WHERE", state.Province(unit.Path[0]).Name));
			}
			if (unit.IsFighting)
			{
				// With a path it is the province ahead that is being attacked; without, ours:
				// the battle's own.
				if (unit.IsMoving)
				{
					return CTextTemplate.Fill(localise(AttackingKey), ("WHERE", state.Province(unit.Path[0]).Name));
				}
				foreach (CCombat combat in unit.Combats)
				{
					if (combat.Attacker.Units.Contains(unit))
					{
						return CTextTemplate.Fill(localise(AttackingKey), ("WHERE", state.Province(combat.Province).Name));
					}
				}
				return localise(DefendingKey);
			}
		}
		// Ours for a fleet sent straight somewhere, which has no order: as a land unit.
		if (unit.IsMoving)
		{
			return CTextTemplate.Fill(localise(MoveOrderKey), ("WHERE", state.Province(unit.Path[0]).Name));
		}
		return localise(NoOrderKey);
	}

	// ---- the brigades

	/// <summary>
	/// The heading over a unit's brigades: how many a land unit has; a fleet's capital
	/// ships, screens and others; an air unit's fighters and bombers. Which ship counts
	/// as which was not read - ours: a capital ship is one its type calls <c>capital</c>,
	/// the others are submarines and transports, and the rest are screens; a bomber is
	/// a wing its type calls <c>is_bomber</c>.
	/// </summary>
	public static string BrigadesHeader(Func<string, string> localise, CUnit unit)
	{
		string Count(Func<CSubUnit, bool> which)
		{
			int count = 0;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				count += which(subUnit) ? 1 : 0;
			}
			return count.ToString(CultureInfo.InvariantCulture);
		}

		switch (unit.Kind)
		{
			case EUnitKind.Navy:
				return CTextTemplate.Fill(localise(NavalCountKey),
					("CAPITAL", Count(IsCapital)),
					("ESCORTS", Count(ship => !IsCapital(ship) && !IsOtherShip(ship))),
					("OTHER", Count(ship => !IsCapital(ship) && IsOtherShip(ship))));
			case EUnitKind.Air:
				return CTextTemplate.Fill(localise(AirCountKey),
					("FIGHTER", Count(wing => !wing.Type.Flag("is_bomber"))),
					("BOMBER", Count(wing => wing.Type.Flag("is_bomber"))));
			default:
				return CTextTemplate.Fill(localise(LandCountKey), ("NUM", unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture)));
		}
	}

	private static bool IsCapital(CSubUnit ship)
	{
		return ship.Type.Flag("capital");
	}

	private static bool IsOtherShip(CSubUnit ship)
	{
		return ship.Type.Flag("is_sub") || ship.Stats["transport_capability"] > CFixedPoint.Zero;
	}

	/// <summary>The sprite of a brigade's picture, or an empty string.</summary>
	public static string BrigadeSprite(CSubUnit subUnit)
	{
		return CCounterText.ListSprite + subUnit.Type.Key;
	}

	/// <summary>
	/// Which model of its type a brigade is, as a Roman numeral: the model of its owner's nearest the levels it carries -
	/// not its builder's, as the summary's is - and I for none.
	/// </summary>
	public static string BrigadeLevel(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		return CRomanNumerals.Format(Math.Max(CHistoricalModels.OfBrigade(state, owner, subUnit), 0) + 1);
	}

	/// <summary>
	/// A brigade's strength as the original's rows write it: a brigade's men, strength times a hundred cut to a whole man;
	/// a ship's or a wing's strength as a whole percentage.
	/// </summary>
	public static string BrigadeMen(CSubUnit subUnit)
	{
		return subUnit.Type.Branch == EUnitBranch.Land
			? CBuilderText.Men(subUnit.Strength)
			: CNumberText.Format(subUnit.Strength, 0) + "%";
	}

	/// <summary>
	/// The frame of a brigade's stars: none under an experience of one, and otherwise
	/// two more than its experience over ten rounded to the nearest whole - to the even
	/// one from a half - up to the eleventh.
	/// </summary>
	public static int ExperienceFrame(CFixedPoint experience)
	{
		if (experience.Raw < Scale)
		{
			return 1;
		}
		long tenths = experience.Raw / 10;                    // experience over ten, in thousandths
		long whole = tenths / Scale;
		long rest = tenths % Scale;
		if (rest > Scale / 2 || (rest == Scale / 2 && whole % 2 == 1))
		{
			whole++;
		}
		return (int)Math.Min(whole + 2, 11);
	}

	/// <summary>A brigade's organisation bar: what it has over the most it may have, officers and all.</summary>
	public static int BrigadeOrganisationBar(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		return Share(subUnit.Organisation.Raw, COrganisation.Ceiling(state, owner, subUnit).Raw);
	}

	/// <summary>A brigade's strength bar: what it has over what its type is made with.</summary>
	public static int BrigadeStrengthBar(CSubUnit subUnit)
	{
		return Share(subUnit.Strength.Raw, subUnit.Stats[CSubUnitType.MaxStrength].Raw);
	}

	/// <summary>How high a window of rows is: its label, and its rows as far as the window's own height has room for.</summary>
	public static int RowsHeight(int label, int rows, int rowHeight, int windowHeight)
	{
		return label + Math.Max(0, Math.Min(rows * rowHeight, windowHeight - label));
	}

	// ---- the units under it

	/// <summary>The units directly under one, in the order it holds them.</summary>
	public static IReadOnlyList<CUnit> Children(CUnit unit)
	{
		return unit.Children;
	}

	/// <summary>
	/// The heading over them: how many there are at each level, the highest first -
	/// "2 Corps, 1 Division(s)".
	/// </summary>
	public static string ChildrenHeader(Func<string, string> localise, CUnit unit)
	{
		var parts = new List<string>();
		foreach ((EUnitKind kind, string key) in LevelKeys)
		{
			int count = 0;
			foreach (CUnit child in unit.Children)
			{
				if (child.Kind == kind)
				{
					count++;
				}
			}
			if (count > 0)
			{
				parts.Add(count.ToString(CultureInfo.InvariantCulture) + " " + localise(key));
			}
		}
		return string.Join(", ", parts);
	}

	/// <summary>The men of every land unit under one, at any depth; nothing for a unit that is not a land unit.</summary>
	public static string ChildrenMen(CUnit unit)
	{
		return CMovementSpeed.IsLand(unit) ? CBuilderText.Men(new CFixedPoint(StrengthBelow(unit))) : "";
	}

	private static long StrengthBelow(CUnit unit)
	{
		long strength = 0;
		foreach (CUnit child in unit.Children)
		{
			if (CMovementSpeed.IsLand(child))
			{
				foreach (CSubUnit subUnit in child.SubUnits)
				{
					strength += subUnit.Strength.Raw;
				}
			}
			strength += StrengthBelow(child);
		}
		return strength;
	}

	/// <summary>A subordinate's own men, without what is under it; nothing for a unit that is not a land unit.</summary>
	public static string ChildMen(CUnit unit)
	{
		if (!CMovementSpeed.IsLand(unit))
		{
			return "";
		}
		long strength = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
		}
		return CBuilderText.Men(new CFixedPoint(strength));
	}

	// ---- the buttons

	/// <summary>
	/// The one button of five a unit's level shows: a division makes a corps, a corps an
	/// army, an army an army group, an army group a theatre, and a theatre is edited.
	/// Empty for a fleet or an air unit.
	/// </summary>
	public static string LevelButton(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Division => "create_corps",
			EUnitKind.Corps => "create_army",
			EUnitKind.Army => "create_armygroup",
			EUnitKind.ArmyGroup => "create_theatre",
			EUnitKind.Theatre => "edit_theatre",
			_ => "",
		};
	}

	/// <summary>The five, for hiding the four that are not it.</summary>
	public static readonly IReadOnlyList<string> LevelButtons = new[]
	{
		"create_corps", "create_army", "create_armygroup", "create_theatre", "edit_theatre",
	};

	private static CFixedPoint Sum(CUnit unit, string statistic)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[statistic].Raw;
		}
		return new CFixedPoint(sum);
	}
}
