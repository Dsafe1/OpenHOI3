using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Sim.Rules;

/// <summary>
/// The fixes by the names a mod's file, a save and the Lua know them under - lower case with underscores, matched case
/// and all. A name, once given out, is kept: a mod's file and every enhanced save hold it.
/// </summary>
public static class CFixes
{
	private static readonly (EFix Fix, string Name)[] Table =
	{
		(EFix.MinisterTechDecay, "minister_tech_decay"),
	};

	/// <summary>Every fix, in the order of their names.</summary>
	public static IEnumerable<EFix> All => Table.OrderBy(entry => entry.Name, StringComparer.Ordinal).Select(entry => entry.Fix);

	/// <summary>A fix's name.</summary>
	public static string Name(EFix fix)
	{
		foreach ((EFix candidate, string name) in Table)
		{
			if (candidate == fix)
			{
				return name;
			}
		}
		throw new ArgumentOutOfRangeException(nameof(fix), fix, "a fix with no name");
	}

	/// <summary>The fix of a name, or none where no fix has it.</summary>
	public static EFix? Named(string name)
	{
		foreach ((EFix fix, string candidate) in Table)
		{
			if (string.Equals(candidate, name, StringComparison.Ordinal))
			{
				return fix;
			}
		}
		return null;
	}
}
