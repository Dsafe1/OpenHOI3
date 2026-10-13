using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Screens;

/// <summary>
/// A line of the production screen's list of trades, a <c>production_trade_item</c> window: the
/// partner's flag and name, an icon for each good the player hands over in <c>purchase_box</c> - the
/// Export column - and for each he gets in <c>sales_box</c>, the inactive marker, and the cancel button.
/// No figure is on the line: an icon's tooltip says the amount. See <c>docs/mechanics/trade.md</c>,
/// section 13.
/// </summary>
public sealed class CTradeListEntry
{
	public const string WindowName = "production_trade_item";
	private const string GoodsWindowName = "production_trade_goods";

	private readonly CGuiIcon? _inactive;
	private readonly CGuiOverlappingBox? _gives;
	private readonly CGuiOverlappingBox? _gets;

	/// <summary>Raised when the cancel button is pressed.</summary>
	public event Action? CancelPressed;

	private CTradeListEntry(CGuiWindow window, CTradeRoute route)
	{
		Window = window;
		Route = route;
		_inactive = window.TryFindIcon("inactive_icon");
		_gives = window.FindBox("purchase_box");
		_gets = window.FindBox("sales_box");
		if (window.FindButton("cancel") is { } cancel)
		{
			cancel.Pressed += () => CancelPressed?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CTradeRoute Route { get; }

	public static CTradeListEntry? Create(CGuiContext context, CGameState state, CTradeRoute route, CCountryTag player)
	{
		if (CGuiWindow.CreateEntry(context, WindowName) is not { } window)
		{
			return null;
		}
		var line = new CTradeListEntry(window, route);
		Func<string, string> localise = context.Localisation.Localise;
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = CTradeListText.Name(state, route, player);
		}
		foreach (CTradeListText.CTradeIcon icon in CTradeListText.Icons(route, player))
		{
			if ((icon.PlayerGives ? line._gives : line._gets) is not { } box || GoodsIcon(context, icon) is not { } entry)
			{
				continue;
			}
			CTradeListText.CTradeIcon shown = icon;
			foreach (Node child in entry.GetChildren())
			{
				if (child is CGuiIcon picture)
				{
					picture.Tooltip = () => new CTooltip { Text = CTradeListText.IconTooltip(localise, state, route, player, shown) };
				}
			}
			box.Add(entry);
		}
		if (line._inactive is { } marker)
		{
			marker.Tooltip = () => new CTooltip
			{
				Text = CTradeListText.InactiveTooltip(localise),
				Delayed = CTradeListText.InactiveReason(localise, state, route),
			};
		}
		line.Update(player);
		return line;
	}

	/// <summary>A good's icon, the interface's <c>&lt;good&gt;_trade_icon</c>, in a window of its own.</summary>
	private static CGuiWindow? GoodsIcon(CGuiContext context, CTradeListText.CTradeIcon icon)
	{
		if (context.Types.Find(CTradeListText.IconName(icon.Goods)) is not CGuiIconType type
			|| CGuiWindow.CreateEntry(context, GoodsWindowName) is not { } window)
		{
			return null;
		}
		var picture = new CGuiIcon();
		picture.Initialise(type, context);
		window.AddChild(picture);
		return window;
	}

	/// <summary>
	/// Every frame: the inactive marker shown while the route did not run, a column's start and 32 to the
	/// right - the Import column's where the player is the one the reason names.
	/// </summary>
	public void Update(CCountryTag player)
	{
		if (_inactive is null)
		{
			return;
		}
		_inactive.Visible = Route.Inactive;
		if (!Route.Inactive)
		{
			return;
		}
		if ((CTradeListText.InactiveOnImport(Route, player) ? _gets : _gives) is { } box)
		{
			_inactive.Position = box.Position + new Vector2(CTradeListText.InactiveOffset, 0f);
		}
	}
}
