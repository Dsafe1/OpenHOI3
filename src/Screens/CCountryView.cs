using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Sim;

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
	private CCountry? _skinned;

	protected CGuiContext Context { get; private set; } = null!;
	protected CSimulation Simulation { get; private set; } = null!;

	/// <summary>The main window, or null when no file declares it.</summary>
	protected CGuiWindow? Window { get; private set; }

	/// <summary>The country the screen is about.</summary>
	public CCountry Player { get; set; } = null!;

	/// <summary>The names of the two windows: the main one, and the one behind it.</summary>
	protected abstract string WindowName { get; }
	protected abstract string TopColourName { get; }

	/// <summary>Raised when the screen goes up or comes down.</summary>
	public event Action? Toggled;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		Context = context;
		Simulation = simulation;
		Player = player;
	}

	public override void _Ready()
	{
		Name = WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;

		_topColour = CGuiWindow.Create(Context, TopColourName);
		if (_topColour is not null)
		{
			AddChild(_topColour);

			// Its one picture is a strip 64 wide that fades downwards: a haze of the
			// screen's colour under the top bar. Drawn once it would be a stripe in the
			// corner, so it is drawn across the whole screen. How the original spreads
			// it was not read.
			_topColour.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			if (_topColour.TryFindIcon("topcolor_icon") is { } haze)
			{
				haze.AnchorRight = 1f;
				haze.OffsetRight = 0f;
				haze.MouseFilter = MouseFilterEnum.Ignore;
			}
		}
		Window = CGuiWindow.Create(Context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);

		if (Window.TryFindButton("close_button") is { } close)
		{
			close.Pressed += Close;
		}
		// There is no AI to hand a screen to, so the line always reads the same.
		if (Window.FindTextBox("automation_setting") is { } automation)
		{
			automation.Text = Context.Localisation.Localise("OPT_NORMAL");
		}
		Build(Window);
	}

	public bool IsOpen => Visible;

	public void Open()
	{
		if (Window is null || Visible)
		{
			return;
		}
		Visible = true;
		ShowSkin();
		Opened();
		Refresh();
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
			Tick(delta);
		}
	}

	/// <summary>
	/// The picture behind the screen is the country's own when the install has one:
	/// <c>gfx/interface/skins/country_bg_TAG.dds</c>. Otherwise whatever the icon's
	/// sprite names.
	/// </summary>
	private void ShowSkin()
	{
		if (ReferenceEquals(Player, _skinned) || Window is null)
		{
			return;
		}
		_skinned = Player;
		if (Window.TryFindIcon(SkinIcon) is not { } icon)
		{
			return;
		}
		string file = SkinFolder + Player.Tag + ".dds";
		if (Context.Textures.Exists(file) && Context.Textures.LoadFromGamePath(file) is { } skin)
		{
			icon.SetTexture(skin);
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
