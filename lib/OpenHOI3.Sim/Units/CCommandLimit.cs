using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// How many land units an army group, an army or a corps may have under it. The original's answer is five for every
/// country and every level, and a faithful game's is that. An enhanced game's is a mod's choice, written as data: a
/// base for each level in <c>common/defines.lua</c>, five where the file gives none, and to it the whole part of what
/// the commander's country has of the level's kind of modifier - from a technology's levels, a triggered modifier, a
/// law, a minister or anything else a modifier comes from - and never less than one. A define of its own lets a
/// division of one brigade take no place. A theatre has no limit in either game, and the AI's own ladder of commands
/// (<c>CCommandLadder</c>) counts as the original's does and asks nothing here.
/// See <c>docs/mechanics/attaching.md</c>, section 8.
/// </summary>
public static class CCommandLimit
{
	/// <summary>The original's limit, at every level.</summary>
	public const int Original = 5;

	/// <summary>The kinds of modifier that raise or lower the limit of a corps, an army and an army group.</summary>
	public const string CorpsKind = "corps_unit_limit";
	public const string ArmyKind = "army_unit_limit";
	public const string ArmyGroupKind = "army_group_unit_limit";

	/// <summary>The three kinds, a corps' first: what an enhanced game's files may name beyond the original's kinds.</summary>
	public static IReadOnlyList<string> Kinds { get; } = new[] { CorpsKind, ArmyKind, ArmyGroupKind };

	/// <summary>The defines of the <c>military</c> section that give each level's base.</summary>
	public const string CorpsDefine = "OPENHOI_CORPS_UNIT_LIMIT";
	public const string ArmyDefine = "OPENHOI_ARMY_UNIT_LIMIT";
	public const string ArmyGroupDefine = "OPENHOI_ARMY_GROUP_UNIT_LIMIT";

	/// <summary>The define that, at one or more, lets a division of one brigade take no place under a commander.</summary>
	public const string LoneBrigadeDefine = "OPENHOI_LONE_BRIGADE_TAKES_NO_PLACE";

	/// <summary>The most land units a commander below a theatre may have under it.</summary>
	public static int Of(CGameState state, CUnit commander)
	{
		if (!state.Rules.Enhanced || Level(commander.Kind) is not { } level)
		{
			return Original;
		}
		long thousandths = state.Defines.Has(CManpower.MilitarySection, level.Define)
			? state.Defines[CManpower.MilitarySection, level.Define].Raw
			: Original * CFixedPoint.Scale;
		if (state.Country(commander.Owner) is { } country)
		{
			thousandths += country.Modifier[level.Kind].Raw + country.Technology[level.Kind].Raw;
		}
		return (int)Math.Max(1, thousandths / CFixedPoint.Scale);
	}

	/// <summary>Whether a division of one brigade takes no place: an enhanced game's, where its defines say so.</summary>
	public static bool SparesLoneBrigades(CGameState state)
	{
		return state.Rules.Enhanced && state.Defines[CManpower.MilitarySection, LoneBrigadeDefine].Raw >= CFixedPoint.Scale;
	}

	/// <summary>A division that holds exactly one brigade.</summary>
	public static bool IsLoneBrigade(CUnit unit)
	{
		return unit.Kind == EUnitKind.Division && unit.SubUnits.Count == 1;
	}

	/// <summary>The land units under a commander that count towards its limit.</summary>
	public static int Counted(CGameState state, CUnit commander)
	{
		bool spares = SparesLoneBrigades(state);
		int land = 0;
		foreach (CUnit child in commander.Children)
		{
			if (child.Kind != EUnitKind.Navy && child.Kind != EUnitKind.Air && !(spares && IsLoneBrigade(child)))
			{
				land++;
			}
		}
		return land;
	}

	/// <summary>Whether a commander below a theatre has room for one more land unit - this one.</summary>
	public static bool HasRoom(CGameState state, CUnit commander, CUnit subordinate)
	{
		if (SparesLoneBrigades(state) && IsLoneBrigade(subordinate))
		{
			return true;
		}
		return Counted(state, commander) < Of(state, commander);
	}

	private static (string Kind, string Define)? Level(EUnitKind kind)
	{
		return kind switch
		{
			EUnitKind.Corps => (CorpsKind, CorpsDefine),
			EUnitKind.Army => (ArmyKind, ArmyDefine),
			EUnitKind.ArmyGroup => (ArmyGroupKind, ArmyGroupDefine),
			_ => null,
		};
	}
}
