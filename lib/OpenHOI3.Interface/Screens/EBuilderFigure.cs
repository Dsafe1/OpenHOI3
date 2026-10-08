namespace OpenHOI3.Interface.Screens;

/// <summary>
/// How a column of a builder window's list writes its figure. See
/// <c>docs/mechanics/builders.md</c>, section 3.
/// </summary>
public enum EBuilderFigure
{
	/// <summary>The statistic as it is.</summary>
	Plain,

	/// <summary>A hundred times the statistic: a firing distance.</summary>
	Hundredfold,

	/// <summary>A hundred times the statistic and a percent sign: softness, positioning.</summary>
	Percent,

	/// <summary>A hundred times the statistic, in thousands with a <c>k</c> from a thousand up: strength as men.</summary>
	Men,

	/// <summary>A speed: whole, unless it is a slow one with a fraction.</summary>
	Speed,

	/// <summary>What one costs the country in IC a day.</summary>
	IcCost,

	/// <summary>What one costs the country in manpower.</summary>
	Manpower,

	/// <summary>The days one takes the country to build.</summary>
	Days,
}
