using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

/// <summary>
/// A run of the original's played back draw for draw (<c>docs/replay.md</c>, section 9): the folder the fact base's
/// <c>probes/hoidraws.lua</c> recorded - <c>draws.txt</c>, and each hour's <c>gen_&lt;tick&gt;.txt</c> and
/// <c>ai_&lt;tick&gt;.txt</c>. As each hour begins the game's generator is put where the original's stood, and every draw a
/// country's AI makes - on the AI's generator and in the Lua's <c>math.random</c> - is answered with what the original drew
/// for that country that hour, in order. Where ours draws more than the original did, the draw is its own and counted;
/// where less, what is left is counted. A measuring instrument: nothing of it is in the game.
/// </summary>
internal sealed class CDrawPlayback
{
	/// <summary>How far below a job's entry a thread's stack may reach: a draw within it is that job's.</summary>
	private const long StackReach = 0x100000;

	private const int GeneratorSize = 624;

	private readonly string _folder;
	private readonly Dictionary<long, Hour> _hours = new();
	private Hour? _current;

	/// <summary>What the recording holds before its first hour: entering's draws, where the recorder was armed before Play.</summary>
	private Hour _entering = new();

	/// <summary>
	/// Each unit's AI model caches as the original's load left them (the fact base's <c>probes/hoimodelcache.lua</c>, file
	/// <c>caches.txt</c>), by owner and name: its province, and for defence and attack the dirty byte and the float.
	/// </summary>
	private readonly Dictionary<(string Tag, string Name), List<(int Province, int DefenceDirty, float Defence, int AttackDirty,
		float Attack)>> _caches = new();

	private int _cacheLooks;
	private int _cacheHanded;

	private CDrawPlayback(string folder)
	{
		_folder = folder;
	}

	/// <summary>The draws of one hour of the original's, by whom they were made for.</summary>
	private sealed class Hour
	{
		public readonly Dictionary<(string Tag, string? Kind), Queue<int>> Ai = new();
		public readonly Dictionary<string, Queue<int>> Rand = new();
		public int UnfiledAi;
		public int UnfiledRand;
		public int FedAi;
		public int FedRand;
		public readonly Dictionary<string, int> FedRandBy = new();
		public readonly Dictionary<string, int> MissedAi = new();
		public readonly Dictionary<string, int> MissedRand = new();
	}

	/// <summary>Reads a recording; its hours are keyed by the original's tick.</summary>
	public static CDrawPlayback Load(string folder)
	{
		var playback = new CDrawPlayback(folder);
		long tick = -1;
		var entries = new List<(long Esp, string Tag)>();
		var aiDraws = new List<(string? Tag, string? Kind, int Index, int? Value)>();
		// Lines before the first hour are entering's.
		Hour? hour = playback._entering;
		void Close()
		{
			if (hour is not null)
			{
				playback.Fill(tick, hour, aiDraws);
			}
			aiDraws.Clear();
			entries.Clear();
		}
		// Shared, so a recording still being written can be read.
		using var reader = new StreamReader(new FileStream(Path.Combine(folder, "draws.txt"), FileMode.Open, FileAccess.Read,
			FileShare.ReadWrite));
		while (reader.ReadLine() is { } line)
		{
			string[] p = line.Split(' ');
			switch (p[0])
			{
				case "H":
					Close();
					tick = long.Parse(p[1], CultureInfo.InvariantCulture);
					hour = new Hour();
					playback._hours[tick] = hour;
					break;
				case "E" when hour is not null:
				{
					long esp = long.Parse(p[1], NumberStyles.HexNumber);
					// A thread runs one job at a time: an entry on the same stack ends the one before.
					entries.RemoveAll(e => Math.Abs(e.Esp - esp) < StackReach);
					entries.Add((esp, p[2]));
					break;
				}
				case "A" when hour is not null:
				{
					// `A <esp> <index> <caller>`, `... trade <tag>` for a draw of the hourly trading, which the probe marks, and
					// last `=<value>`, what the draw returned, where the probe logged it: the AI's generator is drawn on unlocked
					// from several threads, and two draws at one index need not give the same.
					bool trade = p.Length > 5 && p[4] == CRandom.TradeKind;
					string? tag = trade ? (p[5] == "?" ? null : p[5]) : Owner(entries, long.Parse(p[1], NumberStyles.HexNumber));
					int? value = p[^1].StartsWith('=') ? int.Parse(p[^1][1..], CultureInfo.InvariantCulture) : null;
					aiDraws.Add((tag, trade ? CRandom.TradeKind : null, int.Parse(p[2], CultureInfo.InvariantCulture), value));
					break;
				}
				case "R" when hour is not null:
				{
					string? tag = Owner(entries, long.Parse(p[1], NumberStyles.HexNumber));
					if (tag is null)
					{
						hour.UnfiledRand++;
					}
					else
					{
						Queue(hour.Rand, tag).Enqueue(int.Parse(p[2], CultureInfo.InvariantCulture));
					}
					break;
				}
			}
		}
		Close();
		playback.LoadCaches();
		return playback;
	}

