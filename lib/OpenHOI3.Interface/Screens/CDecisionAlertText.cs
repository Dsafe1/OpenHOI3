using System;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What an enhanced game's tick beside a decision says: whether the decision counts for the "decisions available"
/// alert. The tick is a window of the mod's own, <see cref="WindowName"/>, declared in its <c>interface/openhoi</c>
/// folder - which the original never opens, so that the original's row is as it was - and put into every row of the
/// decision list by the screen; a mod that declares none has no tick. See <c>docs/mechanics/events.md</c>, section 12.
/// </summary>
public static class CDecisionAlertText
{
	/// <summary>The window put into each row, at the place its declaration gives it in the row.</summary>
	public const string WindowName = "openhoi_decision_alert";

	/// <summary>Its button: a field that shows the mark while the decision counts.</summary>
	public const string ButtonName = "alert";

	public const string CountsKey = "OPENHOI_DECISION_ALERT_ON";
	public const string QuietKey = "OPENHOI_DECISION_ALERT_OFF";
	public const string AllKey = "OPENHOI_DECISION_ALERT_ALL";

	/// <summary>What the field shows: the mark while the decision counts for the alert, nothing once it is quiet.</summary>
	public static string Mark(bool counts) => counts ? "X" : "";

	/// <summary>The tick's tooltip: what it now says, and under the dashes what Ctrl does.</summary>
	public static CTooltip Tooltip(Func<string, string> localise, bool counts)
	{
		return new CTooltip { Text = localise(counts ? CountsKey : QuietKey), Delayed = localise(AllKey) };
	}
}
