using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The province view's covert operations and allied objectives: the points, which operations are listed and which may be
/// pressed, and which allies may be asked. See <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public static class CProvinceCovertOpsText
{
	/// <summary><c>cov_ops_points</c>: the player's covert-ops points in the province's holder, whole.</summary>
	public static long Points(CGameState state, CCountry player, CCountryTag? holder)
	{
		return state.Country(holder) is { } controller ? CGameState.Presence(player, controller).CovertOpsPoints / 1000 : 0;
	}

	/// <summary>Whether an operation's <c>is_valid</c> holds in the province, with the player as FROM, or says nothing.</summary>
	public static bool IsValid(CGameState state, CCountry player, CProvince province, CCovertOperation operation)
	{
		return province.Id != 0 && (operation.IsValid.Children.Count == 0
			|| CConditions.Holds(state, operation.IsValid, CEventScope.Start(null, province.Id, player.Tag)));
	}

	/// <summary>The operations the panel lists, in the file's order: those valid in the province.</summary>
	public static List<CCovertOperation> Listed(CGameState state, CCountry player, CProvince province)
	{
		var listed = new List<CCovertOperation>();
		foreach (CCovertOperation operation in state.CovertOperations)
		{
			if (IsValid(state, player, province, operation))
			{
				listed.Add(operation);
			}
		}
		return listed;
	}

	/// <summary>Whether a row's button may be pressed: still valid, and the player may choose it.</summary>
	public static bool Enabled(CGameState state, CCountry player, CProvince province, CCovertOperation operation)
	{
		return IsValid(state, player, province, operation) && CCovertOps.MaySelect(state, player, province, operation);
	}

	/// <summary>
	/// The allies the allied-objectives panel lists: the player's allies, leaving out any in a faction that is not the
	/// player's where both are in one.
	/// </summary>
	public static List<CCountry> Allies(CGameState state, CCountry player)
	{
		var allies = new List<CCountry>();
		foreach (CCountry ally in CCountryLists.Allies(state, player))
		{
			if (player.Faction.Length > 0 && ally.Faction.Length > 0 && ally.Faction != player.Faction)
			{
				continue;
			}
			allies.Add(ally);
		}
		return allies;
	}
}