	private void LoadCaches()
	{
		string path = Path.Combine(_folder, "caches.txt");
		if (!File.Exists(path))
		{
			return;
		}
		foreach (string line in File.ReadLines(path, System.Text.Encoding.Latin1))
		{
			// <tag> <id> <province> <defence dirty> <defence> <attack dirty> <attack> <name...>
			string[] p = line.Split(' ', 8);
			if (p.Length < 8)
			{
				continue;
			}
			static float Read(string text) =>
				float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : float.NaN;
			var key = (p[0], p[7]);
			if (!_caches.TryGetValue(key, out var list))
			{
				list = new();
				_caches[key] = list;
			}
			list.Add((int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture), Read(p[4]),
				int.Parse(p[5], CultureInfo.InvariantCulture), Read(p[6])));
		}
	}

	/// <summary>
	/// A unit's half for the AI's first looks as the original's memory held it: null where the dirty byte was set, so the
	/// original worked it out; else the float, in thousandths - a defender's not a number as such, which the odds take as
	/// the original's floats do, and an attacker's as nought, none having been seen.
	/// </summary>
	private long? FirstLook(CGameState state, OpenHOI3.Sim.Units.CUnit unit, bool attacking)
	{
		_cacheLooks++;
		if (!_caches.TryGetValue((unit.Owner.Value, unit.Name), out var list))
		{
			return null;
		}
		var entry = list.Count == 1 ? list[0] : list.FirstOrDefault(e => e.Province == unit.Location, list[0]);
		(int dirty, float value) = attacking ? (entry.AttackDirty, entry.Attack) : (entry.DefenceDirty, entry.Defence);
		if (dirty != 0)
		{
			return null;
		}
		_cacheHanded++;
		if (float.IsNaN(value))
		{
			return attacking ? 0 : OpenHOI3.Sim.Agents.CAiCombatModel.NotANumber;
		}
		return float.IsFinite(value) ? (long)Math.Round(value * 1000.0, MidpointRounding.AwayFromZero) : 0;
	}

	/// <summary>The country of the latest job whose thread's stack holds that point, or null.</summary>
	private static string? Owner(List<(long Esp, string Tag)> entries, long esp)
	{
		for (int i = entries.Count - 1; i >= 0; i--)
		{
			if (entries[i].Esp - esp is >= 0 and < StackReach)
			{
				return entries[i].Tag;
			}
		}
		return null;
	}

	private static Queue<int> Queue<TKey>(Dictionary<TKey, Queue<int>> queues, TKey key) where TKey : notnull
	{
		if (!queues.TryGetValue(key, out Queue<int>? queue))
		{
			queue = new Queue<int>();
			queues[key] = queue;
		}
		return queue;
	}

	/// <summary>
	/// The AI generator's draws of an hour: the value the probe logged, or in a recording that logged only the index, worked
	/// out from the hour's dump at that index - so a draw the probe did not see moves no other.
	/// </summary>
	private void Fill(long tick, Hour hour, List<(string? Tag, string? Kind, int Index, int? Value)> draws)
	{
		if (draws.All(d => d.Value is not null))
		{
			foreach ((string? tag, string? kind, _, int? logged) in draws)
			{
				if (tag is null)
				{
					hour.UnfiledAi++;
				}
				else
				{
					Queue(hour.Ai, (tag, kind)).Enqueue(logged!.Value);
				}
			}
			return;
		}
		if (ReadGenerator(Path.Combine(_folder, $"ai_{tick}.txt"), indexFirst: true) is not { } dump)
		{
			return;
		}
		int most = draws.Count == 0 ? 0 : draws.Max(d => (d.Index - dump.Index + GeneratorSize) % GeneratorSize);
		var generator = new CRandom();
		generator.Restore((dump.Words, dump.Index, 0));
		var values = new int[most + 1];
		for (int i = 0; i <= most && draws.Count > 0; i++)
		{
			values[i] = generator.Next();
		}
		foreach ((string? tag, string? kind, int index, _) in draws)
		{
			if (tag is null)
			{
				hour.UnfiledAi++;
				continue;
			}
			Queue(hour.Ai, (tag, kind)).Enqueue(values[(index - dump.Index + GeneratorSize) % GeneratorSize]);
		}
	}

	/// <summary>A dump of an MT19937: hoidraws' <c>ai_</c> (index, then the words) or hoiDumpGenerator's (draws and index).</summary>
	private static (uint[] Words, int Index, long Draws)? ReadGenerator(string path, bool indexFirst)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		string[] lines = File.ReadAllLines(path);
		string[] head = lines[0].Split(' ');
		var words = new uint[GeneratorSize];
		for (int i = 0; i < GeneratorSize; i++)
		{
			words[i] = uint.Parse(lines[i + 1], NumberStyles.HexNumber);
		}
		return indexFirst
			? (words, int.Parse(head[0], CultureInfo.InvariantCulture), 0)
			: (words, int.Parse(head[1], CultureInfo.InvariantCulture), long.Parse(head[0], CultureInfo.InvariantCulture));
	}

	/// <summary>Hands the game's AI generator and the scripts' <c>math.random</c> to this recording.</summary>
	public void Attach(CGameState state, OpenHOI3.Lua.CAiScripts scripts)
	{
		state.AiRandom.Feed = (who, kind) => Take(_current?.Ai, (who.Value, kind), _current?.MissedAi,
			kind is null ? who.Value : $"{who.Value}:{kind}", fed: true);
		scripts.RandFeed = who => Take(_current?.Rand, who.Value, _current?.MissedRand, who.Value, fed: false);
		PutMinisterCountdowns(state);
		// Entering draws what the recording holds before its first hour, and its units look with the halves the original's
		// memory left them (moving.md, section 11.4), until the first hour, when the original's units have worked theirs out.
		_current = _entering;
		if (_caches.Count > 0)
		{
			state.FirstLookModel = (unit, attacking) => FirstLook(state, unit, attacking);
		}
	}

	/// <summary>
	/// The ministers' first hours as the original drew them on loading the save, before the recording began: its AI
	/// generator is seeded at start-up with the seconds the process has run, rounded, and drawn on first by the load, which
	/// makes every country's ministers in the list's order - the null country first - each country's foreign, production,
	/// intelligence, technology and politics minister in turn, one draw each, a third of them % 3 + 1 and the intelligence
	/// minister's % 10 + 1, a played country's only for what it hands over (FINDINGS-liveread.md, section 106). The seed is
	/// found as the one whose twisted words are the first hour's dump's.
	/// </summary>
	private void PutMinisterCountdowns(CGameState state)
	{
		long first = _hours.Keys.DefaultIfEmpty(-1).Min();
		if (ReadGenerator(Path.Combine(_folder, $"ai_{first}.txt"), indexFirst: true) is not { } dump)
		{
			return;
		}
		uint? found = null;
		for (uint seed = 0; seed < 3600 && found is null; seed++)
		{
			var probe = new CRandom(seed);
			probe.Next();
			if (probe.Snapshot().Words[0] == dump.Words[0] && probe.Snapshot().Words[623] == dump.Words[623])
			{
				found = seed;
			}
		}
		if (found is not { } start)
		{
			Console.WriteLine("draws: the load's seed not found; the ministers' first hours are ours");
			return;
		}
		// The load made again on the original's generator: entering then draws on from where the original's did.
		state.AiRandom.Seed(start);
		OpenHOI3.Sim.Settings.CAutomation.MakeMinistersOfLoad(state);
		Console.WriteLine($"draws: the ministers' first hours put as the original's load drew them, its seed {start}, " +
			$"{state.AiRandom.Draws} draws");
	}

	private int? Take<TKey>(Dictionary<TKey, Queue<int>>? queues, TKey key, Dictionary<string, int>? missed, string tag, bool fed)
		where TKey : notnull
	{
		if (_current is null || queues is null)
		{
			return null;
		}
		if (queues.TryGetValue(key, out Queue<int>? queue) && queue.Count > 0)
		{
			if (fed)
			{
				_current.FedAi++;
			}
			else
			{
				_current.FedRand++;
				_current.FedRandBy[tag] = _current.FedRandBy.GetValueOrDefault(tag) + 1;
			}
			return queue.Dequeue();
		}
		string who = $"{tag}@{Site()}";
		missed![who] = missed.GetValueOrDefault(who) + 1;
		return null;
	}

	/// <summary>
	/// The method that made a draw the original did not: the first frame of the stack outside the generator, the Lua's
	/// random, the scripts' and this class - a miss being rare, the stack is walked only then.
	/// </summary>
	private static string Site()
	{
		foreach (System.Diagnostics.StackFrame frame in new System.Diagnostics.StackTrace(2).GetFrames())
		{
			if (frame.GetMethod() is not { DeclaringType: { } type } method)
			{
				continue;
			}
			string name = type.Name;
			if (name.Contains('<'))
			{
				name = type.DeclaringType?.Name ?? name;
			}
			if (name is nameof(CDrawPlayback) or nameof(CRandom) or "CLuaRandom" or "CGameApi" or "CAiIntelClass"
				or "CGameStateClass" or "CLuaState" or "CLuaCall" or "CLuaNative")
			{
				continue;
			}
			return name.StartsWith("CAiScripts", StringComparison.Ordinal) ? "Lua" : $"{name}.{method.Name}";
		}
		return "?";
	}

	/// <summary>
	/// As the hour of that tick begins, as the original's dumps were taken: the game's generator put where the original's
	/// stood, and that hour's draws made the ones handed out. False where the recording has no such hour.
	/// </summary>
	public bool BeginHour(CGameState state, long tick)
	{
		if (ReferenceEquals(_current, _entering))
		{
			Console.WriteLine("entering " + Summary());
			if (state.FirstLookModel is not null)
			{
				Console.WriteLine($"entering: the original's cached halves handed over {_cacheHanded} of {_cacheLooks} looks, " +
					$"{_caches.Count} units recorded");
				state.FirstLookModel = null;
			}
		}
		_current = _hours.GetValueOrDefault(tick);
		if (ReadGenerator(Path.Combine(_folder, $"gen_{tick}.txt"), indexFirst: false) is { } game)
		{
			state.Random.Restore(game);
		}
		return _current is not null;
	}

	/// <summary>One line on the hour: what was handed out, what ours asked for beyond it, and what it left.</summary>
	public string Summary()
	{
		if (_current is not { } hour)
		{
			return "draws: no such hour in the recording";
		}
		int leftAi = hour.Ai.Values.Sum(q => q.Count);
		int leftRand = hour.Rand.Values.Sum(q => q.Count);
		static string Who(Dictionary<string, int> counts) => counts.Count == 0 ? "" :
			" (" + string.Join(" ", counts.OrderByDescending(c => c.Value).Take(8).Select(c => $"{c.Key} {c.Value}")) + ")";
		static string Left<TKey>(Dictionary<TKey, Queue<int>> queues, Func<TKey, string> name) where TKey : notnull =>
			string.Join(" ", queues.Where(q => q.Value.Count > 0).OrderByDescending(q => q.Value.Count).Take(8)
				.Select(q => $"{name(q.Key)} {q.Value.Count}"));
		string leftAiWho = Left(hour.Ai, k => k.Kind is null ? k.Tag : $"{k.Tag}:{k.Kind}");
		string leftRandWho = Left(hour.Rand, k => k);
		return $"draws: AI generator fed {hour.FedAi}, ours beyond {hour.MissedAi.Values.Sum()}{Who(hour.MissedAi)}, " +
			$"left {leftAi}{(leftAiWho.Length > 0 ? $" ({leftAiWho})" : "")}, nobody's {hour.UnfiledAi}; " +
			$"math.random fed {hour.FedRand}{Who(hour.FedRandBy)}, ours beyond {hour.MissedRand.Values.Sum()}{Who(hour.MissedRand)}, " +
			$"left {leftRand}{(leftRandWho.Length > 0 ? $" ({leftRandWho})" : "")}, nobody's {hour.UnfiledRand}";
	}
}
