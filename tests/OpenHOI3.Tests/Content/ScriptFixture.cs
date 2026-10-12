using System.Linq;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The names script is read against in the tests: a few of each kind, made up here in the
/// shape of the install's and not copied from it.
/// </summary>
internal static class ScriptFixture
{
	public static CScriptContext Context()
	{
		var names = new CScriptNames { ProvinceCount = 100 };
		foreach (string tag in new[] { "GER", "ENG", "ITA", "PHI", "ROM", "REB" })
		{
			names.AddCountry(new CCountryTag(tag));
		}
		names.Add(EScriptName.Building, "industry");
		names.Add(EScriptName.Building, "land_fort");
		names.Add(EScriptName.Technology, "industry");
		names.Add(EScriptName.Technology, "infantry_activation");
		names.Add(EScriptName.TechnologyCategory, "infantry_folder");
		names.Add(EScriptName.UnitType, "infantry_brigade");
		names.Add(EScriptName.GovernmentPosition, "head_of_state");
		names.AddLaw("conscription_law", "service_by_requirement");
		names.AddLaw("press_laws", "free_press");
		names.Add(EScriptName.CombinedArms, "armor_unit_type");
		names.Add(EScriptName.Ideology, "stalinist");
		names.Add(EScriptName.IdeologyGroup, "communism");
		names.Add(EScriptName.Faction, "axis");
		names.Add(EScriptName.Government, "national_socialism");
		names.Add(EScriptName.Region, "sudetenland");
		names.Add(EScriptName.RebelType, "partisans");
		names.Add(EScriptName.Modifier, "us_new_deal");
		names.Add(EScriptName.CasusBelli, "conquer");
		names.Add(EScriptName.Continent, "europe");
		names.Add(EScriptName.OrderOfBattle, "AnschlussOOB.txt");
		return new CScriptContext(names) { Problems = { File = "events/test.txt" } };
	}

	public static CScriptNode Parse(string text, CScriptContext context)
	{
		return CScriptParser.Parse(text, context.Problems);
	}

	/// <summary>The problems of one kind, as their messages.</summary>
	public static string[] Of(CScriptContext context, EScriptProblem kind)
	{
		return context.Problems.All.Where(p => p.Kind == kind).Select(p => p.Message).ToArray();
	}
}
