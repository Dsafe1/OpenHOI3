using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Screens;

/// <summary>
/// A leader's trait in a row of overlapping icons: the original's <c>trait_entry</c>,
/// its <c>trait_icon</c> the sprite <c>icon_</c> and the trait's key where one is
/// declared, and the file's picture where not. See <c>docs/mechanics/leaders.md</c>,
/// section 12.
/// </summary>
public static class CTraitIconEntry
{
	public static CGuiWindow? Create(CGuiContext context, CTrait trait)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CLeaderWindowText.TraitEntryName);
		if (window is null)
		{
			return null;
		}
		window.FindIcon("trait_icon")?.SetSprite(CLeaderWindowText.TraitSprite(trait));
		window.SetTooltips(name => name == "trait_icon"
			? CLeaderWindowTooltips.TraitIcon(context.Localisation.Localise, trait)
			: CTooltip.Empty);
		return window;
	}

	/// <summary>Fills a box with an icon for each of a leader's traits, in his order.</summary>
	public static void Fill(CGuiContext context, CGuiOverlappingBox? box, CLeader? leader)
	{
		if (box is null)
		{
			return;
		}
		box.Clear();
		if (leader is null)
		{
			return;
		}
		foreach (CTrait trait in leader.Traits)
		{
			if (Create(context, trait) is { } icon)
			{
				box.Add(icon);
			}
		}
	}
}
