using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's <c>active_leaders</c>: the leaders in service, in their order, and only their state - rank, skill where
/// it is not the file's, experience, the progress towards a trait and the traits gained. Everything else of a leader is
/// his file's, and a save keeps no history of him. See <c>docs/mechanics/savegame.md</c>, section 5.
/// </summary>
public static class CSaveLeaders
{
	public static void Read(CGameState state, CCountry country, ClausewitzNode block, CSaveReport report)
	{
		country.ActiveLeaders.Clear();
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || !entry.IsBlock || !int.TryParse(entry.Key, out int id))
			{
				continue;
			}
			// He must be one of the files' leaders; a number that is none is passed over, as the original passes it.
			if (!state.Leaders.TryGetValue(id, out CLeader? leader))
			{
				report.Unread("active_leaders", "a leader of no file's, passed over");
				continue;
			}
			foreach (ClausewitzNode key in entry.Children)
			{
				switch (key.Key)
				{
					case null:
						break;
					case "rank":
						leader.Rank = (int)CSaveValues.Whole(key.Value);
						break;
					case "skill":
						leader.Skill = (int)CSaveValues.Whole(key.Value);
						break;
					case "current_experience":
						leader.Experience = CSaveValues.Fixed15(key.Value);
						break;
					case "gainable_trait" when key.IsBlock:
						ReadProgress(state, leader.TraitProgress, key, report);
						break;
					case "trait" when key.IsBlock:
						foreach (ClausewitzNode name in key.Children)
						{
							// One already held is not added twice.
							if (state.Traits.FirstOrDefault(trait => trait.Key == name.Value) is { } trait
								&& !leader.Traits.Contains(trait))
							{
								leader.Traits.Add(trait);
							}
						}
						break;
					default:
						report.Unread("leader", key.Key);
						break;
				}
			}
			// Then his skill and experience are raised to their floors, as after any reading of him.
			CLeaders.SettleSkill(leader);
			country.ActiveLeaders.Add(leader);
		}
	}

	private static void ReadProgress(CGameState state, CTraitProgress progress, ClausewitzNode block, CSaveReport report)
	{
		progress.Progress.Clear();
		progress.Duration = 0;
		progress.CurrentHour = -1;
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
					break;
				case "duration":
					progress.Duration = (int)CSaveValues.Whole(entry.Value);
					break;
				case "current":
					progress.CurrentHour = (int)CSaveValues.Whole(entry.Value);
					break;
				default:
					if (state.Traits.FirstOrDefault(trait => trait.Key == entry.Key) is { } trait)
					{
						progress.Progress[trait] = (int)CSaveValues.Fixed(entry.Value).Raw;
					}
					else
					{
						report.Unread("gainable_trait", entry.Key);
					}
					break;
			}
		}
	}
}
