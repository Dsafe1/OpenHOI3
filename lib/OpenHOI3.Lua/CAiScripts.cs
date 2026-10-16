using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Lua.Api;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua;

/// <summary>
/// The install's Lua, as the original runs it: one state, every standard library, the game's classes,
/// a searcher over the install's layers ahead of Lua's own, and <c>script/autoexec.lua</c> run once.
/// What the simulation asks of it, it answers here. See <c>docs/mechanics/lua.md</c>.
/// </summary>
public sealed class CAiScripts : IAiScripts, IDisposable
{
	private const string Autoexec = "script/autoexec.lua";

	/// <summary>Where a <c>require</c>d module is looked for, in order, each over every layer.</summary>
	private static readonly string[] ModuleFolders = { "script/", "script/country/", "common/" };

	/// <summary>What the relation is where the call fails: the original sets it before calling.</summary>
	private const int RelationOnFailure = 25;

	/// <summary>What an offer's score is where the call fails: the original's bridge answers 0, a refusal.</summary>
	private const int ScoreOnFailure = 0;

	/// <summary>What a failed call's report begins with.</summary>
	private const string ErrorPrefix = "LUA Error: ";

	private const string UnbuiltPrefix = CGameApi.UnbuiltPrefix;

	private readonly CDirectorySettings _directories;
	private readonly Action<string> _report;
	private readonly CLuaState _lua = new();
	private readonly CGameApi _api;
	private CGameState? _state;
	private int _failures;
	private CTradeWorkers? _workers;

	public CAiScripts(CDirectorySettings directories, Action<string>? report = null)
		: this(directories, report, worker: false)
	{
	}

	private CAiScripts(CDirectorySettings directories, Action<string>? report, bool worker)
	{
		_directories = directories;
		_report = report ?? CDiagnostics.Warn;
		_api = new CGameApi(() => _state ?? throw new CLuaError("no game is being asked"), _report) { Scripts = this };
		_lua.OpenLibraries("base", "package", "table", "io", "os", "string", "math", "debug");
		// Every state draws math.random on a generator of its own, seeded 1 as a new thread's is - the tech and
		// production scripts' picks among them (ministers.md, Ours).
		new CLuaRandom().Install(_lua);
		foreach (CLuaClass luaClass in _api.Classes)
		{
			_lua.DefineClass(luaClass);
		}
		InstallSearcher();
		RunFile(Autoexec);
	}

	/// <summary>A state of the hour's trading's own, with a <c>math.random</c> of its own (<see cref="CTradeWorkers"/>).</summary>
	internal static CAiScripts Worker(CDirectorySettings directories, Action<string> report) =>
		new(directories, report, worker: true);

	/// <summary>The state, for a check or a test that asks it something directly.</summary>
	public CLuaState Lua => _lua;

	/// <summary>How many calls failed and were reported.</summary>
	public int Failures => _failures;

	/// <summary>The classes the scripts are handed, and what of the game they reported as not built.</summary>
	public CGameApi Api => _api;

