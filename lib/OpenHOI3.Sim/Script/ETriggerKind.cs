namespace OpenHOI3.Sim.Script;

/// <summary>
/// What a trigger's key turned out to be, in the order the original tries them:
/// <c>docs/mechanics/script.md</c>, section 5.
/// </summary>
public enum ETriggerKind
{
	/// <summary>One of the language's 153 keywords, in <see cref="CTrigger.Keyword"/>.</summary>
	Keyword,

	/// <summary>A government position, asked for the minister holding it.</summary>
	GovernmentPosition,

	/// <summary>A building, asked for its level.</summary>
	Building,

	/// <summary>A technology, asked for its level.</summary>
	Technology,

	/// <summary>A unit type, asked for how many there are.</summary>
	UnitType,

	/// <summary>A law group, asked for the law named by the value.</summary>
	LawGroup,

	/// <summary>A combined-arms group, always asked whether there is none of it.</summary>
	CombinedArms,

	/// <summary>An ideology, asked for its organisation or its popularity.</summary>
	Ideology,

	/// <summary>A region, over whose provinces the children are asked.</summary>
	Region,

	/// <summary>A country, a province or a scope word, in whose scope the children are asked.</summary>
	Scope,
}
