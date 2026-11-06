namespace OpenHOI3.Gui;

/// <summary>
/// What an element says of its own greying: nothing, so that whatever holds it decides, or greyed or enabled for itself
/// and all it holds. See <see cref="CGuiObject.Greying"/>.
/// </summary>
public enum EGreying
{
	Inherit,
	Greyed,
	Enabled,
}
