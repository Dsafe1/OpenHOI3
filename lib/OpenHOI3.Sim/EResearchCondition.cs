namespace OpenHOI3.Sim;

/// <summary>What one line of a technology's <c>allow</c> block asks.</summary>
public enum EResearchCondition
{
	/// <summary>The country has that level of a technology, or more.</summary>
	TechnologyLevel,

	/// <summary>Its ability in a category is that much, or more.</summary>
	Ability,

	/// <summary>It controls that many provinces with a naval base, or more.</summary>
	Ports,

	/// <summary>A province it owns has the building.</summary>
	OwnsProvinceWithBuilding,

	All,
	Any,
	None,
}
