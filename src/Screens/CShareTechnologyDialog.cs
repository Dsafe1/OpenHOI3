using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Screens;

/// <summary>
/// The dialog "Share Technology" opens, <c>ShareTechnologyDialog</c>: a row for each theory the recipient is
/// behind in, with its icon, name, our level and theirs - made once, never refreshed while open - one
/// chosen at a time, a second click un-choosing it; the description; and Accept, enabled with a theory
/// chosen, posting the share. It has no tooltips. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public sealed partial class CShareTechnologyDialog : Control
{
	public const string WindowName = "ShareTechnologyDialog";
	private const string RowName = "share_tech_category_entry";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private COffer _offer = null!;
	private readonly List<(CTechnologyCategory Category, CGuiButton? Box)> _rows = new();
	private CTechnologyCategory? _chosen;
	private CGuiButton? _agree;
	private CGuiTextBox? _description;

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
		if (state.Country(_offer.Actor) is not { } actor || state.Country(_offer.Recipient) is not { } recipient)
		{
			return;
		}
		Window.TryFindIcon("LeftShield")?.SetCountry(actor);
		Window.TryFindIcon("RightShield")?.SetCountry(recipient);
		if (Window.FindTextBox("Title") is { } title)
		{
			title.Text = CDiplomacyActions.Title(localise, _offer);
		}
		_description = Window.FindTextBox("Description");
		if (Window.FindListBox("tech_list") is { } list)
		{
			foreach (CTechnologyCategory category in CDiplomacyActions.ShareableTheories(state, actor, recipient))
			{
				if (CGuiWindow.CreateEntry(_context, RowName) is not { } row)
				{
					break;
				}
				row.TryFindIcon("tech_icon")?.SetSprite("icon_" + category.Name);
				Set(row, "info", localise(category.Name));
				Set(row, "our_ability", CShareTechnologyText.Our(actor, category));
				Set(row, "their_ability", CShareTechnologyText.Their(recipient, category));
				CGuiButton? box = row.TryFindButton("select_frame");
				int index = _rows.Count;
				if (box is not null)
				{
					box.Pressed += () => Choose(index);
				}
				_rows.Add((category, box));
				list.Add(row);
			}
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
		ShowChoice();
	}

	private static void Set(CGuiWindow row, string name, string text)
	{
		if (row.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	/// <summary>A row's box clicked: the chosen row un-chosen, any other chosen in its place.</summary>
	private void Choose(int index)
	{
		CTechnologyCategory category = _rows[index].Category;
		if (_chosen == category)
		{
			_chosen = null;
		}
		else
		{
			_chosen = category;
		}
		for (int row = 0; row < _rows.Count; row++)
		{
			if (_rows[row].Box is { } box)
			{
				box.Checked = _rows[row].Category == _chosen;
			}
		}
		ShowChoice();
	}

	/// <summary>The description of the chosen theory, and Agree pressable only with one chosen.</summary>
	private void ShowChoice()
	{
		CGameState state = _simulation.State;
		if (_description is not null && state.Country(_offer.Recipient) is { } recipient)
		{
			_description.Text = CShareTechnologyText.Description(_context.Localisation.Localise, state, recipient, _chosen);
		}
		if (_agree is not null)
		{
			_agree.Disabled = _chosen is null;
		}
	}

	/// <summary>The chosen theory written into the offer, dated now, and posted.</summary>
	private void Agree()
	{
		if (_chosen is null)
		{
			return;
		}
		_simulation.Post(new COfferCommand(_offer with { Category = _chosen.Name, Date = _simulation.State.TotalHours }));
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
