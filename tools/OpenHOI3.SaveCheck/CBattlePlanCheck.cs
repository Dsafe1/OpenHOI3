using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.SaveCheck;

/// <summary>Reads every historical battle plan of an install and writes it back, setting the two side by side token by token.</summary>
public static class CBattlePlanCheck
{
	public static int Run(string install)
	{
		CDirectorySettings directories = CToolDirectories.At(install);
		int empty = 0, same = 0, different = 0;
		foreach (string folder in directories.LayerFolders("battleplans/historical").Where(Directory.Exists))
		{
			foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
			{
				CBattlePlan header = CBattlePlanFile.ReadHeader(file);
				if (!header.HeaderGood)
				{
					empty++;
					continue;
				}
				string text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(file));
				string written = CBattlePlanFile.Write(CBattlePlanFile.Read(ClausewitzParser.Parse(text)));
				string[] one = Tokens(text), two = Tokens(written);
				if (one.SequenceEqual(two))
				{
					same++;
				}
				else
				{
					different++;
					int at = Enumerable.Range(0, Math.Min(one.Length, two.Length)).FirstOrDefault(index => one[index] != two[index], Math.Min(one.Length, two.Length));
					Console.WriteLine($"{Path.GetFileName(file)}: token {at}: {(at < one.Length ? one[at] : "(end)")} against {(at < two.Length ? two[at] : "(end)")}");
				}
			}
		}
		Console.WriteLine($"battle plans: {same} written back the same, {different} not, {empty} with no header");
		return different == 0 ? 0 : 1;
	}

	private static string[] Tokens(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}
