using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The lend-lease window the production screen's "Distribute" opens, <c>lendlease_distribution_window</c>:
/// a row for each receiver in the order the giving began - flag, name, a slider of its share and the IC
/// that is - the sliders kept at a hundred percent by equal parts, the whole set posted at most once a
/// second after the player moves one, and each row's cancel asking first. Locked sliders stay locked over
/// a rebuild. It has no tooltips. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public sealed partial class CLendLeaseWindow : Control
{
	public const string WindowName = "lendlease_distribution_window";
	private const string RowName = "lendlease_country_entry";

	/// <summary>How long the posting of a move waits for more: a second.</summary>
	private const double PostEvery = 1.0;

	private sealed class CRow
	{
		public required CCountryTag Receiver { get; init; }
		public required CGuiWindow Window { get; init; }
		public CGuiScrollbar? Slider { get; init; }
		public CGuiTextBox? Ic { get; init; }
		public bool Moved { get; set; }
	}

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private readonly List<CRow> _rows = new();
	private bool _changed;
	private double _sinceSent = PostEvery;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("country_list");
		if (_window.FindButton("close") is { } close)
		{
			close.Pressed += () => Visible = false;
		}
		Visible = false;
	}

	/// <summary>"Distribute": the window shown, its rows made again, the locked ones locked again.</summary>
	public void Open()
	{
		Visible = true;
		Rebuild();
	}

	private void Rebuild()
	{
		if (_list is null)
		{
			return;
		}
		var locked = _rows.Where(row => row.Slider is { Locked: true }).Select(row => row.Receiver).ToHashSet();
		_list.Clear();
		_rows.Clear();
		foreach (CLendLeaseGift gift in _player.LendLeaseGiving)
		{
			if (CGuiWindow.CreateEntry(_context, RowName) is not { } window || _simulation.State.Country(gift.Receiver) is not { } receiver)
			{
				continue;
			}
			window.TryFindIcon("flag")?.SetCountry(receiver);
			if (window.FindTextBox("country_name") is { } name)
			{
				name.Text = receiver.Name;
			}
			var row = new CRow
			{
				Receiver = gift.Receiver,
				Window = window,
				Slider = window.FindScrollbar("actual_slider"),
				Ic = window.FindTextBox("actual_IC"),
			};
			if (row.Slider is { } slider)
			{
				slider.Value = new CFixedPoint(gift.Weight);
				slider.Locked = locked.Contains(gift.Receiver);
				slider.LockPressed += () => slider.Locked = !slider.Locked;
				slider.Moved += _ =>
				{
					row.Moved = true;
					_changed = true;
				};
			}
			if (window.FindButton("cancel") is { } cancel)
			{
				cancel.Pressed += () => AskToCancel(receiver);
			}
			if (row.Ic is not null)
			{
				row.Ic.Text = "";
			}
			_rows.Add(row);
			_list.Add(window);
		}
	}

	/// <summary>A row's cancel: another prompt each press.</summary>
	private void AskToCancel(CCountry receiver)
	{
		var prompt = new CLendLeasePrompt();
		prompt.Setup(_context, _simulation, _player.Tag, receiver);
		AddChild(prompt);
	}

	/// <summary>
	/// Every frame it is up: made again where the receivers are not the rows; else the sliders spread to a
	/// hundred and each row's IC set; and a move posted once a second has passed since the last post.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}
		if (_rows.Count != _player.LendLeaseGiving.Count)
		{
			Rebuild();
			return;
		}
		long[] values = _rows.Select(row => row.Slider?.Value.Raw ?? 0).ToArray();
		bool[] free = _rows.Select(row => row.Slider is { } slider && !slider.Held && !slider.Locked && !row.Moved).ToArray();
		CLendLeaseText.Spread(values, free);
		for (int index = 0; index < _rows.Count; index++)
		{
			CRow row = _rows[index];
			row.Moved = false;
			if (row.Slider is { } slider && free[index])
			{
				slider.Value = new CFixedPoint(values[index]);
			}
			if (row.Ic is not null)
			{
				row.Ic.Text = CLendLeaseText.ActualIc(_player, values[index]);
			}
		}
		_sinceSent += delta;
		if (_changed && _sinceSent >= PostEvery)
		{
			_changed = false;
			_sinceSent = 0;
			_simulation.Post(new CChangeLendLeaseDistributionCommand(_player.Tag,
				_rows.Select(row => (row.Receiver, row.Slider?.Value.Raw ?? 0))));
		}
	}
}
