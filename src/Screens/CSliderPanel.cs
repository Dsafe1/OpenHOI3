using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// The sliders of one screen: the windows down the screen, the amounts they hold
/// between them, and the question of when what the player has set is sent to the game.
/// The original has this twice, once in each screen; here both screens have one of
/// these. See <c>docs/mechanics/sliders.md</c>, sections 3 and 4.
/// </summary>
public sealed class CSliderPanel
{
	/// <summary>What the player sets is sent no more often than this, as in the original.</summary>
	private const double SecondsBetweenSends = 1.0;

	private readonly CEconomySlider?[] _sliders;
	private readonly CSliderGroup _group;
	private CFixed15[] _known;
	private CFixed15[]? _sent;
	private bool _changed;
	private double _sinceSent = SecondsBetweenSends;

	/// <param name="order">Which share each slider down the screen is, from the top.</param>
	/// <param name="title">The title of the slider for a share.</param>
	public CSliderPanel(CGuiContext context, CGuiWindow parent, string sliderWindow, string startPosition,
		string offsetPosition, int[] order, Func<int, string> title)
	{
		_group = new CSliderGroup(order.Length);
		_sliders = new CEconomySlider?[order.Length];
		_known = new CFixed15[order.Length];

		CGuiPositionType? start = context.Types.FindPosition(startPosition);
		CGuiPositionType? offset = context.Types.FindPosition(offsetPosition);
		for (int row = 0; row < order.Length; row++)
		{
			int share = order[row];
			CEconomySlider? slider = CEconomySlider.Create(context, sliderWindow, title(share));
			_sliders[share] = slider;
			if (slider is null)
			{
				continue;
			}
			slider.Window.MoveTo(new Vector2(
				(start?.Position.X ?? 0) + row * (offset?.Position.X ?? 0),
				(start?.Position.Y ?? 0) + row * (offset?.Position.Y ?? 0)));
			parent.AddChild(slider.Window);

			slider.Moved += value => Move(share, value);
			slider.LockPressed += () => _group.SetHeld(share, !_group.IsHeld(share));
			slider.NeedPressed += () => NeedPressed?.Invoke(share);
		}
	}

	/// <summary>Every slider greyed, enabled, or left to the window it stands in (<see cref="CGuiObject.Greying"/>).</summary>
	public void SetGreying(EGreying greying)
	{
		foreach (CEconomySlider? slider in _sliders)
		{
			if (slider is not null)
			{
				slider.Window.Greying = greying;
			}
		}
	}

	/// <summary>Raised when the button under a slider's title is pressed, with which share it is.</summary>
	public event Action<int>? NeedPressed;

	/// <summary>
	/// Gives each slider's bar a tooltip: what its share would do at the amount the slider
	/// holds, and on the slider itself, delayed, whether a right click locks or unlocks it.
	/// See <c>docs/mechanics/sliders.md</c>, section 6.
	/// </summary>
	/// <param name="text">What a share would do at an amount.</param>
	/// <param name="lockLine">The line for a slider locked or not.</param>
	public void SetTooltips(Func<int, CFixedPoint, string> text, Func<bool, string> lockLine)
	{
		for (int share = 0; share < _sliders.Length; share++)
		{
			if (_sliders[share]?.Scrollbar is not { } bar)
			{
				continue;
			}
			int which = share;
			bar.Tooltip = () => new CTooltip
			{
				Text = text(which, _group[which]),
				Delayed = bar.MouseOnSlider ? lockLine(_group.IsHeld(which)) : "",
			};
		}
	}

	public CSliderGroup Group => _group;

	/// <summary>Each slider locked or not as the country's mirror says, as the original's are when its sliders are made.</summary>
	public void LoadLocks(Func<int, bool> locked)
	{
		for (int share = 0; share < _sliders.Length; share++)
		{
			_group.SetHeld(share, locked(share));
		}
	}

	/// <summary>
	/// The need button: the slider put at its need and half a step, and locked - pressed on a slider already locked, moved
	/// to its need and left locked. On these screens it never lets go (<c>docs/mechanics/sliders.md</c>, section 3).
	/// </summary>
	public void PinAtNeed(int share, CFixedPoint need)
	{
		CFixedPoint halfStep = _sliders[share]?.Scrollbar is { } bar ? new CFixedPoint(bar.StepSize.Raw / 2) : CFixedPoint.Zero;
		_group.SetHeld(share, false);
		Move(share, need + halfStep);
		_group.SetHeld(share, true);
	}

	/// <summary>The player moves a slider, by hand or by its button. The others make room.</summary>
	public void Move(int share, CFixedPoint value)
	{
		_group.Move(share, value);
		_changed = true;
	}

	/// <summary>
	/// Keeps the sliders in step with the country: reloads them when its shares or the
	/// total were changed by something other than this panel, and otherwise leaves the
	/// player's setting standing until the game has taken it.
	/// </summary>
	public void Follow(CFixedPoint total, ReadOnlySpan<CFixed15> shares)
	{
		if (_sent is not null && Same(shares, _sent))
		{
			_known = _sent;                                  // the game has what was sent
			_sent = null;
		}
		if (_changed)
		{
			return;                                          // the player's setting stands until it is sent
		}

		// While something sent has yet to be taken, that is what the sliders show.
		bool moved = _sent is null && !Same(shares, _known);
		if (!moved && total == _group.Total)
		{
			return;
		}
		if (_sent is null)
		{
			_known = shares.ToArray();
		}
		_group.Load(total, _sent ?? _known);
	}

	/// <summary>
	/// Holds a slider to a limit, as the original's production screen does each frame it is open: one over it is put down
	/// and the free ones take up the rest, and what that makes is sent to the game as a setting of the player's would be
	/// (<c>reversing/findings/FINDINGS-lendleaseslider.md</c>, section 3).
	/// </summary>
	public void HoldToLimit(int share, CFixedPoint? limit)
	{
		_group.SetLimit(share, limit);
		if (_group.ClampToLimits())
		{
			_changed = true;
		}
	}

	/// <summary>Draws one slider: its place, its lock and the two lines of text.</summary>
	public void Show(int share, string value, string need)
	{
		_sliders[share]?.Show(_group.Total, _group[share], _group.IsHeld(share), value, need);
	}

	/// <summary>
	/// Whether it is time to send what the player has set: something changed, and a
	/// second has gone since the last time. Answers the shares to send, once.
	/// </summary>
	public CFixed15[]? Due(double delta)
	{
		_sinceSent += delta;
		if (!_changed || _sinceSent < SecondsBetweenSends)
		{
			return null;
		}
		return Take();
	}

	/// <summary>What the player has set and the game has not been told, at once - for when the screen comes down.</summary>
	public CFixed15[]? Flush()
	{
		return _changed ? Take() : null;
	}

	private CFixed15[] Take()
	{
		_changed = false;
		_sinceSent = 0.0;
		_sent = _group.Shares();
		return _sent;
	}

	private static bool Same(ReadOnlySpan<CFixed15> a, ReadOnlySpan<CFixed15> b)
	{
		return a.SequenceEqual(b);
	}
}
