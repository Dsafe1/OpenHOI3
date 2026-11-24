namespace OpenHOI3.Interface.Map;

/// <summary>How a custom map source's value becomes one of ten shades (<c>docs/mechanics/custommapmodes.md</c>, section 4).</summary>
public enum ECustomMapScale
{
	/// <summary>The shade is the value itself, a building's level, held between 1 and 10.</summary>
	Level,

	/// <summary>Ten fixed start values.</summary>
	Bands,

	/// <summary>Ten start values worked out from the map: logarithmic between the 1% with the least and the 1% with the most.</summary>
	Map,
}
