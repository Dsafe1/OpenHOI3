using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CSpyPresence</c>, a country's spies in another as the intelligence script sees them: their level, their priority,
/// each mission's weight, the date the missions last changed and the primary mission, and the static
/// <c>MissionAllowed</c> nothing calls; and the two constants. See <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
public static class CSpyPresenceClass
{
	private const string Name = "CSpyPresence";

	/// <summary>What <c>GetPrimaryMission</c> answers where no mission has a weight: <c>SPYMISSION_MAX</c>.</summary>
	public const int NoMission = 9;

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Constant("MAX_SPY_PRIORITY", 3)
			.Constant("MAX_SPY_LEVEL", 10)
			.Method("GetLevel", call => call.Return(api.Fixed(new CFixedPoint(Of(call).Level))))
			.Method("GetPriority", call => call.Return(Of(call).Priority))
			// Unchecked in the original; out of range here answers nought.
			.Method("GetMissionPriority", call =>
			{
				CSpyPresence presence = Of(call);
				int mission = (int)call.Number(2);
				return call.Return(mission >= 0 && mission < CSpyPresence.MissionCount ? presence.Missions[mission] : 0);
			})
			.Method("GetLastMissionChangeDate", call =>
			{
				long? hour = Of(call).LastMissionChange;
				CDate date = hour is { } at ? CDate.FromDayNumber((int)(at / CGameState.HoursPerDay)) : new CDate(1, 1, 1);
				return call.Return(new CLuaObject(api.Date, date));
			})
			.Method("GetPrimaryMission", call => call.Return(PrimaryMission(Of(call))))
			.Static("MissionAllowed", call => call.Return(MissionAllowed((int)call.Number(1), call.Object<Sim.Countries.CCountryTag>(2, "CCountryTag"),
				call.Object<Sim.Countries.CCountryTag>(3, "CCountryTag"))));
	}

	/// <summary>The mission with the highest weight, the lower of equals; <see cref="NoMission"/> where none is above nought.</summary>
	public static int PrimaryMission(CSpyPresence presence)
	{
		int best = NoMission;
		int highest = 0;
		for (int i = 0; i < CSpyPresence.MissionCount; i++)
		{
			if (presence.Missions[i] > highest)
			{
				highest = presence.Missions[i];
				best = i;
			}
		}
		return best;
	}

	/// <summary>
	/// The original's table nothing asks: counter-espionage and boost ruling party anywhere, raise national unity only at
	/// home, the six others only abroad.
	/// </summary>
	public static bool MissionAllowed(int mission, Sim.Countries.CCountryTag actor, Sim.Countries.CCountryTag target)
	{
		bool home = actor == target;
		return (ESpyMission)mission switch
		{
			ESpyMission.CounterEspionage or ESpyMission.BoostRulingParty => true,
			ESpyMission.RaiseNationalUnity => home,
			_ => mission >= 0 && mission < CSpyPresence.MissionCount && !home,
		};
	}

	private static CSpyPresence Of(CLuaCall call) => call.Object<CSpyPresence>(1, Name);
}
