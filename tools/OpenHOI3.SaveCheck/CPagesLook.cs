using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Windows;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

/// <summary>
/// A look at the windows a mod defines with no screen (<c>docs/mechanics/modwindows.md</c>): which pages there are, by
/// tab, and - given a page's name - what that page draws on a new game: gathered as it opens, the hours given run
/// with the install's Lua at work, gathered again and drawn into a canvas that keeps what it was asked
/// (<see cref="CPrintingCanvas"/>). Every Lua error is told.
///
///   OpenHOI3.SaveCheck pages &lt;install folder&gt; [page] [hours] [tag] [sort=COLUMN] [mod=NAME]
///
/// Germany for 30 hours with nothing given. Nothing is pressed, so a page is seen as it first stands - but a sorted
/// table answers the heading given as the one clicked: <c>sort=4</c> the fourth column, the smallest first,
/// <c>sort=-4</c> the largest first.
/// </summary>
internal static class CPagesLook
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck pages <install folder> [page] [hours] [tag] [sort=COLUMN] [mod=NAME]");
			return 2;
		}
		const string sortWord = "sort=";
		int sort = args.Where(arg => arg.StartsWith(sortWord, StringComparison.Ordinal))
			.Select(arg => int.Parse(arg[sortWord.Length..], System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();
		args = args.Where(arg => !arg.StartsWith(sortWord, StringComparison.Ordinal)).ToArray();
		string? wanted = args.Length > 2 ? args[2] : null;
		int hours = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 30;
		var tag = new CCountryTag(args.Length > 4 ? args[4].ToUpperInvariant() : "GER");

		var warnings = new List<string>();
		var reports = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		using var display = new CAiScripts(directories, reports.Add);
		var windows = new CLuaWindows(display, directories, localisation.Localise, reports.Add);

		Console.WriteLine($"{windows.Pages.Count} pages:");
		foreach (CModPageGroup group in CModPageGroup.Of(windows.Pages))
		{
			Console.WriteLine($"  {localisation.Localise(group.Key.Length == 0 ? "OPENHOI_WINDOWS_UNGROUPED" : group.Key)}: " +
				string.Join(", ", group.Pages.Select(page => page.Name)));
		}
		int failed = Tell(reports);
		if (wanted is null)
		{
			return failed == 0 ? 0 : 1;
		}
		// With a mod named, an argument that could be a file's path has been made whole (CToolDirectories): a page's
		// name, which has a folder in it, is then the end of what is given.
		string named = wanted.Replace('\\', '/');
		if (windows.Pages.FirstOrDefault(each => named == each.Name || named.EndsWith("/" + each.Name, StringComparison.Ordinal)) is not { } page)
		{
			Console.Error.WriteLine($"no page is named {wanted}");
			return 2;
		}

		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		using var scripts = new CAiScripts(directories, _ => { });
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { tag });
		var simulation = new CSimulation(state);
		windows.Collect(state, page, tag);
		simulation.RunHours(hours);
		simulation.Pump();
		windows.Collect(state, page, tag);
		(IModWindowCanvas canvas, CPrintingCanvas kept) = CPrintingCanvas.Create();
		if (sort != 0)
		{
			kept.Sort = (Math.Abs(sort) - 1, sort < 0);
		}
		windows.Draw(page, tag, canvas);

		Console.WriteLine();
		Console.WriteLine($"{page.Name} as {tag} on {state.Date}, {hours} hours on, {kept.Calls.Count} calls of the canvas:");
		foreach (string line in kept.Lines())
		{
			Console.WriteLine(line);
		}
		return Tell(reports) + failed == 0 ? 0 : 1;
	}

	/// <summary>Tells what the Lua reported since the last telling, and how many they were.</summary>
	private static int Tell(List<string> reports)
	{
		foreach (string report in reports)
		{
			Console.WriteLine($"  ! {report}");
		}
		int count = reports.Count;
		reports.Clear();
		return count;
	}
}
