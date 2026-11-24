namespace OpenHOI3.Content.Windows;

/// <summary>
/// What a plot of several lines says of its axes (<see cref="IModWindowCanvas.PlotMany"/>): what runs across and what
/// runs up, and what the first and the last step are called - a date each, where a step is a day. An empty text is
/// not drawn; with all four empty the plot is its frame alone.
/// </summary>
public sealed record CPlotAxes(string Across, string Up, string From, string To)
{
	/// <summary>No axes.</summary>
	public static CPlotAxes None { get; } = new("", "", "", "");

	/// <summary>Whether nothing of it is drawn.</summary>
	public bool IsEmpty => Across.Length == 0 && Up.Length == 0 && From.Length == 0 && To.Length == 0;
}
