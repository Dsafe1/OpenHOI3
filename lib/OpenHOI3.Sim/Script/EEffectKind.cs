namespace OpenHOI3.Sim.Script;

/// <summary>
/// What an effect's key turned out to be, in the order the original tries them:
/// <c>docs/mechanics/script.md</c>, section 6.
/// </summary>
public enum EEffectKind
{
	/// <summary>One of the language's 91 keywords, in <see cref="CEffect.Keyword"/>.</summary>
	Keyword,

	/// <summary>A government position, given the minister named by the value.</summary>
	GovernmentPosition,

	/// <summary>A building, given levels.</summary>
	Building,

	/// <summary>An ideology, given organisation and popularity.</summary>
	Ideology,

	/// <summary>A technology, given an investment.</summary>
	Technology,

	/// <summary>A rebel type, risen.</summary>
	RebelType,

	/// <summary>A region, over whose provinces the children are done.</summary>
	Region,

	/// <summary>A unit type, given a brigade of it.</summary>
	UnitType,

	/// <summary>A country, a province or a scope word, in whose scope the children are done.</summary>
	Scope,

	/// <summary>A scope whose key is none of those, whose children the original never does.</summary>
	DeadScope,

	/// <summary>One entry of a <c>random_list</c>: its weight and its effects.</summary>
	Weighted,
}
