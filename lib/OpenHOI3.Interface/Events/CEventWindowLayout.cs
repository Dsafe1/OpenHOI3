using OpenHOI3.Content.Gui;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Events;

/// <summary>
/// Which event window an event is shown in, its option buttons' type and offset, and where each
/// button stands. See <c>docs/mechanics/events.md</c>, section 10.
/// </summary>
public static class CEventWindowLayout
{
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
