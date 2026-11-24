using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the legend of a custom map source says: what stands under each shade, and the lines beneath them. See
/// <c>docs/mechanics/custommapmodes.md</c>, section 6.
/// </summary>
public static class CCustomMapLegend
{
	public const string CutOff = CCustomMapSources.TextPrefix + "CUT_OFF";
	public const string NoneHasAny = CCustomMapSources.TextPrefix + "NONE_HAS_ANY";
	public const string MapNote = CCustomMapSources.TextPrefix + "MAP_NOTE";
	public const string LevelNote = CCustomMapSources.TextPrefix + "LEVEL_NOTE";
	public const string GreyNote = CCustomMapSources.TextPrefix + "GREY_NOTE";
	public const string GreySupplyNote = CCustomMapSources.TextPrefix + "GREY_SUPPLY_NOTE";

	/// <summary>The line a map mode button's tooltip gains where a right click on it lists sources.</summary>
	public const string ListHint = CCustomMapSources.TextPrefix + "TIP";

	/// <summary>The row the list begins with: the map mode as the game has it.</summary>
	public const string GameOwn = CCustomMapSources.TextPrefix + "GAME_OWN";

	/// <summary>What the palette button says, by the palette in use.</summary>
	public static string PaletteKey(string palette) => CCustomMapSources.TextPrefix + "PALETTE_" + CCustomMapColours.Palette(palette).ToUpperInvariant();

	/// <summary>
	/// What stands under a shade, 1 to 10: for a level its own number; else the value the shade starts at - which for a
	/// source where more is worse run the other way, the largest under the first.
	/// </summary>
	public static string Under(Func<string, string> localise, CCustomMapSource source, CCustomMapScale scale, int shade)
	{
		if (source.Scale == ECustomMapScale.Level || scale.Starts is not { } starts)
		{
			return shade.ToString(CultureInfo.InvariantCulture);
		}
		long start = starts[source.HigherIsWorse ? CCustomMapScale.Shades - shade : shade - 1];
		return source.Unit switch
		{
			ECustomMapUnit.Cost => start >= CSupplyNetwork.Unreached ? localise(CutOff) : start.ToString(CultureInfo.InvariantCulture),
			ECustomMapUnit.Percent => (start / 10).ToString(CultureInfo.InvariantCulture) + "%",
			_ => Amount(start),
		};
	}

	/// <summary>
	/// Thousandths to about three figures: no decimals from a hundred, one from ten, two from one and three below, with
	/// the noughts at the end and a point left bare taken off.
	/// </summary>
	public static string Amount(long thousandths)
	{
		double value = thousandths / 1000.0;
		int places = value >= 100 ? 0 : value >= 10 ? 1 : value >= 1 ? 2 : 3;
		string text = value.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
		if (text.Contains('.'))
		{
			text = text.TrimEnd('0').TrimEnd('.');
		}
		return text;
	}

	/// <summary>
	/// The lines under the shades: the source's own explanation; for a map source how many provinces have any and where
	/// the top shade begins, or that none has any; for a level the ladder; and what the greys mean.
	/// </summary>
	public static string Notes(Func<string, string> localise, CCustomMapSource source, CCustomMapScale scale)
	{
		var text = new System.Text.StringBuilder();
		void Line(string line)
		{
			if (line.Length > 0)
			{
				text.Append(text.Length > 0 ? "\n" : "").Append(line);
			}
		}
		if (source.Explanation.Length > 0)
		{
			Line(localise(source.Explanation));
		}
		if (source.Scale == ECustomMapScale.Map)
		{
			Line(scale.Starts is { } starts
				? CTextTemplate.Fill(localise(MapNote), ("COUNT", scale.Count.ToString(CultureInfo.InvariantCulture)),
					("TOP", Amount(starts[CCustomMapScale.Shades - 1])))
				: localise(NoneHasAny));
		}
		else if (source.Scale == ECustomMapScale.Level)
		{
			Line(localise(LevelNote));
		}
		Line(localise(source.HiddenUnseen ? GreySupplyNote : GreyNote));
		return text.ToString();
	}
}
