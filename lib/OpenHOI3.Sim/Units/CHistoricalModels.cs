using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A country's historical models: for each unit type, a numbered list of what
/// technology levels a unit of that type was built with. An order of battle picks one
/// with <c>historical_model</c>, and that is where a brigade's starting equipment
/// comes from. See <c>docs/mechanics/units.md</c>, section 5.
/// </summary>
public static class CHistoricalModels
{
	public const string MilitarySection = "military";
	public const string MaxDefine = "HISTORICAL_MODEL_MAX";

	/// <summary>
	/// What a technology a model asks more of than is held counts in an enhanced game, whatever the gap
	/// (<see cref="Rules.EFix.HistoricalModelChoice"/>): more than any sum of levels a thousand each.
	/// </summary>
	public const long TooHighTerm = 1_048_576;

	/// <summary>How many default models every country has of every type.</summary>
	public static int DefaultCount(CGameState state)
	{
		return (int)state.Defines[MilitarySection, MaxDefine].ToInt();
	}

	/// <summary>
	/// How many models a country has of a type: the defaults, and any its files add
	/// past them.
	/// </summary>
	public static int Count(CGameState state, CCountry country, CSubUnitType type)
	{
		int count = DefaultCount(state);
		if (country.HistoricalModels.TryGetValue(type.Key, out SortedDictionary<int, Dictionary<string, int>>? given))
		{
			foreach (int number in given.Keys)
			{
				if (number >= count)
				{
					count = number + 1;
				}
			}
		}
		return count;
	}

	/// <summary>
	/// The model of a type a country would build today: each model scored by the sum, over the technologies
	/// it lists that change a unit, of how far the country's level is from the model's, a thousand a level;
	/// the least wins, the first of equals. -1 for a type with no model. See <c>docs/mechanics/aid.md</c>,
	/// section 6.
	/// </summary>
	public static int PickCurrent(CGameState state, CCountry country, CSubUnitType type)
	{
		return Nearest(state, country, type, country.TechnologyLevels);
	}

	/// <summary>
	/// The first model that lists every technology of the type, each that changes a unit at exactly the country's
	/// level and the others at any; -1 for none. Where it is not <see cref="PickCurrent"/>'s, a model's name carries
	/// "+*".
	/// </summary>
	public static int FindExact(CGameState state, CCountry country, CSubUnitType type)
	{
		return Exact(state, country, type, country.TechnologyLevels);
	}

	/// <summary>
	/// Which of a country's models of its type a brigade is shown as - by the unit panel's numeral, its card's picture and
	/// name, its summary and a sunk ship's row: the model nearest the levels the brigade itself carries, as
	/// <see cref="Nearest"/> scores them. Its own levels and nothing else, so a brigade loaded from a save, which names no
	/// model, is shown as the one it was. -1 for a type with no model. See <c>docs/mechanics/units.md</c>, section 5.
	/// </summary>
	public static int OfBrigade(CGameState state, CCountry country, CSubUnit subUnit)
	{
		return Nearest(state, country, subUnit.Type, subUnit.TechnologyLevels);
	}

	/// <summary>
	/// Whether a brigade's model is marked <c>+*</c>: where the first model its levels match exactly is not the nearest -
	/// none matching, mostly, as when a level has been upgraded past the models a country has.
	/// </summary>
	public static bool IsInexact(CGameState state, CCountry country, CSubUnit subUnit)
	{
		return Exact(state, country, subUnit.Type, subUnit.TechnologyLevels) != OfBrigade(state, country, subUnit);
	}

	/// <summary>
	/// Whose models a brigade is named by: the country that built it where it was built for another - a licence's seller -
	/// and otherwise its owner.
	/// </summary>
	public static CCountry BuilderOf(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		return state.Country(subUnit.Builder) ?? owner;
	}

	/// <summary>
	/// The model of a type nearest a set of levels: each model scored by the sum, over the technologies it lists that
	/// change a unit, of how far the level is from the model's, a thousand a level; the least wins, the first of equals.
	/// -1 for a type with no model. The original's counts a level the model has too many as one it has too few, so a
	/// country is shown a model it has not researched; an enhanced game's counts <see cref="TooHighTerm"/> for each
	/// technology the model asks more of, so such a model loses to every one that asks no more, and of those that all
	/// do the one with the fewest such technologies wins.
	/// </summary>
	public static int Nearest(CGameState state, CCountry country, CSubUnitType type, IReadOnlyDictionary<string, int> levels)
	{
		long best = 999_999_000;
		int chosen = -1;
		bool tooHighLoses = state.Rules.Has(Rules.EFix.HistoricalModelChoice);
		for (int model = 0; model < Count(state, country, type); model++)
		{
			long score = 0;
			foreach (CTechnology technology in type.Technologies)
			{
				if (!technology.Change || Level(state, country, type, model, technology) is not { } wanted)
				{
					continue;
				}
				int held = levels.GetValueOrDefault(technology.Name);
				score += tooHighLoses && wanted > held ? TooHighTerm : Math.Abs(held - wanted) * 1000L;
			}
			if (score < best)
			{
				best = score;
				chosen = model;
			}
		}
		return chosen;
	}

	/// <summary>
	/// The first model that lists every technology of the type, each that changes a unit at exactly the level given and
	/// the others at any; -1 for none.
	/// </summary>
	public static int Exact(CGameState state, CCountry country, CSubUnitType type, IReadOnlyDictionary<string, int> levels)
	{
		for (int model = 0; model < Count(state, country, type); model++)
		{
			bool matches = true;
			foreach (CTechnology technology in type.Technologies)
			{
				if (Level(state, country, type, model, technology) is not { } wanted
					|| (technology.Change && levels.GetValueOrDefault(technology.Name) != wanted))
				{
					matches = false;
					break;
				}
			}
			if (matches)
			{
				return model;
			}
		}
		return -1;
	}

	/// <summary>
	/// The level a model gives a technology, or null where the model does not list it -
	/// which leaves the brigade's level as it was - or where there is no such model.
	///
	/// A model out of a file lists what the file lists and nothing more. A default
	/// model lists every technology of the type, at the model's own number - except a
	/// one-level technology that has a precondition, which it lists at nothing.
	/// </summary>
	public static int? Level(CGameState state, CCountry country, CSubUnitType type, int model, CTechnology technology)
	{
		if (model < 0 || model >= Count(state, country, type))
		{
			return null;
		}
		if (country.HistoricalModels.TryGetValue(type.Key, out SortedDictionary<int, Dictionary<string, int>>? given)
			&& given.TryGetValue(model, out Dictionary<string, int>? levels))
		{
			return levels.TryGetValue(technology.Name, out int level) ? level : null;
		}
		return technology.HasPrecondition && technology.IsSingleLevel ? 0 : model;
	}
}
