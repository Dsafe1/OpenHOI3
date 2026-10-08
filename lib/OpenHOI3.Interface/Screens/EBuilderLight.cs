namespace OpenHOI3.Interface.Screens;

/// <summary>
/// Which of a builder window's three lights is on: whether the production share has the
/// IC to spare for what is about to be ordered. See <c>docs/mechanics/builders.md</c>,
/// section 6.
/// </summary>
public enum EBuilderLight
{
	/// <summary>The share has all of it to spare.</summary>
	Green,

	/// <summary>The share has something to spare, and not enough.</summary>
	Orange,

	/// <summary>The share has nothing to spare.</summary>
	Red,
}
