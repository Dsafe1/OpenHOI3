using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Screens;

/// <summary>One line of a Strategic Warfare Manager chart: its values in 15-place fixed point, oldest first, and its country's colour.</summary>
public sealed record CWarfareSeries(IReadOnlyList<long> Values, CColor Colour);
