using System.Diagnostics;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// Ours: for <c>--perf=SECONDS</c>, where a frame's time goes - the game's hours, the session's own work each frame and
/// the engine's drawing - written once a second and summed at the end, with the game's days a real minute. The drawing's
/// figures are of an earlier frame, as the engine measures them.
/// </summary>
public sealed class CFrameLog
{
	private readonly double _seconds;
	private readonly Rid _viewport;
	private readonly Stopwatch _wall = Stopwatch.StartNew();
	private readonly Part _second = new();
	private readonly Part _whole = new();
	private double _lastWritten;

	public CFrameLog(double seconds, Rid viewport)
	{
		_seconds = seconds;
		_viewport = viewport;
		RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
	}

	/// <summary>Whether the time asked for has gone by.</summary>
	public bool IsDone => _wall.Elapsed.TotalSeconds >= _seconds;

	/// <summary>One frame: its length, the game's share and the session's other work in milliseconds, and the hours run.</summary>
	public void Frame(double delta, double simulation, double session, int hours)
	{
		double renderCpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport)
			+ RenderingServer.GetFrameSetupTimeCpu();
		double renderGpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport);
		foreach (Part part in new[] { _second, _whole })
		{
			part.Add(delta * 1000, simulation, session, renderCpu, renderGpu, hours);
		}
		double now = _wall.Elapsed.TotalSeconds;
		if (now - _lastWritten >= 1)
		{
			// What the engine drew in the last frame, by its own count: what the drawing's CPU time is spent on.
			ulong calls = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
			ulong objects = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
			ulong primitives = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
			GD.Print("--perf " + _second.Line(now - _lastWritten)
				+ $"; last frame {calls} draw calls, {objects} objects, {primitives} primitives");
			_second.Clear();
			_lastWritten = now;
		}
	}

	/// <summary>The whole run's sums.</summary>
	public void WriteSummary()
	{
		GD.Print("--perf in all: " + _whole.Line(_wall.Elapsed.TotalSeconds));
	}

	private sealed class Part
	{
		private int _frames;
		private int _hours;
		private double _frame;
		private double _longest;
		private double _simulation;
		private double _session;
		private double _renderCpu;
		private double _renderGpu;

		public void Add(double frame, double simulation, double session, double renderCpu, double renderGpu, int hours)
		{
			_frames++;
			_hours += hours;
			_frame += frame;
			_longest = System.Math.Max(_longest, frame);
			_simulation += simulation;
			_session += session;
			_renderCpu += renderCpu;
			_renderGpu += renderGpu;
		}

		public void Clear()
		{
			_frames = _hours = 0;
			_frame = _longest = _simulation = _session = _renderCpu = _renderGpu = 0;
		}

		public string Line(double seconds)
		{
			int frames = System.Math.Max(_frames, 1);
			double daysAMinute = seconds > 0 ? _hours / 24.0 / seconds * 60 : 0;
			return $"{_frames / System.Math.Max(seconds, 1e-9):0.0} frames/s, {_hours} hours ({daysAMinute:0.0} days/min), "
				+ $"a frame {_frame / frames:0.0} ms (longest {_longest:0.0}): game {_simulation / frames:0.0}, "
				+ $"session {_session / frames:0.0}, draw cpu {_renderCpu / frames:0.0} gpu {_renderGpu / frames:0.0}";
		}
	}
}
