namespace OpenHOI3.Interface.Map;

/// <summary>How a custom map source's start values are written under the legend's shades.</summary>
public enum ECustomMapUnit
{
	/// <summary>A building's level: the shade's own number.</summary>
	Level,

	/// <summary>Thousandths, written to about three figures.</summary>
	Amount,

	/// <summary>Thousandths of a day, written as an amount.</summary>
	Days,

	/// <summary>A whole number, or "cut off" at the network's figure for no way.</summary>
	Cost,

	/// <summary>Thousandths of a whole, written as whole percent.</summary>
	Percent,
}
