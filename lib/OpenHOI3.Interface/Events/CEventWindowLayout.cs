using OpenHOI3.Content.Gui;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Events;

/// <summary>
/// Which event window an event is shown in, its option buttons' type and offset, and where each
/// button stands. See <c>docs/mechanics/events.md</c>, section 10.
/// </summary>
public static class CEventWindowLayout
{
	/// <summary>
	/// Where a country event's window stands across the screen by an enhanced game's option, or null where it stays
	/// where its file puts it - which is every province event's window, and every window in a faithful game: nought at
	/// the left, and in the middle the half of what the screen is wider than the window (<c>docs/mechanics/gameoptions.md</c>,
	/// section 5).
	/// </summary>
	public static float? PlacedX(CEventScope scope, OpenHOI3.Content.Settings.EEventWindowPlace place, float windowWidth, float screenWidth)
	{
		if (scope.Province >= 1)
		{
			return null;
		}
		return place switch
		{
			OpenHOI3.Content.Settings.EEventWindowPlace.Left => 0f,
			OpenHOI3.Content.Settings.EEventWindowPlace.Centre => System.MathF.Floor((screenWidth - windowWidth) / 2f),
			_ => null,
		};
	}

	/// <summary>The window: the province's where the event's scope has a province, whatever the event.</summary>
	public static string WindowName(CEventScope scope)
	{
		return scope.Province >= 1 ? "Event_Province_Window" : "EventWindow";
	}

	public static string ButtonType(CEventScope scope)
	{
		return scope.Province >= 1 ? "EventPOptionButton" : "EventOptionButton";
	}

	public static string OffsetName(CEventScope scope)
	{
		return scope.Province >= 1 ? "event_province_option_offset" : "event_option_offset";
	}

	public static string NoPicture(CEventScope scope)
	{
		return scope.Province >= 1 ? "GFX_prov_event_no_image" : "GFX_event_no_image";
	}

	/// <summary>The picture's file, or null where the event names none.</summary>
	public static string? PicturePath(CEvent shown)
	{
		return shown.Picture.Length == 0 ? null : "gfx/pictures/events/" + shown.Picture + ".tga";
	}

	/// <summary>
	/// Option i of n: x the offset's, y the window's height less (n + 1 - i) of the offset's - so the
	/// last always stands two steps above the bottom and more grow upwards.
	/// </summary>
	public static CGuiPoint OptionPosition(int windowHeight, int count, int index, CGuiPoint offset)
	{
		return new CGuiPoint(offset.X, windowHeight - (count + 1 - index) * offset.Y);
	}
}
