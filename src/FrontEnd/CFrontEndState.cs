namespace OpenHOI3.FrontEnd;

/// <summary>
/// The main menu panel. Named after the original's <c>CFrontEndState</c>, which is
/// likewise a <c>CFrontEndView</c> rather than anything special - the main menu is
/// just the view shown first.
/// </summary>
public sealed class CFrontEndState : CFrontEndView
{
	// Hardcoded in the original executable rather than in any data file; recovered
	// from it. The social links really are the bare sites, with no account path.
	private const string HomepageUrl = "http://www.heartsofirongame.com";
	private const string ForumUrl =
		"http://forum.paradoxplaza.com/forum/forumdisplay.php?415-Hearts-of-Iron-3";
	private const string FacebookUrl = "http://www.facebook.com/?ref=logo";
	private const string TwitterUrl = "http://twitter.com/";

	public override string WindowName => "mainmenu_panel";

	protected override void OnAttached()
	{
		Bind("multi_player_button", Host.Show<CMultiplayerMenuView>);
		Bind("tutorial_button", Host.Show<CTutorialScreen>);
		Bind("settings_button", Host.Show<CFrontendSettingsScreen>);
		Bind("credits_button", Host.Show<CCreditsScreen>);
		Bind("toggle_quick_normal", Host.Show<CEasymodeView>);
		Bind("exit_button", Host.Quit);

		BindLink("homepage_button", HomepageUrl);
		BindLink("forum_button", ForumUrl);
		BindLink("facebook_button", FacebookUrl);
		BindLink("twitter_button", TwitterUrl);

		// Single player opens the country selection screen, which the original models
		// as CGameSetup: a CFrontEndView needing scenario, bookmark and country data.
		BindPlaceholder("single_player_button");
		BindPlaceholder("gg_micro_button");

		// Nudge is the map object placement editor, reached in the original by
		// switching to a separate top-level idler rather than another view. The
		// retail build never shows this button: CFrontEnd resolves it and calls
		// Hide() on it unconditionally during setup. Patching those calls out shows
		// a tool that only really works for province text placement, so it was
		// disabled because it was unfinished. Hidden here to match.
		HideElement("nudge_button");
	}
}
