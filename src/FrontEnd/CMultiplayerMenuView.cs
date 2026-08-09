namespace OpenHOI3.FrontEnd;

/// <summary>
/// The multiplayer lobby browser. Everything here needs networking, so only the
/// navigation is live.
/// </summary>
public sealed class CMultiplayerMenuView : CFrontEndView
{
	public override string WindowName => "multiplayer_menu";

	protected override void OnAttached()
	{
		// This panel spells its back button "Back" where the others use "back_button".
		BindBack("Back");

		BindPlaceholder("host");
		BindPlaceholder("join");
		BindPlaceholder("join_direct");
		BindPlaceholder("scan");
		BindPlaceholder("metaserver");
	}
}
