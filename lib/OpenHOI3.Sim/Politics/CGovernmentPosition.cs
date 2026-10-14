namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A cabinet post from <c>common/government_positions.txt</c>, numbered from 1 in the file's order; one with
/// <c>changeable = no</c> - the head of state and the head of government - is fixed. See
/// <c>docs/mechanics/politics.md</c>, section 1.
/// </summary>
public sealed record CGovernmentPosition(string Name, int Index, bool Changeable);
