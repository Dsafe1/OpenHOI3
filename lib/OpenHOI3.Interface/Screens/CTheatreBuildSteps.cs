using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What a press of the theatres screen's Build does: a run of steps, each ordering aircraft, one division, a land type or ships out of what is needed, until one
/// finds nothing to order or the men run out. Each order is aimed at the theatre's headquarters. See <c>docs/mechanics/theatresscreen.md</c>, section 5.
/// </summary>
public sealed class CTheatreBuildSteps
{
	private const long NeedWeight = 5000;
	private const long DrawWeight = 1000;
	private const int MostSteps = 4096;
	private const int FirstPriorityPass = 4;
	private const int LeastLineSize = 2;
	private const string Priority = "priority";

	private readonly CGameState _state;
	private readonly CCountry _player;
	private readonly int _target;
	private readonly int[] _needs;
	private readonly Func<uint> _draw;
	private readonly List<CCommand> _commands = new();
	private long _manpower;

	/// <param name="needs">One figure for each unit type by its number in the database, taken down as orders are made.</param>
	/// <param name="draw">The original's generator is never seeded, so that a pick is arbitrary; the caller's stands in for it.</param>
	public CTheatreBuildSteps(CGameState state, CCountry player, int target, int[] needs, Func<uint> draw)
	{
		_state = state;
		_player = player;
		_target = target;
		_needs = needs;
		_draw = draw;
		_manpower = player.Manpower.Raw;
	}

	/// <summary>Every order the press makes, in the order they are to be sent.</summary>
	public IReadOnlyList<CCommand> Run()
	{
		for (int step = 0; step < MostSteps && Step(); step++)
		{
		}
		return _commands;
	}

	private IEnumerable<CSubUnitType> Types(EUnitBranch branch) =>
		_state.SubUnitTypes.Values.Where(type => type.Branch == branch).OrderBy(type => type.DatabaseIndex);

	private long Sum(EUnitBranch branch) => Types(branch).Sum(type => (long)Math.Max(0, _needs[type.DatabaseIndex]));

	private bool Orderable(CSubUnitType type) =>
		_needs[type.DatabaseIndex] > 0 && CUnitAvailability.IsBuildableType(type) && CUnitAvailability.StillBuildable(_state, _player, type) >= 1;

	/// <summary>One pass: a step answers whether anything was ordered, and the press goes on until one says no.</summary>
	private bool Step()
	{
		long air = Sum(EUnitBranch.Air);
		long land = Sum(EUnitBranch.Land);
		if (air > land / 4)
		{
			return Best(EUnitBranch.Air) is { } plane ? Series(plane) : Ships();
		}
		if (land > 2)
		{
			if (Division())
			{
				return true;
			}
			return Best(EUnitBranch.Land) is { } type ? Series(type) : Ships();
		}
		if (land > 0)
		{
			return Best(EUnitBranch.Land) is { } type ? Series(type) : Ships();
		}
		return Ships();
	}

	private bool Ships() => Best(EUnitBranch.Naval) is { } ship && Series(ship);

	/// <summary>The type with the highest score, by how much of it is needed and a draw; strictly, so the first of equals.</summary>
	private CSubUnitType? Best(EUnitBranch branch)
	{
		CSubUnitType? best = null;
		long bestScore = long.MinValue;
		foreach (CSubUnitType type in Types(branch).Where(Orderable))
		{
			long need = _needs[type.DatabaseIndex];
			long score = need * NeedWeight + _draw() % (need * 10 + 1) * DrawWeight;
			if (score > bestScore)
			{
				bestScore = score;
				best = type;
			}
		}
		return best;
	}

	/// <summary>Lines of a square root's size, each cut to what the men left will raise, no smaller than two. Short of men, the step ends and orders no more.</summary>
	private bool Series(CSubUnitType type)
	{
		int need = _needs[type.DatabaseIndex];
		int size = Math.Max(1, SquareRoot(need));
		int lines = need / size;
		for (int line = 0; line < lines; line++)
		{
			while (size > LeastLineSize && Men(type) * size > _manpower)
			{
				size--;
			}
			_manpower -= Men(type) * size;
			if (_manpower < 0)
			{
				return false;
			}
			_commands.Add(new CConstructUnitCommand(_player.Tag, new[] { type.Key }, size, false, _target));
			_needs[type.DatabaseIndex] -= size;
		}
		return true;
	}

	private static long Men(CSubUnitType type) => type.Stats[CSubUnitStats.BuildCostManpower].Raw;

	private static int SquareRoot(int value)
	{
		if (value < 2)
		{
			return value;
		}
		int root = value / 2;
		while (true)
		{
			int next = (root + value / root) / 2;
			if (next >= root)
			{
				return root;
			}
			root = next;
		}
	}

	/// <summary>
	/// One division: its priority brigades up to one short of the most it may have, then the rest. The needs stay taken down even where the division then cannot be ordered.
	/// </summary>
	private bool Division()
	{
		int most = CUnitAvailability.MostBrigades(_state, _player);
		var brigades = new List<CSubUnitType>();
		Fill(brigades, most - 1, priority => priority > FirstPriorityPass);
		Fill(brigades, most, priority => priority <= FirstPriorityPass);
		if (brigades.Count == 0 || !CUnitAvailability.MayBuildDivision(_state, _player, brigades, 1))
		{
			return false;
		}
		_manpower -= brigades.Sum(Men);
		_commands.Add(new CConstructUnitCommand(_player.Tag, brigades.Select(type => type.Key), 1, false, _target));
		return true;
	}

	private void Fill(List<CSubUnitType> brigades, int limit, Func<long, bool> passes)
	{
		foreach (CSubUnitType type in Types(EUnitBranch.Land).Where(type => passes(type.Stats[Priority].Raw / CFixedPoint.Scale) && Orderable(type)))
		{
			while (brigades.Count < limit && _needs[type.DatabaseIndex] > 0)
			{
				brigades.Add(type);
				_needs[type.DatabaseIndex]--;
			}
		}
	}
}
