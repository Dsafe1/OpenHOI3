using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// What becomes of a faction's list of provinces as the rebels take land and lose it: the siege won, and the province
/// dropped. See <c>docs/mechanics/rebels.md</c>, section 10.
/// </summary>
public static class CRebelSiege
{
	/// <summary>Who reports a siege won.</summary>
	public const string Messenger = "MESSAGE_HEAD_MARS";

	/// <summary>
	/// What the original's controller setter does on the rebels' side: where they lose a province, the faction holding
	/// it drops it; where they become the holder of an owned province from somebody else, the first rebel army there
	/// whose faction does not hold it already makes that faction take it. A change with its consequences is kept in the
	/// province's history, and the last day the rebels took it is what independence counts from.
	/// </summary>
	public static void ControllerChanged(CGameState state, CProvince province, CCountryTag? old, CCountryTag now,
		bool consequences)
	{
		if (consequences && CRebelFactions.IsRebels(now))
		{
			province.RebelsTook = state.Date;
		}
		if (CRebelFactions.IsRebels(old) && CRebelFactions.Holding(state, province.Id) is { } losing)
		{
			losing.Provinces.Remove(province.Id);
		}
		if (!CRebelFactions.IsRebels(now) || CRebelFactions.IsRebels(old) || province.Owner is null)
		{
			return;
		}
		foreach (CUnit unit in state.UnitsIn(province.Id))
		{
			if (CRebelFactions.IsRebels(unit.Owner) && CRebelFactions.OfArmy(state, unit) is { } faction
				&& !faction.Provinces.Contains(province.Id))
			{
				Take(state, faction, province);
				return;
			}
		}
	}

	/// <summary>
	/// The original's <c>RebelFaction_TakeProvince</c>, the siege won: one draw seeds a scope of the province with no
	/// country and the faction; where <c>siege_won_trigger</c> holds, the owner is told <c>REVOLTSIEGE</c> - which the
	/// player is only where the effect has words - and <c>siege_won_effect</c> runs; and the province goes on the
	/// faction's list whatever the condition answered.
	/// </summary>
	public static void Take(CGameState state, CRebelFaction faction, CProvince province)
	{
		CEventScope scope = CEventScope.ForRebels(faction, province.Id, (uint)state.Random.Next());
		if (CConditions.Holds(state, faction.Type.SiegeWonTrigger, scope))
		{
			if (province.Owner is { } owner)
			{
				state.RaisePactMessage(new CPactMessage("REVOLTSIEGE", "", "", CRebelFactions.Tag, owner)
				{
					Told = owner,
					Messenger = Messenger,
					// EFFECT is the type's name and PROVINCEID the province's, for the effect's words the message writes.
					Values = new[]
					{
						("REBELS", faction.Name), ("EFFECT", faction.Type.Name), ("PROV", province.Name),
						("PROVINCEID", province.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					},
				});
			}
			CEffects.Run(state, faction.Type.SiegeWonEffect, scope);
		}
		faction.Provinces.Add(province.Id);
	}
}
