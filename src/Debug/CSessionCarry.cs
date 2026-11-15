using System.Collections.Generic;
using Godot;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Debug;

/// <summary>What one session leaves the next when the console has every screen made again around the same game.</summary>
/// <param name="EnteredAs">The country the game was entered with, whose pictures the top bar and the country screens keep
/// whoever is played after a <c>tag</c> (<c>docs/mechanics/console.md</c>, section 3); null where nobody was.</param>
public sealed record CSessionCarry(IReadOnlyList<string> ConsoleLines, IReadOnlyList<string> ConsoleHistory, bool ConsoleOpen, Vector2 Centre,
	float Zoom, int RenderLayers, bool Paused, int Speed, CReplayFile? ReplayFile, bool Wireframe, CCountryTag? EnteredAs);
