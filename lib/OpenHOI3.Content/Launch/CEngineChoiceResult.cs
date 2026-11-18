namespace OpenHOI3.Content.Launch;

/// <summary>The engine a choice of mods is played on, or why there is none (<see cref="CEngineChoice"/>).</summary>
/// <param name="Engine">The engine to start, or null.</param>
/// <param name="Problem">Why none may be started, said to the player; null where one may.</param>
public sealed record CEngineChoiceResult(CEngine? Engine, string? Problem);
