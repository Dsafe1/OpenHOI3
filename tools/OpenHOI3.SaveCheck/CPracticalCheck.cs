using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

/// <summary>
/// The practicals a battle teaches (<c>docs/mechanics/research.md</c>, section 10), set beside a later save of the same
/// game: for each the defines' top level names, every country's figure in the earlier save, the later save and ours.
/// Over a few hours nothing but battles moves them - a finished level would add a whole point - so where ours and the
/// later save part, the battles fought did.
/// </summary>
public static class CPracticalCheck
{
	public static void Report(CGameState state, ClausewitzNode earlier, ClausewitzNode later, string indent, int examples = 8)
	{
		var names = new[] { CCombatPractical.LandCategory, CCombatPractical.NavalCategory, CCombatPractical.AirCategory,
			CCombatPractical.BombingCategory }.Select(key => state.Defines.Text("", key)).Where(n => n.Length > 0).Distinct();
		foreach (string name in names)
		{
			int countries = 0, moved = 0, oursMoved = 0, same = 0;
			var misses = new List<string>();
			foreach (CCountry country in state.Countries)
			{
				if (later.Get(country.Tag.ToString()) is not { IsBlock: true } after
					|| earlier.Get(country.Tag.ToString()) is not { IsBlock: true } before)
				{
					continue;
				}
				CFixedPoint was = CSaveValues.Fixed(before, name);
				CFixedPoint saved = CSaveValues.Fixed(after, name);
				CFixedPoint ours = country.Ability(name);
				countries++;
				moved += saved != was ? 1 : 0;
				oursMoved += ours != was ? 1 : 0;
				if (ours == saved)
				{
					same++;
				}
				else if (misses.Count < examples)
				{
					misses.Add($"{country.Tag} {was} -> saved {saved}, ours {ours}");
				}
			}
			Console.WriteLine($"{indent}{name}: {same} of {countries} the later save's; moved in the save {moved}, in ours {oursMoved}");
			foreach (string miss in misses)
			{
				Console.WriteLine($"{indent}  {miss}");
			}
		}
	}
}
