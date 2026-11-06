using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// The clicks, keys and waits of <c>--click=</c>, played into a node's viewport as a hand would make them: the session's and
/// the front end's. What an <c>h48</c> lets go by is the host's to run, an hour at a time.
/// </summary>
public sealed class CClickScript
{
	private readonly Node _host;
	private readonly System.Func<bool> _runHour;

	/// <param name="runHour">Runs one hour, answering whether the game may go on; null where there is no game.</param>
	public CClickScript(Node host, System.Func<bool>? runHour = null)
	{
		_host = host;
		_runHour = runHour ?? (() => false);
	}

	/// <summary>
	/// Clicks the left mouse button at each of some points of the 1920 x 1080 canvas, in
	/// turn, as a hand would: the pointer moves there, the button goes down and comes up.
	/// The clicks go in at the same door as real ones, so what would stop a real click
	/// stops these.
	/// </summary>
	public async System.Threading.Tasks.Task Run(string points)
	{
		foreach (string entry in points.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
		{
			// h48 is not a click: it lets that many hours go by, so that what a click set
			// going can be seen further on.
			if (entry.StartsWith('h') && int.TryParse(entry[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
			{
				// An hour to a frame, as the game runs them, and nothing told to redraw: what
				// the screen shows afterwards is what a player would see.
				for (int hour = 0; hour < hours; hour++)
				{
					await Frames(1);
					if (!_runHour())
					{
						break;
					}
				}
				await Frames(2);
				continue;
			}
			// type:Text types each character of the text, as a key with its character, into what has the keyboard; bs,
			// del, left, right, home, end and enter press those keys.
			if (entry.StartsWith("type:", System.StringComparison.Ordinal) || entry is "bs" or "del" or "left" or "right" or "home"
				or "end" or "enter")
			{
				var pressed = new List<(Key Key, long Unicode)>();
				if (entry.StartsWith("type:", System.StringComparison.Ordinal))
				{
					pressed.AddRange(entry["type:".Length..].Select(typed => (Key.None, (long)typed)));
				}
				else
				{
					pressed.Add((entry switch
					{
						"bs" => Key.Backspace,
						"del" => Key.Delete,
						"left" => Key.Left,
						"right" => Key.Right,
						"home" => Key.Home,
						"end" => Key.End,
						_ => Key.Enter,
					}, 0));
				}
				await Frames(2);
				foreach ((Key key, long unicode) in pressed)
				{
					_host.GetViewport().PushInput(new InputEventKey { Keycode = key, Unicode = unicode, Pressed = true }, true);
					await Frames(1);
					_host.GetViewport().PushInput(new InputEventKey { Keycode = key, Unicode = unicode, Pressed = false }, true);
					await Frames(1);
				}
				await Frames(2);
				continue;
			}
			// kH is not a click either: it presses and lets go of the key of that letter or digit, and K4 the same with
			// Shift held - K4 opens the debug menu.
			// ck1 the same with Ctrl held: Ctrl and a digit keeps a selection group.
			// esc the same with Escape, which opens the in-game menu.
			bool withCtrl = entry.Length == 3 && entry[0] == 'c';
			string keyEntry = withCtrl ? entry[1..] : entry;
			if (entry == "esc" || keyEntry.Length == 2 && keyEntry[0] is 'k' or 'K' && char.IsLetterOrDigit(keyEntry[1]))
			{
				Key key = entry == "esc" ? Key.Escape : (Key)char.ToUpperInvariant(keyEntry[1]);
				bool shifted = keyEntry[0] == 'K';
				await Frames(2);
				_host.GetViewport().PushInput(new InputEventKey
				{
					Keycode = key, PhysicalKeycode = key, ShiftPressed = shifted, CtrlPressed = withCtrl, Pressed = true,
				}, true);
				await Frames(1);
				_host.GetViewport().PushInput(new InputEventKey
				{
					Keycode = key, PhysicalKeycode = key, ShiftPressed = shifted, CtrlPressed = withCtrl, Pressed = false,
				}, true);
				await Frames(2);
				continue;
			}
			// w2 waits two seconds of real time, which a tooltip's second half waits on.
			if (entry.StartsWith('w') && double.TryParse(entry[1..], NumberStyles.Float, CultureInfo.InvariantCulture, out double wait))
			{
				await _host.ToSignal(_host.GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
				await Frames(2);
				continue;
			}
			// d700,310>900,500 holds the left button down at the first point, moves to the second
			// in ten steps and lets go there: a drag. sd lets go with Shift held, and D does not let go,
			// for a screenshot of the drag box.
			bool shiftDrag = entry.StartsWith("sd");
			string dragEntry = shiftDrag ? entry[1..] : entry;
			if (dragEntry.StartsWith('d') || dragEntry.StartsWith('D'))
			{
				bool letGo = dragEntry[0] == 'd';
				string[] ends = dragEntry[1..].Split('>');
				string[] a = ends[0].Split(',');
				string[] b = ends.Length == 2 ? ends[1].Split(',') : System.Array.Empty<string>();
				if (a.Length == 2 && b.Length == 2
					&& float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)
					&& float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay)
					&& float.TryParse(b[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float bx)
					&& float.TryParse(b[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float by))
				{
					var from = new Vector2(ax, ay);
					var to = new Vector2(bx, by);
					await Frames(3);
					_host.GetViewport().PushInput(new InputEventMouseMotion { Position = from, GlobalPosition = from }, true);
					await Frames(1);
					_host.GetViewport().PushInput(new InputEventMouseButton
					{
						Position = from, GlobalPosition = from, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left,
					}, true);
					for (int step = 1; step <= 10; step++)
					{
						await Frames(1);
						Vector2 there = from.Lerp(to, step / 10f);
						_host.GetViewport().PushInput(new InputEventMouseMotion { Position = there, GlobalPosition = there, ButtonMask = MouseButtonMask.Left }, true);
					}
					await Frames(1);
					if (letGo)
					{
						_host.GetViewport().PushInput(new InputEventMouseButton
						{
							Position = to, GlobalPosition = to, ButtonIndex = MouseButton.Left, Pressed = false, ShiftPressed = shiftDrag,
						}, true);
					}
					await Frames(2);
				}
				else
				{
					GD.Print($"--click: '{entry}' is not a drag.");
				}
				continue;
			}
			// z+ before a point turns the wheel up there, which zooms the map in, and z- down; z+5 turns it five notches.
			if (entry.StartsWith("z+") || entry.StartsWith("z-"))
			{
				bool upward = entry[1] == '+';
				string rest = entry[2..];
				int notches = 1;
				int countEnd = rest.IndexOf(':');
				if (countEnd > 0 && int.TryParse(rest[..countEnd], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
				{
					notches = count;
					rest = rest[(countEnd + 1)..];
				}
				string[] to = rest.Split(',');
				if (to.Length == 2
					&& float.TryParse(to[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float zx)
					&& float.TryParse(to[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float zy))
				{
					var where = new Vector2(zx, zy);
					await Frames(3);
					_host.GetViewport().PushInput(new InputEventMouseMotion { Position = where, GlobalPosition = where }, true);
					for (int notch = 0; notch < notches; notch++)
					{
						await Frames(1);
						MouseButton wheel = upward ? MouseButton.WheelUp : MouseButton.WheelDown;
						_host.GetViewport().PushInput(new InputEventMouseButton { Position = where, GlobalPosition = where, ButtonIndex = wheel, Pressed = true }, true);
						_host.GetViewport().PushInput(new InputEventMouseButton { Position = where, GlobalPosition = where, ButtonIndex = wheel, Pressed = false }, true);
					}
					await Frames(2);
				}
				else
				{
					GD.Print($"--click: '{entry}' is not a point to turn the wheel at.");
				}
				continue;
			}
			// m before a point only moves the pointer there, which is how a tooltip is asked for.
			if (entry.StartsWith('m'))
			{
				string[] to = entry[1..].Split(',');
				if (to.Length == 2
					&& float.TryParse(to[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx)
					&& float.TryParse(to[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float my))
				{
					await Frames(3);
					_host.GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(mx, my), GlobalPosition = new Vector2(mx, my) }, true);
					await Frames(2);
				}
				else
				{
					GD.Print($"--click: '{entry}' is not a point to move to.");
				}
				continue;
			}
			// r before a point clicks the right button there, s holds shift down and c Ctrl,
			// and t1.5: before the point holds the button down that many seconds.
			string point = entry.TrimStart('r', 's', 'c');
			string keys = entry[..(entry.Length - point.Length)];
			MouseButton which = keys.Contains('r') ? MouseButton.Right : MouseButton.Left;
			bool shift = keys.Contains('s');
			bool ctrl = keys.Contains('c');
			double holdFor = 0;
			int colon = point.IndexOf(':');
			if (point.StartsWith('t') && colon > 0)
			{
				double.TryParse(point[1..colon], NumberStyles.Float, CultureInfo.InvariantCulture, out holdFor);
				point = point[(colon + 1)..];
			}
			string[] parts = point.Split(',');
			if (parts.Length != 2
				|| !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
				|| !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
			{
				GD.Print($"--click: '{point}' is not a point.");
				continue;
			}
			var at = new Vector2(x, y);
			await Frames(3);
			// A key held is told to the input singleton, which is what is asked whether it is down.
			if (ctrl)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = true });
			}
			if (shift)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = true });
			}
			_host.GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
			await Frames(1);
			_host.GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = true, ShiftPressed = shift,
				CtrlPressed = ctrl, ButtonMask = which == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left,
			}, true);
			if (holdFor > 0)
			{
				await _host.ToSignal(_host.GetTree().CreateTimer(holdFor), SceneTreeTimer.SignalName.Timeout);
			}
			else
			{
				await Frames(2);
			}
			_host.GetViewport().PushInput(new InputEventMouseButton
			{
				Position = at, GlobalPosition = at, ButtonIndex = which, Pressed = false, ShiftPressed = shift,
				CtrlPressed = ctrl,
			}, true);
			if (ctrl)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, PhysicalKeycode = Key.Ctrl, Pressed = false });
			}
			if (shift)
			{
				Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });
			}
			await Frames(2);
		}
	}

	private async System.Threading.Tasks.Task Frames(int count)
	{
		for (int frame = 0; frame < count; frame++)
		{
			await _host.ToSignal(_host.GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
