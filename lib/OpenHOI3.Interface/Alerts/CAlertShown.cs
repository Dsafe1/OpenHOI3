using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Interface.Alerts;

/// <summary>An alert that is up, and what its icon says.</summary>
public sealed record CAlertShown(EAlert Alert, CTooltip Tooltip);
