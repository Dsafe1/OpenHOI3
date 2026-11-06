using System.Collections.Generic;

namespace OpenHOI3.Interface.Map;

/// <summary>A country's name laid out on the map in the far view: the text as shown and its letters.</summary>
public sealed record CCountryMapName(string Text, IReadOnlyList<CMapNameLetter> Letters);
