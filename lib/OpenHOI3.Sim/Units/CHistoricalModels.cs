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
