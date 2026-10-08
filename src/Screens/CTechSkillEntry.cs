
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Screens;

/// <summary>
/// A theory or practical along the foot of the technology screen: a
/// <c>tech_category_skill_entry</c> window with the category's picture, how much of it
/// the country has as a number, and the same as a bar of twenty steps. See
/// <c>docs/mechanics/technologyscreen.md</c>, section 5.
/// </summary>
public sealed class CTechSkillEntry
{
	private readonly CGuiTextBox? _skill;
	private readonly CGuiIcon? _indicator;

	private CTechSkillEntry(CGuiWindow window, CTechnologyCategory category)
	{
		Window = window;
		Category = category;
		_skill = window.FindTextBox("skill");
		_indicator = window.FindIcon("tech_skill_indicator");
		window.FindIcon("tech_cat_icon")?.SetSprite("icon_" + category.Name);
	}

	public CGuiWindow Window { get; }

	public CTechnologyCategory Category { get; }

	public static CTechSkillEntry? Create(CGuiContext context, CTechnologyCategory category)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "tech_category_skill_entry");
		return window is null ? null : new CTechSkillEntry(window, category);
	}

	public void Update(CCountry country)
	{
		// Nothing shares a category between countries yet, so the figure is always the country's own.
		CFixedPoint level = country.Ability(Category.Name);
		if (_skill is not null)
		{
			_skill.Text = CTechnologyScreenText.Skill(level, shared: false);
		}

		// The bar's first frame is the empty one, so a level of n is frame n + 1 of the sheet.
		_indicator?.SetFrame(CTechnologyScreenText.SkillFrame(level) + 1);
	}
}
