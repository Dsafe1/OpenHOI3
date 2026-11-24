using System.Collections.Generic;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// One line of a plot of several (<see cref="IModWindowCanvas.PlotMany"/>): its values, a value a step, the step its
/// first value stands at - a day's number, so that lines beginning on different days stand where they belong - and its
/// colour, each part nought to one.
/// </summary>
public sealed record CPlotSeries(int First, float Red, float Green, float Blue, float Alpha, IReadOnlyList<float> Values);
