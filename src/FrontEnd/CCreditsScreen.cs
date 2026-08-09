namespace OpenHOI3.FrontEnd;

/// <summary>
/// The credits roll. Unlike the other panels this one is not declared inside the
/// front end file, so it is the case that exercises loading a view's window from a
/// separate interface file.
/// </summary>
public sealed class CCreditsScreen : CFrontEndView
{
	public override string WindowName => "CreditsWindow";

	public override string GuiFile => "interface/credits.gui";

	protected override void OnAttached()
	{
		BindBack("back_button");
	}
}
