using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Tests.Content;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The strip of ground conditions and effects are tried on:
///
///     5 Sea --- 1 Berlin --- 2 Border --- 3 Posen --- 4 Warsaw --- 6 Wilds     7 Rome
///
/// Germany owns 1 to 3 and Poland 4; Italy owns Rome, alone; the wilds are nobody's. Posen is a
/// core of Germany and of Slovakia, which owns nothing and has its capital there. Germany and Italy
/// are the axis, and 1 and 2 are the region of sudetenland. Berlin has a port on the sea. One event
/// modifier, <c>us_new_deal</c>, gives 10% IC.
/// </summary>
internal static class ScriptWorld
{
	public static readonly CCountryTag Ger = new("GER");
	public static readonly CCountryTag Pol = new("POL");
	public static readonly CCountryTag Ita = new("ITA");
	public static readonly CCountryTag Slo = new("SLO");
	public static readonly CCountryTag Reb = new("REB");
	public static readonly CBuilding Industry = new(0, "industry", 10, "", CFixedPoint.Zero);
	public static readonly CBuilding NavalBase = new(1, "naval_base", 10, "", CFixedPoint.Zero);

	public static CGameState State(bool atWar = false, IReadOnlyList<CIdeologyGroup>? groups = null)
	{
		CCountry Country(CCountryTag tag, int capital) => new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };
		CCountry germany = Country(Ger, 1);
		germany.Faction = "axis";
		germany.Manpower = CFixedPoint.FromInt(50);
		germany.MaxManpower = CFixedPoint.FromInt(200);
		germany.Laws["press_laws"] = new CLaw("press_laws", new CModifier("free_press"));
		var head = new CMinister(1001, "Head");
		head.SetType("head_of_state", new CMinisterType(new CModifier("stern_imperialist")));
		germany.Ministers[1001] = head;
		germany.Government["head_of_state"] = head;
		var other = new CMinister(1002, "Other");
		other.SetType("head_of_state", new CMinisterType(new CModifier("stern_imperialist")));
		germany.Ministers[1002] = other;
		CCountry italy = Country(Ita, 7);
		italy.Faction = "axis";

		CProvince Land(int id, CCountryTag? owner, params int[] beside)
		{
			var province = new CProvince(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner };
			province.Edges = System.Array.ConvertAll(beside, to => new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			return province;
		}
		var berlin = new CProvince(1, "p1", isSea: false) { Owner = Ger, Controller = Ger, PortSea = 5 };
		berlin.Edges = new[] { new CProvinceEdge(5, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero), new CProvinceEdge(2, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) };
		berlin.Cores.Add(Ger);
		berlin.Regions.Add(0);
		CProvince border = Land(2, Ger, 1, 3);
		border.Regions.Add(0);
		border.SetBuildingLevel(Industry, CFixedPoint.FromInt(3));
		CProvince posen = Land(3, Ger, 2, 4);
		posen.Cores.Add(Ger);
		posen.Cores.Add(Slo);
		var sea = new CProvince(5, "Sea", isSea: true) { Edges = new[] { new CProvinceEdge(1, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero) } };

		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		var newDeal = new CModifier("us_new_deal");
		newDeal.Add("ic", CFixedPoint.Parse("0.1"));
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), berlin, border, posen, Land(4, Pol, 3, 6), sea, Land(6, null, 4),
				Land(7, Ita),
			},
			new[] { germany, Country(Pol, 4), italy, Country(Slo, 3), Country(Reb, 0) },
			buildings: new[] { Industry, NavalBase }, staticModifiers: new[] { newDeal }, wars: wars)
		{
			Regions = new[] { new CRegion("sudetenland", new[] { 1, 2 }, useForPeace: false) },
			IdeologyGroups = groups ?? System.Array.Empty<CIdeologyGroup>(),
		};
	}

	private static CScriptContext Context()
	{
		CScriptContext context = ScriptFixture.Context();
		foreach (string tag in new[] { "POL", "SLO" })
		{
			context.Names.AddCountry(new CCountryTag(tag));
		}
		context.Names.Add(EScriptName.Building, "naval_base");
		return context;
	}

	public static CTrigger Condition(string text)
	{
		CScriptContext context = Context();
		CTrigger all = CTrigger.All();
		CTriggerReader.ReadInto(all, ScriptFixture.Parse(text, context), context);
		return all;
	}

	public static List<CEffect> Effects(string text)
	{
		CScriptContext context = Context();
		var effects = new List<CEffect>();
		CEffectReader.ReadInto(effects, ScriptFixture.Parse(text, context), context);
		return effects;
	}
}
