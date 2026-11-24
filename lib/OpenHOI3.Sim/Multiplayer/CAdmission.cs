namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// What a host made of a machine's first line (<see cref="CHostSession.Admit(string, string, System.Action{string}, bool, System.Collections.Generic.IReadOnlyList{string}?)"/>).
/// </summary>
/// <param name="Name">The name the machine gave; null where it was refused or the line was no asking.</param>
/// <param name="OtherFiles">Whether it was let in though its play checksum is not the host's, by the host's rule.</param>
public sealed record CAdmission(string? Name, bool OtherFiles);
