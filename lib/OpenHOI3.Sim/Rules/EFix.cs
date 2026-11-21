namespace OpenHOI3.Sim.Rules;

/// <summary>
/// A fault of the original's that the enhanced mode mends: on in an enhanced game unless the mod names it as one it does
/// not want (<c>docs/enhanced.md</c>, <i>The shape</i>). One is added here when a candidate of that file is taken up,
/// with its name in <see cref="CFixes"/> and a test for each arm.
/// </summary>
public enum EFix
{
	/// <summary>A minister's <c>decay</c> counts beside the technologies' (<c>docs/mechanics/research.md</c>, section 6).</summary>
	MinisterTechDecay,
}
