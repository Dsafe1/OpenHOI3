using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Screens;

/// <summary>
/// What the screens the top bar opens have in common, as in the original, where six of
/// them share a base: a main window, a second window behind it for the colour along
/// the top, a picture that follows the country, a button that closes it and a line
/// saying who runs it. See <c>docs/mechanics/countryviews.md</c>.
/// </summary>
public abstract partial class CCountryView : Control
{
	private const string SkinFolder = "gfx/interface/skins/country_bg_";
	private const string SkinIcon = "bg_countryskin";

	private CGuiWindow? _topColour;
	private bool _skinned;

	protected CGuiContext Context { get; private set; } = null!;
	protected CSimulation Simulation { get; private set; } = null!;

	/// <summary>The main window, or null when no file declares it.</summary>
	protected CGuiWindow? Window { get; private set; }

	/// <summary>The country the screen is about.</summary>
	public CCountry Player { get; set; } = null!;

	/// <summary>
	/// The country whose picture stands behind the screen, given once: the one played unless set after
	/// <see cref="Setup"/>. The session sets the country the game was entered with, which a <c>tag</c> does not change
	/// (<c>countryviews.md</c>, section 2); null leaves the sprite's own.
	/// </summary>
	public CCountryTag? SkinCountry { get; set; }

	/// <summary>The names of the two windows: the main one, and the one behind it.</summary>
	protected abstract string WindowName { get; }
	protected abstract string TopColourName { get; }

	/// <summary>Raised when the screen goes up or comes down.</summary>
	public event Action? Toggled;

