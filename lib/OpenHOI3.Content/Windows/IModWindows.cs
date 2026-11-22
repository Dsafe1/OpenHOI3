using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// The windows a mod defines, as the screen asks for them: which there are, what one shows now, and a press of one of
/// its buttons. The mod's Lua answers (<c>OpenHOI3.Lua</c>'s <c>CLuaWindows</c>); the screen knows nothing of Lua.
/// <c>docs/mechanics/modwindows.md</c>.
/// </summary>
public interface IModWindows
{
	/// <summary>The pages, in the menu's order.</summary>
	IReadOnlyList<CModWindowPage> Pages { get; }

	/// <summary>What a page shows now for the country played; null where its Lua failed, which is reported.</summary>
	CModWindowData? Collect(CGameState state, CModWindowPage page, CCountryTag player);

	/// <summary>
	/// A button of a page pressed: its name, and where it stands in a list's row that row's place from nought, else -1.
	/// The page's Lua may post commands and changes nothing itself.
	/// </summary>
	void Click(CGameState state, CModWindowPage page, CCountryTag player, string element, int row);
}
