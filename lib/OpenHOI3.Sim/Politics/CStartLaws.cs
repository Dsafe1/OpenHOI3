using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A new game's laws brought forward: every country, the player's too, has each law moved to the latest law of its group,
/// in the file's order, whose <c>allow</c> holds, where that is later than its own - put in place with no cost and no
/// message. A group with a law that gives units starting experience later than the country's is left alone. The original's
/// <c>SetUpCountryLawsAndTechnologyForNewGame</c>, its first half. See <c>docs/mechanics/politics.md</c>, section 13.
/// </summary>
public static class CStartLaws
{
	/// <summary>A law with any of this stops its group's walk, the group keeping its law.</summary>
	public const string StartExperience = "unit_start_experience";

	/// <returns>How many of the country's laws were moved.</returns>
	public static int Move(CGameState state, CCountry country)
	{
		int moved = 0;
		foreach (CLawGroup group in state.LawGroups)
		{
			country.Laws.TryGetValue(group.Name, out CLaw? current);
			for (int index = group.Laws.Count - 1; index >= 0; index--)
			{
				CLaw law = group.Laws[index];
				if (law.Modifier[StartExperience].Raw != 0 || ReferenceEquals(law, current))
				{
					break;
				}
				if (CLaws.IsAllowed(state, country, law))
				{
					country.Laws[group.Name] = law;
					moved++;
					break;
				}
			}
		}
		if (moved > 0)
		{
			Modifiers.CCountryModifiers.RebuildOne(state, country);
		}
		return moved;
	}
}
