namespace OpenHOI3.Content.Launch;

/// <summary>
/// An engine the launcher may start, as the <c>engine.txt</c> in its folder describes it (<c>docs/launcher.md</c>, section 2).
/// </summary>
/// <param name="Folder">The engine's folder, absolute.</param>
/// <param name="Version">What a mod's <c>engine</c> line names it by, compared case and all.</param>
/// <param name="Executable">The program to start, absolute.</param>
/// <param name="Contract">The version of the command line it understands (<see cref="CLaunchContract"/>).</param>
public sealed record CEngine(string Folder, string Version, string Executable, int Contract);
