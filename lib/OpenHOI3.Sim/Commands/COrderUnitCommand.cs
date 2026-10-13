using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country ordering a unit: what it is made of, how many of it one after another, and
/// whether they are reserves. The men are taken when it is ordered, for the whole
/// series, and the line goes to the back of the queue. See
/// <c>docs/mechanics/buildqueue.md</c>, section 2, and <c>builders.md</c>, section 7.
///
/// It is refused if a type does not exist, may not be built by that country - not its
/// own to use, not unlocked, or past its ration - or is not of the same branch as the
/// others; if there are more land brigades than the country may put in a division; if
/// the equipment asked for is better than the country has; and if the country has not
/// the men.
/// </summary>
public sealed class COrderUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly (string Type, string Name)[] _brigades;
	private readonly IReadOnlyList<int>?[] _models;
	private readonly string _name;
	private readonly int _size;
	private readonly bool _isReserve;
	private readonly CCountryTag? _builder;

	/// <param name="brigades">
	/// Each brigade, ship or wing: its type's key and the name it is to have. One with
	/// no name is given one as the order is carried out.
	/// </param>
	/// <param name="name">
	/// What the unit is to be called. A unit of several with no name is given one; a
	/// unit of one takes its brigade's, ship's or wing's.
	/// </param>
	/// <param name="size">How many of the unit, one after another.</param>
	/// <param name="models">
	/// For each brigade, the level it is to be built with of each technology of its
	/// type, in the type's order - or nothing, for the country's own levels.
	/// </param>
	/// <param name="builder">
	/// The country whose levels the unit is built at, a licence's seller (<c>aid.md</c>, section 4) - or
	/// nobody, for the ordering country's.
	/// </param>
	public COrderUnitCommand(CCountryTag country, IEnumerable<(string Type, string Name)> brigades, string name = "",
		int size = 1, bool isReserve = false, IReadOnlyList<IReadOnlyList<int>?>? models = null, CCountryTag? builder = null)
	{
		_builder = builder;
		_country = country;
		_brigades = System.Linq.Enumerable.ToArray(brigades);
		_models = new IReadOnlyList<int>?[_brigades.Length];
		for (int index = 0; index < _models.Length && models is not null && index < models.Count; index++)
		{
			_models[index] = models[index];
		}
		_name = name;
		_size = size;
		_isReserve = isReserve;
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || _size < 1 || _brigades.Length == 0)
		{
			return false;
		}
		CCountry levels = Builder(state, country);
		var types = new List<CSubUnitType>();
		for (int index = 0; index < _brigades.Length; index++)
		{
			if (!state.SubUnitTypes.TryGetValue(_brigades[index].Type, out CSubUnitType? type)
				|| !MayBuild(country, type) || !ModelIsWithinReach(levels, type, _models[index]))
			{
				return false;
			}
			if (types.Count > 0 && types[0].Branch != type.Branch)
			{
				return false;
			}
			types.Add(type);
		}
		if (types[0].Branch == EUnitBranch.Land && types.Count > 1
			&& types.Count > CUnitAvailability.MostBrigades(state, country))
		{
			return false;
		}
		if (!CUnitAvailability.MayBuildEach(state, country, types, _size))
		{
			return false;
		}
		CConstruction construction = Make(state, country);
		return country.Manpower.Raw >= CBuildQueue.ManpowerFor(country, construction).Raw * _size;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CConstruction construction = Make(state, country);

		construction.Id = ++state.LastUnitId;
		construction.UnitId = ++state.LastUnitId;
		foreach (CBrigadeOrder order in construction.Brigades)
		{
			order.Id = ++state.LastUnitId;
		}

		// A unit of several is named, and then each of what it is made of.
		if (construction.Brigades.Count > 1)
		{
			if (construction.Name.Length == 0)
			{
				construction.Name = CUnitNames.ForDivision(state, country, construction.Brigades[0].Type);
			}
			CUnitNames.Register(state, construction.Name);
		}
		foreach (CBrigadeOrder order in construction.Brigades)
		{
			if (order.Name.Length == 0)
			{
				order.Name = CUnitNames.ForSubUnit(state, country, order.Type);
			}
			CUnitNames.Register(state, order.Name);
		}

		construction.Manpower = CBuildQueue.ManpowerFor(country, construction);
		long men = construction.Manpower.Raw * _size;
		country.Manpower = new CFixedPoint(Math.Max(0, country.Manpower.Raw - men));

		CBuildQueue.Recalculate(country, construction);
		country.Constructions.Add(construction);
		CBuildQueue.UpdateCost(country);
	}

	/// <summary>
	/// Whether a country may build a type at all: one its file does not rule out, and
	/// that is its own to use where the file names who may.
	/// </summary>
	public static bool MayBuild(CCountry country, CSubUnitType type)
	{
		return CUnitAvailability.IsBuildableType(type)
			&& (country.EveryUnitUnlocked || CUnitAvailability.IsUsableBy(country, type));
	}

	/// <summary>
	/// Equipment may be asked for at any level from nothing to what the country has
	/// reached, technology by technology, and for no more technologies than the type has.
	/// </summary>
	private static bool ModelIsWithinReach(CCountry country, CSubUnitType type, IReadOnlyList<int>? model)
	{
		if (model is null)
		{
			return true;
		}
		if (model.Count > type.Technologies.Count)
		{
			return false;
		}
		for (int index = 0; index < model.Count; index++)
		{
			if (model[index] < 0 || model[index] > country.TechnologyLevels.GetValueOrDefault(type.Technologies[index].Name))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>The country whose levels the unit is built at: the builder where one is named, else the country.</summary>
	private CCountry Builder(CGameState state, CCountry country) =>
		_builder is { } builder && state.Country(builder) is { } seller ? seller : country;

	/// <summary>The line as it would be queued, with no numbers given and nothing taken.</summary>
	private CConstruction Make(CGameState state, CCountry country)
	{
		CCountry levels = Builder(state, country);
		var construction = new CConstruction(country.Tag) { Name = _name, Size = _size, IsReserve = _isReserve };
		if (_isReserve)
		{
			construction.ReservesFactor = CBuildQueue.ReservesFactor(country);
		}
		for (int index = 0; index < _brigades.Length; index++)
		{
			var order = new CBrigadeOrder(state.SubUnitTypes[_brigades[index].Type]) { Name = _brigades[index].Name };
			if (_models[index] is { } model)
			{
				order.Model.AddRange(model);
			}
			else
			{
				CBuildQueue.SetModelToCountrys(levels, order);
			}
			construction.Brigades.Add(order);
		}
		return construction;
	}
}
