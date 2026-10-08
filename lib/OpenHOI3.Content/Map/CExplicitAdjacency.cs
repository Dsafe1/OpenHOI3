namespace OpenHOI3.Content.Map;

/// <summary>A connection the raster cannot show, from <c>adjacencies.csv</c>.</summary>
/// <param name="Kind">As written: <c>sea</c> for a strait, <c>canal</c> or <c>river</c>.</param>
/// <param name="Through">The sea province a strait crosses, or 0 when the row names none.</param>
public readonly record struct CExplicitAdjacency(int From, int To, string Kind, int Through);
