using OpenHOI3.Gui;

namespace OpenHOI3.Screens;

/// <summary>
/// One half of the battle window, whatever the battle's kind: a window of its own that
/// the battle's window hangs on its left or its right, written again every frame.
/// </summary>
public interface ICombatSideView
{
	CGuiWindow Window { get; }

	/// <summary>Whether the half has anybody in its lists. A battle window with an empty half goes away.</summary>
	bool HasUnits { get; }

	void Update();
}