	/// <summary>Raised by the close button, for the game screen's toggle; with nobody listening the screen closes itself.</summary>
	public event Action? CloseRequested;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		Context = context;
		Simulation = simulation;
		Player = player;
		SkinCountry = player.Tag;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
	}

	/// <summary>Whether the windows are made: on the first opening, or the first thing asked of them.</summary>
	private bool _made;

	/// <summary>
	/// The screen's windows made and its elements found, once, as it is first opened - not as the game is entered, where the
	/// eight screens the top bar opens took a quarter of a second of the main thread, the window not answering
	/// (<c>docs/mechanics/loadingscreen.md</c>, section 4). *Ours*: when the original makes its screens was not read.
	/// </summary>
	protected void Make()
	{
		if (_made)
		{
			return;
		}
		_made = true;
		_topColour = CGuiWindow.Create(Context, TopColourName);
		if (_topColour is not null)
		{
			// The window at its declared size and place: its one picture, a tileSpriteType, is a quad of the size its sprite
			// declares, the strip repeated across it (CTileSprite) - the base game's under the top bar, BlackICE's standing up
			// from the window's declared foot (docs/mechanics/countryviews.md, section 6).
			AddChild(_topColour);
		}
		Window = CGuiWindow.Create(Context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);

		if (Window.TryFindButton("close_button") is { } close)
		{
			close.Pressed += () =>
			{
				if (CloseRequested is { } requested)
				{
					requested();
				}
				else
				{
					Close();
				}
			};
		}
		_automationLine = Window.FindTextBox("automation_setting");
		_arrowLeft = Window.TryFindButton("mode_arrow_left");
		_arrowRight = Window.TryFindButton("mode_arrow_right");
		_close = Window.TryFindButton("close_button");
		if (AutomationArea is { } area)
		{
			if (_arrowLeft is not null)
			{
				_arrowLeft.Pressed += () => StepAutomation(area, left: true);
			}
			if (_arrowRight is not null)
			{
				_arrowRight.Pressed += () => StepAutomation(area, left: false);
			}
		}
		Build(Window);
	}

	private CGuiTextBox? _automationLine;
	private CGuiButton? _arrowLeft;
	private CGuiButton? _arrowRight;
	private CGuiButton? _close;

	/// <summary>
	/// The area of the player's the screen's arrows hand to the AI or take back: diplomacy, production, technology,
	/// politics or intelligence; null for a screen with none.
	/// </summary>
	protected virtual EAutomation? AutomationArea => null;

	/// <summary>An arrow pressed: the area's next value, the left one up and the right one down, wrapping (frontend.md, 4.4).</summary>
	private void StepAutomation(EAutomation area, bool left)
	{
		CPlayerAutomation automation = Simulation.State.Automation;
		int count = CAutomationText.Values(area).Length;
		int value = CAutomationText.Step(Interface.Screens.CLobbyOptionsText.Value(automation, area), left, count);
		Simulation.Post(new CSetAutomationCommand(area, value));
	}

	/// <summary>
	/// The automation line, every frame: the area's setting written, the screen greyed as it says (<see
	/// cref="ApplyAutomation"/>), and the arrows and the close button enabled whatever it says - the original's slot 11.
	/// </summary>
	private void UpdateAutomationLine()
	{
		if (AutomationArea is not { } area || _automationLine is null)
		{
			return;
		}
		CPlayerAutomation automation = Simulation.State.Automation;
		_automationLine.Text = CAutomationText.Setting(Context.Localisation.Localise, automation, area);
		ApplyAutomation(automation);
		foreach (CGuiButton? button in new[] { _arrowLeft, _arrowRight, _close })
		{
			if (button is not null)
			{
				button.Greying = EGreying.Enabled;
			}
		}
	}

	/// <summary>
	/// What the AI running the screen's area takes from the player: the whole window, as the original's base does - greyed here where the original's only stops taking the mouse (compatibility.md, entry 19); each screen whose
	/// original says otherwise answers for itself (<c>docs/mechanics/frontend.md</c>, section 4.4).
	/// </summary>
	protected virtual void ApplyAutomation(CPlayerAutomation automation)
	{
		if (Window is not null && AutomationArea is { } area)
		{
			Window.Greying = automation.Has(area) ? EGreying.Greyed : EGreying.Inherit;
		}
	}

	/// <summary>Grey an element and all it holds, or leave it to what holds it.</summary>
	protected static void Grey(CGuiObject? element, bool greyed)
	{
		if (element is not null)
		{
			element.Greying = greyed ? EGreying.Greyed : EGreying.Inherit;
		}
	}

	public bool IsOpen => Visible;

	/// <summary>
	/// Puts the screen away behind a window of its own - the production screen's builders - or brings it back: its main
	/// window hidden, the one behind it left up, the screen still up. So the picture behind country screens stays, the
	/// minimap stays down and the outliner shut, as the original's view still answers that it is shown while a builder is
	/// (<c>docs/mechanics/builders.md</c>, section 1).
	/// </summary>
	protected void PutAway(bool away)
	{
		if (Window is not null)
		{
			Window.Visible = !away;
		}
	}

	public void Open()
	{
		Make();
		if (Window is null || Visible)
		{
			return;
		}
		Visible = true;
		ShowSkin();
		Opened();
		Refresh();
		UpdateAutomationLine();
		Toggled?.Invoke();
	}

	public void Close()
	{
		if (!Visible)
		{
			return;
		}
		Closing();
		Visible = false;
		Toggled?.Invoke();
	}

	/// <summary>Every frame while it is up, as the original does.</summary>
	public override void _Process(double delta)
	{
		if (Visible && Window is not null)
		{
			ShowSkin();
			Refresh();
			UpdateAutomationLine();
			Tick(delta);
		}
	}

	/// <summary>
	/// The picture behind the screen is the country's own when the install has one:
	/// <c>gfx/interface/skins/country_bg_TAG.dds</c>. Otherwise whatever the icon's
	/// sprite names. Given once, by <see cref="SkinCountry"/>: the original gives it as the game
	/// is entered and never again.
	/// </summary>
	private void ShowSkin()
	{
		if (_skinned || Window is null)
		{
			return;
		}
		_skinned = true;
		if (SkinCountry is not { } tag || Window.TryFindIcon(SkinIcon) is not { } icon)
		{
			return;
		}
		string file = SkinFolder + tag + ".dds";
		if (Context.Textures.Exists(file) && Context.Textures.LoadFromGamePath(file) is { } skin)
		{
			icon.ShowPicture(skin);
		}
	}

	/// <summary>Finds the screen's elements, once.</summary>
	protected abstract void Build(CGuiWindow window);

	/// <summary>Fills them.</summary>
	protected abstract void Refresh();

	/// <summary>The screen has just gone up.</summary>
	protected virtual void Opened()
	{
	}

	/// <summary>The screen is about to come down.</summary>
	protected virtual void Closing()
	{
	}

	/// <summary>Real time passing while the screen is up.</summary>
	protected virtual void Tick(double delta)
	{
	}

	protected static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}
}
