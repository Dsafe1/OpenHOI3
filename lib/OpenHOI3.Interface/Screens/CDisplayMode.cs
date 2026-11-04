using System.Collections.Generic;

namespace OpenHOI3.Interface.Screens;

/// <summary>A size of screen and the refresh rates it may run at, in the order they were found: <c>gameoptions.md</c>, section 3.</summary>
public sealed record CDisplayMode(int Width, int Height, IReadOnlyList<int> Rates);
