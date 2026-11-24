namespace OpenHOI3.Interface.Map;

/// <summary>
/// What a custom map source shades by, which is also which map mode's button lists it: a building's level on the
/// infrastructure button, a resource's amount on the resources button, a supply figure on the supply button. See
/// <c>docs/mechanics/custommapmodes.md</c>.
/// </summary>
public enum ECustomMapKind
{
	Buildings,
	Resources,
	Supply,
}
