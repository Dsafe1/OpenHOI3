using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's own conversion settings in an enhanced game's save, both ways (<c>docs/mechanics/savegame.md</c>,
/// section 21): two keys of ours in the country's block, each four whole numbers - whether the country sets the
/// conversion itself, as 1 or 0, then the source a day, the source to keep and the product to stop at, in
/// thousandths. A setting never touched is not written, and a faithful game writes neither key.
/// </summary>
public static class CSaveConversions
{
	public const string EnergyKey = "openhoi_energy_conversion";
	public const string CrudeOilKey = "openhoi_crude_oil_conversion";

	/// <summary>The two keys, which a country's block may hold beside the original's.</summary>
	public static readonly string[] Keys = { EnergyKey, CrudeOilKey };

	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		Write(writer, EnergyKey, country.EnergyConversion);
		Write(writer, CrudeOilKey, country.CrudeOilConversion);
	}

	private static void Write(CSaveTextWriter writer, string key, CConversionSetting setting)
	{
		if (!setting.IsUntouched)
		{
			writer.Numbers(key, new[] { setting.Own ? 1L : 0L, setting.PerDay.Raw, setting.Keep.Raw, setting.Target.Raw });
		}
	}

	/// <summary>A country's two settings as its block has them; one the block has not is as never touched.</summary>
	public static void Read(CCountry country, ClausewitzNode node)
	{
		Read(country.EnergyConversion, node.Get(EnergyKey));
		Read(country.CrudeOilConversion, node.Get(CrudeOilKey));
	}

	private static void Read(CConversionSetting setting, ClausewitzNode? node)
	{
		List<long> values = node?.Children.Select(value => CSaveValues.Whole(value.Value)).ToList() ?? new List<long>();
		if (values.Count < 4)
		{
			setting.Set(false, CFixedPoint.Zero, CFixedPoint.Zero, CFixedPoint.Zero);
			return;
		}
		setting.Set(values[0] != 0, new CFixedPoint(values[1]), new CFixedPoint(values[2]), new CFixedPoint(values[3]));
	}
}
