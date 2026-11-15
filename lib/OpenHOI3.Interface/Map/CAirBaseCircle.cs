namespace OpenHOI3.Interface.Map;

/// <summary>
/// One ring of range round an air base in the air map mode: where it stands, how far it
/// reaches, how full its aircraft are and how far it is dimmed. See
/// <c>docs/mechanics/airmap.md</c>, section 6.
/// </summary>
/// <param name="X">The air base's place, a world point in map pixels as positions.txt counts them: the 3D surface takes it
/// as it is, the 2D map through <see cref="CWorldPoint"/>.</param>
/// <param name="Y">The same, counting north.</param>
/// <param name="Radius">How far the group reaches, in map pixels.</param>
/// <param name="Share">The group's strength over the strength its wings have at their fullest.</param>
/// <param name="Dim">1, or 0.3 where the player has an air unit selected that is not of the group.</param>
public readonly record struct CAirBaseCircle(int Province, float X, float Y, float Radius, float Share, float Dim);
