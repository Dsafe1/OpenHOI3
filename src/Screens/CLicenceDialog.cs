using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The dialog "Buy Production Licence" opens, <c>LicenceDialog</c>: a row for each type the seller would
/// licence - its counter and the seller's model - one chosen at a time, a row greyed where the buyer
/// lacks the men for the order; the runs and the units a run, 1 to 99 by their buttons; the money a unit,
/// a share of the buyer's money; the description, the AI's likely answer, the debt line; and Accept,
/// enabled with a type chosen and the men there. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public sealed partial class CLicenceDialog : Control
{
	public const string WindowName = "LicenceDialog";
	private const string RowName = "prod_share_entry";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private COffer _offer = null!;
	private readonly List<(CSubUnitType Type, CGuiButton? Box)> _rows = new();
	private CSubUnitType? _chosen;
	private int _serial = CLicenceText.LeastRuns;
	private int _parallel = CLicenceText.LeastRuns;
	private CGuiScrollbar? _money;
	private CGuiButton? _agree;
	private string _shown = "";

	public CGuiWindow? Window { get; private set; }

	/// <summary>Raised when the dialog is closed.</summary>
	public event Action? Closed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, COffer offer)
	{
		_context = context;
		_simulation = simulation;
		_offer = offer;
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
		if (state.Country(_offer.Actor) is not { } buyer || state.Country(_offer.Recipient) is not { } seller)
		{
			return;
		}
		Window.TryFindIcon("LeftShield")?.SetCountry(buyer);
		Window.TryFindIcon("RightShield")?.SetCountry(seller);
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CDiplomacyActions.Title(localise, _offer);
		}
		if (Window.FindListBox("tech_list") is { } list)
		{
			foreach (CSubUnitType type in CLicenceText.Types(state, buyer, seller))
			{
				if (CGuiWindow.CreateEntry(_context, RowName) is not { } row)
				{
					break;
				}
				row.TryFindIcon("counter_type")?.SetSprite("GFX_counter_" + type.Key);
				if (row.FindTextBox("name") is { } name)
				{
					name.Text = CLicenceText.RowName(localise, state, seller, type);
				}
				CGuiButton? box = row.TryFindButton("select_frame");
				int index = _rows.Count;
				if (box is not null)
				{
					box.Pressed += () => Choose(index);
				}
				// Every element of the row but its counter answers with the row's tooltip.
				CSubUnitType shown = type;
				row.SetTooltips(element => element == "counter_type" ? CTooltip.Empty : new CTooltip
				{
					Text = CLicenceText.RowTooltip(localise, buyer, shown, CLicenceText.HasMen(buyer, shown, _serial, _parallel)),
				});
				_rows.Add((type, box));
				list.Add(row);
			}
		}
		_money = Window.FindScrollbar("money_slider");
		Step("serial_plus", () => _serial = Math.Min(CLicenceText.MostRuns, _serial + 1));
		Step("serial_minus", () => _serial = Math.Max(CLicenceText.LeastRuns, _serial - 1));
		Step("paralell_plus", () => _parallel = Math.Min(CLicenceText.MostRuns, _parallel + 1));
		Step("paralell_minus", () => _parallel = Math.Max(CLicenceText.LeastRuns, _parallel - 1));
		if (Window.TryFindElement("debt_desc") is { } debt)
		{
			debt.Visible = CGameState.Status(buyer, seller).AllowDebts && state.IsAtWar(buyer.Tag);
		}
		_agree = Window.FindButton("AgreeButton");
		if (_agree is not null)
		{
			_agree.SetText(localise("ACCEPT"));
			_agree.Pressed += Agree;
		}
		if (Window.FindButton("DeclineButton") is { } decline)
		{
			decline.SetText(localise("DECLINE"));
			decline.Pressed += Close;
		}
		Refresh();
	}

	private void Step(string button, Action step)
	{
		if (Window?.FindButton(button) is { } pressed)
		{
			pressed.Pressed += () =>
			{
				step();
				Refresh();
			};
		}
	}

	/// <summary>A row's box clicked: it becomes the chosen type and every other row is unticked.</summary>
	private void Choose(int index)
	{
		_chosen = _rows[index].Type;
		for (int row = 0; row < _rows.Count; row++)
		{
			if (row != index && _rows[row].Box is { } box)
			{
				box.Checked = false;
			}
		}
		Refresh();
	}

	/// <summary>The offer as the dialog stands: the chosen type, the runs and the money a unit.</summary>
	private COffer Current()
	{
		CGameState state = _simulation.State;
		CFixedPoint money = state.Country(_offer.Actor) is { } buyer
			? CLicenceText.MoneyAt(state, buyer, _money?.Value.Raw ?? 500)
			: CFixedPoint.Zero;
		return _offer with
		{
			Licence = _chosen is null ? null : new CLicenceTerms(_chosen.Key, _serial, _parallel, money),
		};
	}

	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>
	/// The texts, the rows' boxes and Accept, made again where anything they rest on has moved - ours: the
	/// original makes them again every frame, its own test of the AI's score being on a field nothing writes.
	/// </summary>
	private void Refresh()
	{
		if (Window is null || _simulation.State.Country(_offer.Actor) is not { } buyer)
		{
			return;
		}
		COffer offer = Current();
		string key = string.Join(",", _chosen?.Key ?? "", _serial, _parallel, offer.Licence?.Money.Raw ?? 0, buyer.Manpower.Raw);
		if (key == _shown)
		{
			return;
		}
		_shown = key;
		Func<string, string> localise = _context.Localisation.Localise;
		CGameState state = _simulation.State;
		Set("serial_value", _serial.ToString(CultureInfo.InvariantCulture));
		Set("paralell_value", _parallel.ToString(CultureInfo.InvariantCulture));
		Set("Description", CLicenceText.Description(localise, offer.Licence));
		string chance = offer.Licence is null ? "" : CDiplomacyActions.Chance(localise, state, offer);
		if (Window.FindTextBox("chance_string") is { } chanceText)
		{
			chanceText.Text = chance;
			chanceText.Visible = chance.Length > 0;
		}
		if (Window.TryFindIcon("diplo_popup_chance_bg") is { } chanceBackground)
		{
			chanceBackground.Visible = chance.Length > 0;
		}
		foreach ((CSubUnitType type, CGuiButton? box) in _rows)
		{
			if (box is not null)
			{
				box.Disabled = !CLicenceText.HasMen(buyer, type, _serial, _parallel);
			}
		}
		if (_agree is not null)
		{
			_agree.Disabled = _chosen is null || !CLicenceText.HasMen(buyer, _chosen, _serial, _parallel);
		}
	}

	private void Set(string name, string text)
	{
		if (Window?.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	/// <summary>The licence dated now and posted.</summary>
	private void Agree()
	{
		COffer offer = Current();
		if (offer.Licence is null)
		{
			return;
		}
		_simulation.Post(new COfferCommand(offer with { Date = _simulation.State.TotalHours }));
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
