using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Screens;

/// <summary>
/// The small pictures of theories and practicals a technology carries, in the tree and
/// in the panel for one technology alike: the one it adds to when it is finished, and
/// the row of those that make it cheaper. Each is a sprite named after the category.
/// See <c>docs/mechanics/technologyscreen.md</c>, section 4.
/// </summary>
public static class CTechCategoryIcons
{
	private const string Prefix = "icon_";
	private const string Small = "_small";

	/// <summary>
	/// Sets <c>icon_finish_type</c> to the category the technology adds to: the small
	/// picture in the tree, the full one in the panel for one technology. An icon keeps
	/// the sprite its file gave it when there is none of the name wanted.
	/// </summary>
	public static void ShowFinishType(CGuiWindow window, CTechnology technology, bool small)
	{
		if (technology.OnCompletion.Length > 0)
		{
			window.FindIcon("icon_finish_type")?.SetSprite(Prefix + technology.OnCompletion + (small ? Small : ""));
		}
	}

	/// <summary>
	/// Fills a box with one <c>tech_category_entry</c> for each category that makes the
	/// technology cheaper, and where a tooltip is given, its icon says that, asked of the
	/// category and its weight.
	/// </summary>
	public static void Fill(CGuiContext context, CGuiOverlappingBox? box, CTechnology technology, bool small,
		Func<string, CFixedPoint, CTooltip>? tooltip = null)
	{
		if (box is null)
		{
			return;
		}
		box.Clear();
		foreach ((string category, CFixedPoint weight) in technology.ResearchBonusFrom)
		{
			if (CGuiWindow.CreateEntry(context, "tech_category_entry") is not { } entry)
			{
				return;
			}
			if (entry.FindIcon("tech_icon") is { } icon)
			{
				icon.SetSprite(Prefix + category + (small ? Small : ""));
				if (tooltip is not null)
				{
					icon.Tooltip = () => tooltip(category, weight);
				}
			}
			box.Add(entry);
		}
	}
}
