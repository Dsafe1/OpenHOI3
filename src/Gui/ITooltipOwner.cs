using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Gui;

/// <summary>
/// A node that answers for the tooltips of the elements inside it before they do - as the original's production screen
/// hands every tooltip to its confirm-build dialog while one is open (<c>docs/mechanics/convoylines.md</c>, section 4).
/// </summary>
public interface ITooltipOwner
{
	/// <summary>What the element under the mouse is to say instead of its own tooltip, or null to let it say its own.</summary>
	CTooltip? Answer(CGuiObject element);
}
