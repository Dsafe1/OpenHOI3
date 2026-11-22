using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Windows;

/// <summary>
/// The windows a mod defines, as the screen asks for them: which there are, a page gathering what it shows, and a page
/// drawing itself. The mod's Lua answers (<c>OpenHOI3.Lua</c>'s <c>CLuaWindows</c>); the screen knows nothing of Lua.
/// <c>docs/mechanics/modwindows.md</c>.
/// </summary>
public interface IModPages
{
	/// <summary>The pages, in the menu's order.</summary>
	IReadOnlyList<CModPage> Pages { get; }

	/// <summary>
	/// A page reads the game and keeps what it will show: asked now and then while the page is open, and never while
	/// the game's hours run beside the frame.
	/// </summary>
	void Collect(CGameState state, CModPage page, CCountryTag player);

	/// <summary>
	/// A page draws itself from what it last gathered, every frame it is open - while hours run too, so it is given no
	/// game to ask. A page whose drawing failed says why in its window until it is gathered again.
	/// </summary>
	void Draw(CModPage page, CCountryTag player, IModWindowCanvas canvas);
}
