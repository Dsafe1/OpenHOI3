using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// Which Goto each message type has. In the original each place that raises a message hands it what its Goto does, so this
/// is the original's table of them and no file can change it - a file says only whether the button is there. See
/// <c>docs/mechanics/messages.md</c>, section 4.1.
/// </summary>
public static class CMessageGotos
{
	public const string AgreementExpired = "DIPLOEXPIRE";
	public const string BuildingDone = "BUILDING_DONE";

	private static readonly HashSet<string> Battles = new(StringComparer.Ordinal)
	{
		"ATTACKTHEM", "ATTACKUS", "NAVALATTACKTHEM", "NAVALATTACKUS", "AIRATTACKTHEM", "AIRATTACKUS", "SHATTERED",
	};

	private static readonly HashSet<string> Bombings = new(StringComparer.Ordinal) { "BOMBTHEM", "BOMBUS" };

	private static readonly HashSet<string> Capitals = new(StringComparer.Ordinal) { "GIESWAPOTHER", "INDEPENDANCE" };

	private static readonly HashSet<string> Invasions = new(StringComparer.Ordinal) { "INVADEDUS", "PARATROOPEDUS" };

	/// <summary>
	/// The original's <c>GotoUnitForMessage</c>: the three arrivals. <c>EXPRETURNED</c> and <c>EXPRETURNEDWE</c> have it too,
	/// and are left out while their message carries no unit.
	/// </summary>
	private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
	{
		Sim.Movement.CArrival.Land, Sim.Movement.CArrival.Naval, Sim.Movement.CArrival.Air,
	};

	private static readonly HashSet<string> Screens = new(StringComparer.Ordinal)
	{
		BuildingDone, AgreementExpired, "ELECTIONRESULTS", "NUKE_ON_OTHER", "NUKE_ON_US", "OTHER_DEMOBILIZE", "OTHER_MOBILIZE",
		"SPIESFOUNDTHEM", "SPIESFOUNDUS", "TECH_ONCE", "TECH_REPEAT", "TECH_STOLEN", "TECH_STOLEN_BY_UNKNOWN", "THEYSURRENDER",
		"THEYSURRENDERUS", "WESURRENDER", "WE_DEMOBILIZE", "WE_MOBILIZE", "WE_NUKE", "WE_STEAL_TECH_ONCE", "WE_STEAL_TECH_REPEAT",
	};

	private static readonly HashSet<string> Provinces = new(StringComparer.Ordinal)
	{
		"ACCMILOTHER", "ACCMILUS", "ACTRAWITHUS", "AIRBATTLEOVER", "ANNEX", "ATTACHEDUNIT", "BOMBENDTHEM", "BOMBENDUS", "BRKTRADEOTH",
		"BRKTRADEUS", "CANCMILUS", "CANMILOTHE", "CONVOY_ATTACK_ON_US", "CONVOY_TOO_STRONG", "DEBTALLOWACCEPT", "DEBTALLOWDECLINE",
		"DEBTREVOKE", "DECLWAR", "DECLWAROTHER", "DECMILOTHER", "DECMILUS", "DECOFFMILOTHER", "DETACHEDUNIT", "DETRAWITHOTHER",
		"DETRAWITHUS", "DISBAND", "EXPOTHER", "FACTIONINVITEACCEPT", "FACTIONINVITEDECLINCE", "FACTIONJOINACCEPT",
		"FACTIONJOINACCEPTOTHER", "FACTIONJOINDECLINCE", "FACTIONWARDEF", "FACTIONWAROFF", "GUARACANCELTOTHEM", "GUARACANCELTOUS",
		"GUARATOOTHER", "GUARATOUS", "LANDBATTLEOVER", "LENDACCEPT", "LENDDECLINE", "LENDOFFERACCEPT", "LENDOFFERREFUSED", "LENDREVOKE",
		"LICBUYACCEPT", "LICBUYDECLINE", "MILALLACCEPT", "MILALLBAN", "MILALLBANOTHER", "MILALLDISHONOUR", "MILALLHONOUR", "MILALLREJECT",
		"MILALLWEACCEPT", "MILALLWEBAN", "MILALLWEREJECT", "MILLALLINVACCEPTOTHER", "MILLALLJOINREJECTOTHER", "NAVALBATTLEOVER",
		"OFFACCMILOTHER", "OFFACCMILUS", "OFFDECMILUS", "OTHERALIGN", "OTHERALIGNSTOP", "OTHERCANCELEMBARGO", "OTHERCANCELNAP",
		"OTHERDEFECT", "OTHEREMBARGO", "OTHERINFLUENCE", "OTHERINFLUENCESTOP", "OTHERMILALLDISHONOUR", "OTHERMILALLHONOUR", "OTHERNAP",
		"OTHERREJECTNAP", "OTHERTRADEAG", "OUREXPACC", "OUREXPDEC", "OURUNDERGROUNDREMOVED", "PARTISANS", "PATROLTRACK", "PEACEACCEPT",
		"PEACEDECLINE", "PROVINCELOST", "REVOLT", "THEYALIGN", "THEYALIGNSTOP", "THEYCANCELEMBARGO", "THEYCANCELNAP", "THEYDEFECT",
		"THEYEMBARGO", "THEYINFLUENCE", "THEYINFLUENCESTOP", "THEYNAP", "THEYREJECTNAP", "TRANSFEREDSUBUNIT", "UNDERGROUNDREMOVED",
		"UNITDEPLOYED", "WEACCMIL", "WEALIGN", "WEALIGNSTOP", "WEBRKTRADE", "WECANCELEMBARGO", "WECANCELNAP", "WECANMIL", "WEDECLWAR",
		"WEDECMIL", "WEDECOFFMIL", "WEDEFECT", "WEDETRA", "WEEMBARGO", "WEGUARAT", "WEINFLUENCE", "WEINFLUENCESTOP", "WEMILALLDISHONOUR",
		"WEMILALLHONOUR", "WENAP", "WEOFFACCMIL", "WEREJECTNAP", "WETRADEAG", "WE_ATTACK_CONVOY",
	};

	/// <summary>What the Goto of a message of the type does.</summary>
	public static EMessageGoto Of(string type)
	{
		if (Provinces.Contains(type))
		{
			return EMessageGoto.Province;
		}
		if (Battles.Contains(type))
		{
			return EMessageGoto.Battle;
		}
		if (Bombings.Contains(type))
		{
			return EMessageGoto.Bombing;
		}
		if (Screens.Contains(type))
		{
			return EMessageGoto.Screen;
		}
		if (Capitals.Contains(type))
		{
			return EMessageGoto.Capital;
		}
		if (Units.Contains(type))
		{
			return EMessageGoto.Unit;
		}
		return Invasions.Contains(type) ? EMessageGoto.Invasion : EMessageGoto.None;
	}

	/// <summary>Whether a message of the type goes to the technology screen, shutting it where it is up.</summary>
	public static bool OpensTechnology(string type)
	{
		return type is "TECH_ONCE" or "TECH_REPEAT";
	}
}
