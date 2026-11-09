using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a unit is lent from or taken back in: <c>ExpeditionDialog</c>, its list of one kind of unit at a time with one unit
/// ticked across all three. See <c>docs/mechanics/expeditions.md</c>, section 1.
/// </summary>
public sealed partial class CExpeditionDialog : Control
{
	public const string WindowName = "ExpeditionDialog";

	private const string RowName = "expedition_entry";

	private readonly HashSet<int> _opened = new();
	private readonly List<(CUnit Unit, int Depth, CGuiButton? Box)> _rows = new();
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private COffer _offer = null!;
	private CCountry _player = null!;
	private CGuiListBox? _list;
	private CGuiButton? _agree;
	private CGuiTextBox? _description;
	private EExpeditionKind _kind = EExpeditionKind.Army;
	private CUnit? _ticked;

	/// <summary>The fleet still counted as chosen after its box is cleared, the original not taking a fleet off its list.</summary>
	private bool _emptied;

	public CGuiWindow? Window { get; private set; }

	public event Action? Closed;

	public void Setup(CGuiContext context, CSimulation simulation, COffer offer, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_offer = offer;
		_player = player;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Window = CGuiWindow.Create(_context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;
		if (state.Country(_offer.Actor) is { } actor)
		{
			Window.TryFindIcon("LeftShield")?.SetCountry(actor);
		}
		if (state.Country(_offer.Recipient) is { } recipient)
		{
			Window.TryFindIcon("RightShield")?.SetCountry(recipient);
		}
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CDiplomacyActions.Title(localise, _offer);
		}
		_description = Window.FindTextBox("Description");
		foreach (string hidden in new[] { "diplo_popup_chance_bg", "chance_string" })
		{
			if (Window.TryFindElement(hidden) is { } element)
			{
				element.Visible = false;
			}
		}
		Kind("ArmyButton", "UNIT_ARMY_NAME", EExpeditionKind.Army);
		Kind("AirButton", "UNIT_AIR_NAME", EExpeditionKind.Air);
		Kind("NavyButton", "UNIT_NAVY_NAME", EExpeditionKind.Navy);
		_list = Window.FindListBox("unitlist_list");
		_agree = Window.TryFindButton("AgreeButton");
		if (_agree is not null)
		{
			_agree.SetText(localise("ACCEPT"));
			_agree.Pressed += Agree;
		}
		if (Window.TryFindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("DECLINE"));
			decline.Pressed += Close;
		}
		Rebuild();
		Say();
	}

	private void Kind(string name, string key, EExpeditionKind kind)
	{
		if (Window?.TryFindButton(name) is { } button)
		{
			button.SetText(_context.Localisation.Localise(key));
			button.Pressed += () =>
			{
				_kind = kind;
				Rebuild();
			};
		}
	}

	private List<(CUnit Unit, int Depth)> Wanted() => CExpeditionText.Rows(_simulation.State, _player, _offer, _kind, _opened);

	private void Rebuild()
	{
		_rows.Clear();
		_list?.Clear();
		foreach ((CUnit unit, int depth) in Wanted())
		{
			if (CGuiWindow.CreateEntry(_context, RowName) is not { } row)
			{
				break;
			}
			row.FindTextBox("name")!.Text = unit.Name;
			row.FindTextBox("location")!.Text = _simulation.State.Province(unit.Location).Name;
			string sprite = CDeployMenuText.CounterSprite(unit);
			if (sprite.Length != 0)
			{
				row.TryFindIcon("counter_type")?.SetSprite(sprite);
			}
			row.TryFindIcon("counter_size")?.SetFrame(COobBrowserText.SizeFrame(unit));
			// The list places the row itself, so a deeper one has its contents moved in.
			foreach (Control part in row.GetChildren().OfType<Control>())
			{
				part.Position += new Vector2(depth * CExpeditionText.Indent, 0f);
			}
			if (row.TryFindButton("expand") is { } expand)
			{
				expand.Disabled = CMovementRules.IsLandUnit(unit) && unit.Children.Count == 0;
				expand.Pressed += () =>
				{
					if (!_opened.Add(unit.Id))
					{
						_opened.Remove(unit.Id);
					}
					Rebuild();
				};
			}
			CGuiButton? box = row.TryFindButton("select_frame");
			if (box is not null)
			{
				box.Disabled = unit.Kind == EUnitKind.Theatre;
				box.Checked = ReferenceEquals(unit, _ticked);
				box.Pressed += () => Tick(unit);
			}
			_rows.Add((unit, depth, box));
			_list?.Add(row);
		}
		Refresh();
	}

	/// <summary>One unit across all three kinds; a fleet's box cleared leaves it counted while the offer names nobody.</summary>
	private void Tick(CUnit unit)
	{
		if (ReferenceEquals(_ticked, unit) && !_emptied)
		{
			if (unit.Kind == EUnitKind.Navy)
			{
				_emptied = true;
			}
			else
			{
				_ticked = null;
			}
		}
		else
		{
			_ticked = unit;
			_emptied = false;
		}
		foreach ((CUnit shown, _, CGuiButton? box) in _rows)
		{
			if (box is not null)
			{
				box.Checked = !_emptied && (ReferenceEquals(shown, _ticked) || IsUnder(shown, _ticked));
			}
		}
		Say();
		Refresh();
	}

	private static bool IsUnder(CUnit unit, CUnit? top)
	{
		for (CUnit? above = unit.Parent; above is not null && top is not null; above = above.Parent)
		{
			if (ReferenceEquals(above, top))
			{
				return true;
			}
		}
		return false;
	}

	private void Say()
	{
		if (_description is not null)
		{
			Func<string, string> localise = _context.Localisation.Localise;
			_description.Text = CExpeditionText.Description(localise, _simulation.State, _offer, _ticked, hour => CDiplomacyActions.HourDateOf(localise, hour));
		}
	}

	private void Refresh()
	{
		if (_agree is not null)
		{
			_agree.Disabled = _ticked is null;
		}
	}

	public override void _Process(double delta)
	{
		// A unit that leaves the list while the window is up drops out at the next frame.
		if (Wanted().Count != _rows.Count)
		{
			Rebuild();
		}
	}

	private void Agree()
	{
		_simulation.Post(new COfferCommand(_offer with { Date = _simulation.State.TotalHours, Unit = _emptied ? 0 : _ticked?.Id ?? 0 }));
		Close();
	}

	public void Close()
	{
		if (IsQueuedForDeletion())
		{
			return;
		}
		Closed?.Invoke();
		QueueFree();
	}
}
