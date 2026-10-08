using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// One of the four windows a unit is ordered from, and the part all four share: the
/// order panel - how many lines and how many units a line, what that costs in IC, men
/// and days, a light for whether the production share can pay, and the two buttons that
/// order or close. Each window finds the panel's elements in its own file by the same
/// names. See <c>docs/mechanics/builders.md</c>, section 6.
/// </summary>
public abstract partial class CBuilderWindow : Control
{
	private static readonly (string Element, EBuilderLight Light)[] Lights =
	{
		("selected_brigade_green", EBuilderLight.Green), ("selected_brigade_orange", EBuilderLight.Orange),
		("selected_brigade_red", EBuilderLight.Red),
	};

	private readonly CGuiIcon?[] _lights = new CGuiIcon?[Lights.Length];
	private CGuiButton? _accept;
	private CGuiButton? _serialPlus;
	private CGuiButton? _serialMinus;
	private CGuiButton? _parallelPlus;
	private CGuiButton? _parallelMinus;
	private CGuiTextBox? _serialValue;
	private CGuiTextBox? _parallelValue;
	private CGuiTextBox? _icCost;
	private CGuiTextBox? _manpowerCost;
	private CGuiTextBox? _buildTime;

	private CGuiButton? _repeating;
	private double _sinceRepeat;
	private bool _firstRepeat;

	protected CGuiContext Context { get; private set; } = null!;
	protected CSimulation Simulation { get; private set; } = null!;

	/// <summary>The window, or null when no file declares it.</summary>
	protected CGuiWindow? Window { get; private set; }

	/// <summary>The country ordering.</summary>
	public CCountry Player { get; set; } = null!;

	/// <summary>How many units each line builds, one after another.</summary>
	protected int Serial { get; private set; } = CBuilderText.FewestOrdered;

	/// <summary>How many lines the order makes.</summary>
	protected int Parallel { get; private set; } = CBuilderText.FewestOrdered;

	protected abstract string WindowName { get; }

	/// <summary>Raised when the window comes down, whether an order was given or not.</summary>
	public event Action? Closed;

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

		Window = CGuiWindow.Create(Context, WindowName);
		if (Window is null)
		{
			return;
		}
		AddChild(Window);

		for (int index = 0; index < Lights.Length; index++)
		{
			_lights[index] = Window.FindIcon(Lights[index].Element);
		}
		_accept = Window.FindButton("accept");
		_serialPlus = Window.FindButton("serial_plus");
		_serialMinus = Window.FindButton("serial_minus");
		// The files spell the two parallel buttons differently, and so did the original.
		_parallelPlus = Window.FindButton("parallell_plus");
		_parallelMinus = Window.FindButton("parallel_minus");
		_serialValue = Window.FindTextBox("serial_value");
		_parallelValue = Window.FindTextBox("paralell_value");
		_icCost = Window.FindTextBox("ic_cost_value");
		_manpowerCost = Window.FindTextBox("mp_cost_value");
		_buildTime = Window.FindTextBox("build_time_value");

		if (_accept is not null)
		{
			_accept.SetText(Context.Localisation.Localise("START_PRODUCTION"));
			_accept.Pressed += () =>
			{
				Order();
				Close();
			};
		}
		if (Window.FindButton("cancel") is { } cancel)
		{
			cancel.Pressed += Close;
		}
		if (Window.FindTextBox("ic_cost_label") is { } label)
		{
			label.Text = Context.Localisation.Localise("PW_IC_COST");
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
		Opened();
		Refresh();
	}

	public void Close()
	{
		if (!Visible)
		{
			return;
		}
		Visible = false;
		_repeating = null;
		Closed?.Invoke();
	}

	/// <summary>
	/// Does what pressing <c>accept</c> would, if the button could be pressed now, and
	/// answers whether it could.
	/// </summary>
	public bool Accept()
	{
		if (!Visible || Window is null)
		{
			return false;
		}
		Refresh();
		if (_accept is null || _accept.Disabled)
		{
			return false;
		}
		Order();
		Close();
		return true;
	}

	/// <summary>Every frame while it is up, as the original does.</summary>
	public override void _Process(double delta)
	{
		if (!Visible || Window is null)
		{
			return;
		}
		Repeat(delta);
		Refresh();
	}

