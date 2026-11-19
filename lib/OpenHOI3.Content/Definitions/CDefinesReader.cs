using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/defines.lua</c>. The original keeps its tuning numbers in a Lua
/// table: one outer table holding a few plain values and a table for each section.
/// Nothing in it is computed, so it is read as data and no Lua is run.
/// </summary>
public static class CDefinesReader
{
	private const string DefinesFile = "common/defines.lua";

	/// <summary>The defines this project reads. One that is missing is reported, and reads as zero.</summary>
	private static readonly (string Section, string Name)[] Used =
	{
		(CResources.EconomySection, CResources.IcToMoney),
		(CResources.EconomySection, CProduction.IcToSupplies),
		(CResources.EconomySection, CLeadership.LeadershipToOfficers),
		(CResources.EconomySection, CLeadership.LeadershipToDiplomacy),
		(CResources.EconomySection, CLeadership.LeadershipToSpies),
		(CManpower.MilitarySection, CManpower.MaxManpower),
		(CResearch.CountrySection, CResearch.YearImpact),
		(CResearch.CountrySection, CResearch.BaseDecay),
		(CResearch.CountrySection, CResearch.MostAbility),
		(CResearch.CountrySection, CResearch.AbilityGainDivisor),
		(CUnitUpkeep.CountrySection, CUnitUpkeep.LowSupplyFactor),
		(CManpower.MilitarySection, CReinforcement.PrideBonus),
		(CManpower.MilitarySection, CPride.RedesignationDissent),
		(CManpower.MilitarySection, CUpgrades.ShipSpeed),
		(CManpower.MilitarySection, CUpgrades.AirSpeed),
		(CManpower.MilitarySection, CUpgrades.LandSpeed),
		(CManpower.MilitarySection, CBrigadeUpgrades.CostDefine),
		(CManpower.MilitarySection, CBrigadeUpgrades.TimeDefine),
		(CManpower.MilitarySection, CBrigadeUpgrades.PracticalDefine),
		(CManpower.MilitarySection, COfficers.MostRatio),
		(CManpower.MilitarySection, COrganisation.LowRegainBonus),
		(CManpower.MilitarySection, CBaseCrowding.NavalBaseEfficiency),
	};

	/// <summary>Every numeric value in a section, by section and name, and every value at all as it is written.</summary>
	public static CDefines Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(DefinesFile);
		var defines = new CDefines();
		foreach (((string section, string name), string text) in ReadText(path))
		{
			defines.SetText(section, name, text);
			if (section.Length != 0 && CFixedPoint.TryParse(text, out CFixedPoint value))
			{
				defines.Set(section, name, value);
			}
		}

		// A define the file does not give has no line of its own: each is said of the file.
		foreach ((string section, string name) in Used)
		{
			if (!defines.Has(section, name))
			{
				warn(CDiagnostics.Located(path, 0, $"does not give {section}.{name}; it is taken as zero"));
			}
		}
		return defines;
	}

	/// <summary>
	/// Every value in the file as written, quotes removed, by section and name. A value
	/// outside any section has an empty section.
	/// </summary>
	public static Dictionary<(string Section, string Name), string> ReadText(string path)
	{
		var values = new Dictionary<(string, string), string>();
		List<string> tokens = Tokenise(Encoding.Latin1.GetString(File.ReadAllBytes(path)));

		// Depth 1 is inside the outer table, depth 2 inside a section. Anything deeper
		// is skipped: the file has nothing there.
		int depth = 0;
		string section = "";
		for (int i = 0; i < tokens.Count; i++)
		{
			string token = tokens[i];
			if (token == "{")
			{
				depth++;
				continue;
			}
			if (token == "}")
			{
				depth--;
				if (depth <= 1)
				{
					section = "";
				}
				continue;
			}
			if (i + 2 >= tokens.Count || tokens[i + 1] != "=")
			{
				continue;
			}

			string value = tokens[i + 2];
			if (value == "{")
			{
				if (depth == 1)
				{
					section = token;
				}
				i++;                                             // the brace is counted on the next turn
				continue;
			}
			if (depth == 1 || depth == 2)
			{
				values[(depth == 2 ? section : "", token)] = value;
			}
			i += 2;
		}
		return values;
	}

	/// <summary>Names, numbers, strings without their quotes, braces and equals signs. Commas and comments are dropped.</summary>
	private static List<string> Tokenise(string text)
	{
		var tokens = new List<string>();
		int i = 0;
		while (i < text.Length)
		{
			char c = text[i];
			if (char.IsWhiteSpace(c) || c == ',' || c == ';')
			{
				i++;
			}
			else if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
			{
				while (i < text.Length && text[i] != '\n')
				{
					i++;
				}
			}
			else if (c == '{' || c == '}' || c == '=')
			{
				tokens.Add(c.ToString());
				i++;
			}
			else if (c == '\'' || c == '"')
			{
				int end = text.IndexOf(c, i + 1);
				if (end < 0)
				{
					end = text.Length;
				}
				tokens.Add(text[(i + 1)..end]);
				i = Math.Min(end + 1, text.Length);
			}
			else
			{
				int start = i;
				while (i < text.Length && !char.IsWhiteSpace(text[i]) && ",;{}=".IndexOf(text[i]) < 0
					&& !(text[i] == '-' && i + 1 < text.Length && text[i + 1] == '-'))
				{
					i++;
				}
				tokens.Add(text[start..i]);
			}
		}
		return tokens;
	}
}
