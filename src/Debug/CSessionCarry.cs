using System.Collections.Generic;
using Godot;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Debug;

/// <summary>What one session leaves the next when the console has every screen made again around the same game.</summary>
public sealed record CSessionCarry(IReadOnlyList<string> ConsoleLines, IReadOnlyList<string> ConsoleHistory, bool ConsoleOpen, Vector2 Centre,
	float Zoom, int RenderLayers, bool Paused, int Speed, CReplayFile? ReplayFile, bool Wireframe);