	private void Refresh()
	{
		Update();

		CConstruction? quote = Quote();
		CFixedPoint daily = quote?.Cost ?? CFixedPoint.Zero;
		CFixedPoint men = quote?.Manpower ?? CFixedPoint.Zero;
		CFixedPoint duration = quote?.Duration ?? CFixedPoint.Zero;

		Set(_icCost, CBuilderText.IcCost(daily, Parallel));
		Set(_manpowerCost, CBuilderText.ManpowerCost(men, Serial, Parallel));
		Set(_buildTime, CBuilderText.BuildTime(Context.Localisation.Localise, duration, Serial));
		Set(_serialValue, Serial.ToString(System.Globalization.CultureInfo.InvariantCulture));
		Set(_parallelValue, Parallel.ToString(System.Globalization.CultureInfo.InvariantCulture));

		EBuilderLight light = CBuilderText.Light(Player, CBuilderText.Total(daily, Parallel));
		for (int index = 0; index < Lights.Length; index++)
		{
			if (_lights[index] is { } icon)
			{
				icon.Visible = Lights[index].Light == light;
			}
		}

		Disable(_serialMinus, !CBuilderText.MayStepDown(Serial));
		Disable(_serialPlus, !CBuilderText.MayStepUp(Serial));
		Disable(_parallelMinus, !CBuilderText.MayStepDown(Parallel));
		Disable(_parallelPlus, !CBuilderText.MayStepUp(Parallel));
		Disable(_accept, quote is null
			|| Player.Manpower.Raw < CBuilderText.Total(men, Serial * Parallel).Raw
			|| !MayOrder());
	}

	/// <summary>
	/// A held button acts at once, again after a wait, and then quickly. With shift
	/// down it steps by ten.
	/// </summary>
	private void Repeat(double delta)
	{
		CGuiButton? down = null;
		foreach (CGuiButton? button in new[] { _serialPlus, _serialMinus, _parallelPlus, _parallelMinus })
		{
			if (button is { IsDown: true })
			{
				down = button;
			}
		}
		if (down is null)
		{
			_repeating = null;
			return;
		}
		if (!ReferenceEquals(down, _repeating))
		{
			_repeating = down;
			_firstRepeat = true;
			_sinceRepeat = 0;
			Step(down);
			return;
		}
		_sinceRepeat += delta;
		if (_sinceRepeat > (_firstRepeat ? CBuilderText.FirstRepeatSeconds : CBuilderText.RepeatSeconds))
		{
			_sinceRepeat = 0;
			_firstRepeat = false;
			Step(down);
		}
	}

	private void Step(CGuiButton button)
	{
		bool longStep = Input.IsKeyPressed(Key.Shift);
		if (ReferenceEquals(button, _serialPlus) && CBuilderText.MayStepUp(Serial))
		{
			Serial = CBuilderText.Stepped(Serial, 1, longStep);
		}
		else if (ReferenceEquals(button, _serialMinus))
		{
			Serial = CBuilderText.Stepped(Serial, -1, longStep);
		}
		else if (ReferenceEquals(button, _parallelPlus) && CBuilderText.MayStepUp(Parallel))
		{
			Parallel = CBuilderText.Stepped(Parallel, 1, longStep);
		}
		else if (ReferenceEquals(button, _parallelMinus))
		{
			Parallel = CBuilderText.Stepped(Parallel, -1, longStep);
		}
	}

	/// <summary>Finds the window's own elements, once.</summary>
	protected abstract void Build(CGuiWindow window);

	/// <summary>The window has just gone up.</summary>
	protected virtual void Opened()
	{
	}

	/// <summary>Fills the window's own elements, every frame.</summary>
	protected virtual void Update()
	{
	}

	/// <summary>What one unit of what is chosen would cost, or null when nothing is chosen.</summary>
	protected abstract CConstruction? Quote();

	/// <summary>Whether what is chosen may be ordered, so many lines of so many.</summary>
	protected abstract bool MayOrder();

	/// <summary>Posts the order.</summary>
	protected abstract void Order();

	protected static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null)
		{
			box.Text = text;
		}
	}

	protected static void Disable(CGuiButton? button, bool disabled)
	{
		if (button is not null && button.Disabled != disabled)
		{
			button.Disabled = disabled;
		}
	}
}
