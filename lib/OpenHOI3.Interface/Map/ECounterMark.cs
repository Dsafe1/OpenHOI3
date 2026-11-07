namespace OpenHOI3.Interface.Map;

/// <summary>
/// What colours a counter's lower right corner: the original's three marks, of which a counter shows one. See
/// <c>docs/mechanics/mapunits.md</c>, section 2.
/// </summary>
public enum ECounterMark
{
	None,

	/// <summary>Waiting out its attack delay: yellow.</summary>
	AttackDelay,

	/// <summary>Given under a twentieth of its supplies or its fuel: red.</summary>
	Supply,

	/// <summary>A brigade of it being changed into another type: orange.</summary>
	Upgrade,
}