	public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other)
	{
		_state = state;
		try
		{
			object? value = _lua.Call("AI_GenerateNonHistoricalRelation", 1, _api.Of(country), _api.Tag(other.Tag))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			Fail($"AI_GenerateNonHistoricalRelation({country.Tag}, {other.Tag}) answered {value ?? "nil"}, not a number");
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		return RelationOnFailure;
	}

	public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer)
	{
		_state = state;
		try
		{
			// The AI handed over is the recipient's, which every country has.
			object? value = _lua.Call(function, 1, new CLuaObject(_api.Ai, new CLuaAi(recipient)),
				_api.Tag(actor.Tag), _api.Tag(recipient.Tag), _api.Tag(observer.Tag))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			Fail($"{function}({actor.Tag}, {recipient.Tag}, {observer.Tag}) answered {value ?? "nil"}, not a number");
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		return ScoreOnFailure;
	}

	public int OfferTradeScore(CGameState state, Sim.Agreements.COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient
			|| offer.Deal is not { } deal)
		{
			return ScoreOnFailure;
		}
		CGameState? asked = _state;
		_state = state;
		try
		{
			var action = new CLuaTradeAction(offer.Actor, offer.Recipient, offer.Date)
			{
				Value = offer.Value,
				Route = new Sim.Trade.CTradeRoute(deal.From, deal.To, deal.Terms, offer.Date),
			};
			object? value = _lua.Call("DiploScore_OfferTrade", 1, new CLuaObject(_api.Ai, new CLuaAi(recipient)),
				_api.Tag(actor.Tag), _api.Tag(recipient.Tag), _api.Tag(recipient.Tag),
				new CLuaObject(_api.TradeAction, action),
				new CLuaObject(_api.GoodsValues, new CLuaGoodsValues(deal.Terms.From)),
				new CLuaObject(_api.GoodsValues, new CLuaGoodsValues(deal.Terms.To)))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			Fail($"DiploScore_OfferTrade({actor.Tag}, {recipient.Tag}) answered {value ?? "nil"}, not a number");
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		finally
		{
			_state = asked ?? state;
		}
		return ScoreOnFailure;
	}

	/// <summary>
	/// <c>DiploScore_LicenceTechnology(ai, buyer, seller, seller, action)</c>, the seller's AI answering and
	/// the licence handed over itself; 0 on a failure. See <c>docs/mechanics/aid.md</c>, section 4.
	/// </summary>
	public int LicenceScore(CGameState state, Sim.Agreements.COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return ScoreOnFailure;
		}
		CGameState? asked = _state;
		_state = state;
		try
		{
			object? value = _lua.Call("DiploScore_LicenceTechnology", 1, new CLuaObject(_api.Ai, new CLuaAi(recipient)),
				_api.Tag(actor.Tag), _api.Tag(recipient.Tag), _api.Tag(recipient.Tag),
				new CLuaObject(_api.LicenceAction, CLuaLicenceAction.Of(offer)))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			Fail($"DiploScore_LicenceTechnology({actor.Tag}, {recipient.Tag}) answered {value ?? "nil"}, not a number");
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		finally
		{
			_state = asked ?? state;
		}
		return ScoreOnFailure;
	}

	public void RunTrade(CGameState state, CCountry country, bool propose)
	{
		_state = state;
		string function = propose ? "ProposeTrades" : "EvalutateExistingTrades";
		try
		{
			_lua.Call(function, 0, new CLuaObject(_api.Ai, new CLuaAi(country)), _api.Tag(country.Tag));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>
	/// <c>Laws(country)</c>, handed the country itself as a new game hands it - which TFH's script reads as it would a
	/// minister. A failure is reported, as the original logs it.
	/// </summary>
	public void RunLaws(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("Laws", 0, _api.Of(country));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary><c>PoliticsMinister_Tick(minister)</c>, the country's politics minister handed over. A failure is reported.</summary>
	public void RunPolitics(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("PoliticsMinister_Tick", 0, new CLuaObject(_api.PoliticsMinister, new CLuaAi(country)));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary><c>IntelligenceMinister_Tick(minister)</c>, the country's intelligence minister handed over. A failure is reported.</summary>
	public void RunIntelligence(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("IntelligenceMinister_Tick", 0, new CLuaObject(_api.IntelligenceMinister, new CLuaAi(country)));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary><c>ForeignMinister_Tick(minister)</c>, the country's foreign minister handed over. A failure is reported.</summary>
	public void RunForeign(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("ForeignMinister_Tick", 0, new CLuaObject(_api.ForeignMinister, new CLuaAi(country)));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>
	/// <c>ForeignMinister_EvaluateDecision(minister, decision, scope)</c>, the country's foreign minister, the decision and
	/// the pass's scope handed over; the answer to the nearest. A failure, or an answer that is not a number, answers null
	/// with why, for the tick that asked to fail with - it is reported there, once.
	/// </summary>
	public int? EvaluateDecision(CGameState state, CCountry country, Sim.Script.CDecision decision,
		Sim.Script.CEventScope scope, out string? error)
	{
		_state = state;
		error = null;
		try
		{
			object? value = _lua.Call("ForeignMinister_EvaluateDecision", 1,
				new CLuaObject(_api.ForeignMinister, new CLuaAi(country)), new CLuaObject(_api.Decision, decision),
				new CLuaObject(_api.EventScope, scope))[0];
			if (value is double number)
			{
				return ToInteger(number);
			}
			error = $"ForeignMinister_EvaluateDecision({country.Tag}, {decision.Name}) answered {value ?? "nil"}, not a number";
		}
		catch (CLuaException e)
		{
			error = e.Message;
		}
		return null;
	}

	/// <summary>
	/// <c>ForeignMinister_OnWar(minister, joiner, enemy, war)</c>, a new pair at war told to the country's foreign
	/// minister. A failure is reported.
	/// </summary>
	public void ForeignOnWar(CGameState state, CCountry country, CCountryTag joiner, CCountryTag enemy, Sim.Wars.CWar war)
	{
		CGameState? asked = _state;
		_state = state;
		try
		{
			_lua.Call("ForeignMinister_OnWar", 0, new CLuaObject(_api.ForeignMinister, new CLuaAi(country)),
				_api.Tag(joiner), _api.Tag(enemy), new CLuaObject(_api.War, war));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
		finally
		{
			_state = asked ?? state;
		}
	}

	/// <summary>
	/// <c>TechMinister_Tick(minister, true, true)</c>, the country's tech minister handed over with both its arguments
	/// true, as a country nobody plays has them. A failure is reported.
	/// </summary>
	public void RunTech(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("TechMinister_Tick", 0, new CLuaObject(_api.TechMinister, new CLuaAi(country)), true, true);
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary><c>ProductionMinister_Tick(minister)</c>, the country's production minister handed over. A failure is reported.</summary>
	public void RunProduction(CGameState state, CCountry country)
	{
		_state = state;
		try
		{
			_lua.Call("ProductionMinister_Tick", 0, new CLuaObject(_api.ProductionMinister, new CLuaAi(country)));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>
	/// <c>BalanceProductionSliders(ai, country, 0, lendLease, consumer, production, supply, reinforce, upgrade,
	/// hasReinforceBonus)</c>: the AI's level, and each share's need over the country's whole IC in single precision -
	/// the lend-lease share's always nought, its need being nought. A failure is reported and answers false.
	/// </summary>
	public bool BalanceProductionSliders(CGameState state, CCountry country)
	{
		_state = state;
		float ic = country.TotalIc;
		object Need(EProductionCategory category)
		{
			float share = CProduction.Needed(state, country, category).Raw / 1000f / ic;
			return (double)(share > 0 ? share : 0f);
		}
		try
		{
			_lua.Call("BalanceProductionSliders", 0, new CLuaObject(_api.Ai, new CLuaAi(country)), _api.Of(country), 0, 0.0,
				Need(EProductionCategory.ConsumerGoods), Need(EProductionCategory.Production), Need(EProductionCategory.Supplies),
				Need(EProductionCategory.Reinforcement), Need(EProductionCategory.Upgrades),
				country.Modifier[ReinforcementBonus].Raw > 0);
			return true;
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
			return false;
		}
	}

	/// <summary>
	/// <c>BalanceLendLeaseSliders(ai, country, tags, values)</c>: the countries it gives lend-lease to, in order, and their
	/// weights, which the script sets and posts. A failure is reported.
	/// </summary>
	public void BalanceLendLeaseSliders(CGameState state, CCountry country)
	{
		_state = state;
		var tags = new List<object?>();
		var values = new List<object?>();
		foreach (Sim.Aid.CLendLeaseGift gift in country.LendLeaseGiving)
		{
			tags.Add(_api.Tag(gift.Receiver));
			values.Add(_api.Fixed(new CFixedPoint(gift.Weight)));
		}
		try
		{
			_lua.Call("BalanceLendLeaseSliders", 0, new CLuaObject(_api.Ai, new CLuaAi(country)), _api.Of(country),
				new CLuaObject(_api.ArrayCountryTag, new CLuaArray(tags)), new CLuaObject(_api.ArrayFix, new CLuaArray(values)));
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>The modifier a script reads as a reinforcement bonus.</summary>
	private const string ReinforcementBonus = "reinforcement_bonus";

	/// <summary>
	/// The hour's trading, side by side on states of its own, made the first time (<see cref="CTradeWorkers"/>).
	/// What the runs post reaches the state's outbox, and what they report is reported, in the order of the
	/// runs - an unbuilt thing once, whichever state met it.
	/// </summary>
	public void RunTrades(CGameState state, IReadOnlyList<(CCountry Country, bool Propose)> runs)
	{
		_workers ??= new CTradeWorkers(_directories);
		foreach ((List<Sim.Commands.CCommand> posted, List<string> reports) in _workers.Run(state, runs))
		{
			state.Outbox.AddRange(posted);
			foreach (string line in reports)
			{
				if (line.StartsWith(UnbuiltPrefix, StringComparison.Ordinal))
				{
					_api.ReportUnbuilt(line[UnbuiltPrefix.Length..]);
				}
				else
				{
					if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
					{
						_failures++;
					}
					_report(line);
				}
			}
		}
	}

	/// <summary>
	/// What a Lua expression answers with this game being asked - for a check or a test. A Lua error is
	/// a <see cref="CLuaException"/>.
	/// </summary>
	public object? Ask(CGameState state, string expression)
	{
		_state = state;
		_lua.Run(Encoding.Latin1.GetBytes("function __ohoi_ask() return " + expression + " end"), "=ask");
		return _lua.Call("__ohoi_ask", 1)[0];
	}

	public void Dispose()
	{
		_workers?.Dispose();
		_workers = null;
		_lua.Dispose();
	}

	/// <summary>
	/// <c>lua_tointeger</c> as the original's build has it: to the nearest, ties to even; out of range
	/// the x87's "indefinite", the lowest int.
	/// </summary>
	public static int ToInteger(double number)
	{
		double rounded = Math.Round(number, MidpointRounding.ToEven);
		return double.IsNaN(rounded) || rounded < int.MinValue || rounded > int.MaxValue ? int.MinValue : (int)rounded;
	}

	private void Fail(string message)
	{
		_failures++;
		_report(ErrorPrefix + message);
	}

	/// <summary>Runs a file of the install's, reporting it where it fails, as the original logs it.</summary>
	private void RunFile(string path)
	{
		if (Read(path) is not { } chunk)
		{
			Fail($"{path} is in no layer of the install");
			return;
		}
		try
		{
			_lua.Run(chunk, path);
		}
		catch (CLuaException e)
		{
			Fail(e.Message);
		}
	}

	/// <summary>
	/// The original's searcher, put at <c>package.loaders[2]</c>: <c>script/</c>, <c>script/country/</c>
	/// and <c>common/</c>, each over every layer, the chunk named by its bare path.
	/// </summary>
	private void InstallSearcher()
	{
		_lua.SetGlobal("__ohoi_searcher", (CLuaFunction)Search);
		_lua.Run(Encoding.Latin1.GetBytes("table.insert(package.loaders, 2, __ohoi_searcher) __ohoi_searcher = nil"),
			"=OpenHOI3");
	}

	private int Search(CLuaCall call)
	{
		string name = call.String(1);
		foreach (string folder in ModuleFolders)
		{
			string path = folder + name + ".lua";
			if (Read(path) is { } chunk)
			{
				// A chunk that does not parse is the error require raises.
				if (!call.Load(chunk, path))
				{
					return -1;
				}
				return 1;
			}
		}
		return call.Return($"\n\tno file in the install's {string.Join(", ", ModuleFolders)} for '{name}'");
	}

	private byte[]? Read(string gameRelative)
	{
		string path = _directories.Resolve(gameRelative);
		return File.Exists(path) ? File.ReadAllBytes(path) : null;
	}
}
