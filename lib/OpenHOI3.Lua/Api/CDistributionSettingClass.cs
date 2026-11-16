using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CDistributionSetting</c>, one of a country's shares - <c>country:GetProductionDistributionAt(i)</c> and
/// <c>GetLeadershipDistributionAt(i)</c> - with its constants: <c>GetPercentage()</c> the share as set, and
/// <c>GetNeeded()</c> what the share's slider shows as its need, each a <c>CFixedPoint64</c>. See
/// <c>docs/mechanics/lua.md</c>, section 4.5, and <c>reversing/findings/FINDINGS-ministers.md</c> C.5.
/// </summary>
public static class CDistributionSettingClass
{
	public const string Name = "CDistributionSetting";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetPercentage", call => call.Return(new CLuaObject(api.FixedPoint64, Percentage(Of(call)))))
			.Method("GetNeeded", call => call.Return(new CLuaObject(api.FixedPoint64, Needed(api.State, Of(call)))))
			// The shares of IC and of leadership, by the original's order (FINDINGS-ministers.md C.5).
			.Constant("_PRODUCTION_LENDLEASE_", 0)
			.Constant("_PRODUCTION_CONSUMER_", 1)
			.Constant("_PRODUCTION_PRODUCTION_", 2)
			.Constant("_PRODUCTION_SUPPLY_", 3)
			.Constant("_PRODUCTION_REINFORCEMENT_", 4)
			.Constant("_PRODUCTION_UPGRADE_", 5)
			.Constant("_PRODUCTION_numof_", CProduction.CategoryCount)
			.Constant("_LEADERSHIP_NCO_", 0)
			.Constant("_LEADERSHIP_DIPLOMACY_", 1)
			.Constant("_LEADERSHIP_ESPIONAGE_", 2)
			.Constant("_LEADERSHIP_RESEARCH_", 3)
			.Constant("_LEADERSHIP_numof_", CLeadership.CategoryCount);
	}

	/// <summary>
	/// The share at an index: the original reads its vector unchecked; here an index outside it is the Lua error the
	/// call names.
	/// </summary>
	public static CLuaDistributionSetting At(CLuaCall call, CCountry country, bool leadership)
	{
		int index = call.Integer(2);
		int count = leadership ? CLeadership.CategoryCount : CProduction.CategoryCount;
		if (index < 0 || index >= count)
		{
			throw new CLuaError($"{(leadership ? "GetLeadershipDistributionAt" : "GetProductionDistributionAt")}: no share {index}, "
				+ $"there are {count}");
		}
		return new CLuaDistributionSetting(country, leadership, index);
	}

	/// <summary>The share as the slider sets it, the setting's own figure.</summary>
	public static CFixed15 Percentage(CLuaDistributionSetting setting)
	{
		return setting.IsLeadership
			? setting.Country.LeadershipShares[setting.Index]
			: setting.Country.ProductionShares[setting.Index];
	}

	/// <summary>
	/// What the share wants, the slider's need: lend-lease, officers and espionage nought, the rest as the slider works it
	/// (<c>sliders.md</c>, section 2), thousandths turned to the second kind of number toward nought as the original does.
	/// </summary>
	public static CFixed15 Needed(CGameState state, CLuaDistributionSetting setting)
	{
		CFixedPoint need = setting.IsLeadership
			? CLeadership.Needed(state, setting.Country, (ELeadershipCategory)setting.Index)
			: CProduction.Needed(state, setting.Country, (EProductionCategory)setting.Index);
		return CFixed15.FromThousandths(need);
	}

	private static CLuaDistributionSetting Of(CLuaCall call) => call.Object<CLuaDistributionSetting>(1, Name);
}
