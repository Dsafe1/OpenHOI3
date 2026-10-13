using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The classes the original hands its scripts, by its names, with the methods built so far on the
/// simulation. A method a script calls that is not here fails as a Lua error naming it, so the
/// script stops there and the call it was part of is reported; a method that reads what the
/// simulation does not hold answers nought and reports that once. See <c>docs/mechanics/lua.md</c>,
/// section 4.
/// </summary>
public sealed class CGameApi
{
	/// <summary>The null tag, the original's country number 0, which nothing is.</summary>
	public static readonly CCountryTag NullTag = new("---");

	private readonly Func<CGameState> _state;
	private readonly Action<string> _report;
	private readonly HashSet<string> _unbuilt = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _governments = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _factions = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _ideologies = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _continents = new(StringComparer.Ordinal);

	public CGameApi(Func<CGameState> state, Action<string>? report = null)
	{
		_state = state;
		_report = report ?? (_ => { });
		Text = CStringClass.Define();
		FixedPoint = CFixedPointClass.Define();
		Date = CDateClass.Define();
		Government = new CLuaClass("CGovernment").Operator("__eq", CLuaEquality.Same<CLuaNamed>("CGovernment"));
		Faction = CFactionClass.Define(this);
		IdeologyGroup = CIdeologyClass.DefineGroup(this);
		Ideology = CIdeologyClass.DefineIdeology(this);
		Continent = CProvinceClass.DefineContinent(this);
		Province = CProvinceClass.DefineProvince(this);
		GoodsPool = CGoodsPoolClass.Define(this);
		Strategy = CAiClass.DefineStrategy(this);
		Country = CCountryClass.Define(this);
		CountryTag = CCountryTagClass.Define(this);
		DiplomacyStatus = CDiplomacyStatusClass.Define(this);
		Ai = CAiClass.DefineAi(this);
		ResourceValues = CResourceValuesClass.Define(this);
		GameState = CGameStateClass.DefineGameState(this);
		CountryDataBase = CGameStateClass.DefineCountryDataBase(this);
		Intel = new CLuaClass("CAIIntel").Constructs(_ => throw new CLuaError("CAIIntel is not built: espionage is not"));
	}

	public CLuaClass Country { get; }

	public CLuaClass CountryTag { get; }

	public CLuaClass DiplomacyStatus { get; }

	public CLuaClass Government { get; }

	public CLuaClass Faction { get; }

	public CLuaClass Ideology { get; }

	public CLuaClass IdeologyGroup { get; }

	public CLuaClass Province { get; }

	public CLuaClass Continent { get; }

	public CLuaClass GoodsPool { get; }

	public CLuaClass Strategy { get; }

	public CLuaClass Ai { get; }

	public CLuaClass ResourceValues { get; }

	public CLuaClass GameState { get; }

	public CLuaClass CountryDataBase { get; }

	public CLuaClass Intel { get; }

	public CLuaClass FixedPoint { get; }

	public CLuaClass Text { get; }

	public CLuaClass Date { get; }

	public IEnumerable<CLuaClass> Classes => new[]
	{
		Country, CountryTag, DiplomacyStatus, Government, Faction, Ideology, IdeologyGroup, Province, Continent,
		GoodsPool, Strategy, Ai, ResourceValues, GameState, CountryDataBase, Intel, FixedPoint, Text, Date,
	}.Concat(CEnumClasses.Define());

	/// <summary>What has been reported as not built, in the order it was first met.</summary>
	public IReadOnlyCollection<string> Unbuilt => _unbuilt;

	/// <summary>The game being asked.</summary>
	public CGameState State => _state();

	/// <summary>Says once a session that a script read something the simulation does not hold.</summary>
	public void ReportUnbuilt(string what)
	{
		if (_unbuilt.Add(what))
		{
			_report("LUA unbuilt: " + what);
		}
	}

	/// <summary>A country, as a script is handed one.</summary>
	public CLuaObject Of(CCountry country) => new(Country, country);

	/// <summary>The country of a tag, or null for the null tag and a tag nobody has.</summary>
	public CLuaObject? OfTag(CCountryTag tag) => State.Country(tag) is { } country ? Of(country) : null;

	/// <summary>A tag, as a script is handed one: a fresh copy each time, as the original pushes it.</summary>
	public CLuaObject Tag(CCountryTag? tag) => new(CountryTag, tag ?? NullTag);

	/// <summary>A figure in thousandths, as a <c>CFixedPoint</c>.</summary>
	public CLuaObject Fixed(CFixedPoint value) => new(FixedPoint, value);

	/// <summary>A text, as a <c>CString</c>.</summary>
	public CLuaObject String(string text) => new(Text, new CLuaText(text));

	public CLuaObject GovernmentNamed(string name) => new(Government, Named(_governments, name));

	/// <summary>A faction by its tag - <c>axis</c> - the empty one being the null faction.</summary>
	public CLuaObject FactionNamed(string name) => new(Faction, Named(_factions, name));

	public CLuaObject IdeologyNamed(string name) => new(Ideology, Named(_ideologies, name));

	public CLuaObject ContinentNamed(string name) => new(Continent, Named(_continents, name));

	/// <summary>The country named by a tag argument.</summary>
	public CCountry? CountryOf(CLuaCall call, int argument) => State.Country(call.Object<CCountryTag>(argument, "CCountryTag"));

	private static CLuaNamed Named(Dictionary<string, CLuaNamed> known, string name)
	{
		if (!known.TryGetValue(name, out CLuaNamed? named))
		{
			known[name] = named = new CLuaNamed(name);
		}
		return named;
	}
}
