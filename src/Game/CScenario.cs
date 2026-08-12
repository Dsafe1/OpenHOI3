using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenHOI3.Core;

namespace OpenHOI3.Game;

/// <summary>
/// A hand-authored battle scenario, as opposed to a grand campaign start date. Each
/// one is a file in <c>scenarios/</c> naming its dates, its artwork and the supporting
/// files that carry its events and victory conditions.
/// </summary>
public sealed class CScenario
{
	/// <summary>File name without extension; also the folder holding its data.</summary>
	public required string Id { get; init; }

	public required string Name { get; init; }
	public required string Description { get; init; }
	public string Icon { get; init; } = "";

	public required CDate StartDate { get; init; }
	public CDate EndDate { get; init; } = CDate.Zero;

	public override string ToString()
	{
		return $"{Id} ({StartDate})";
	}
}

/// <summary>
/// The scenarios in <c>scenarios/</c>, ordered by start date. Expansion copies replace
/// base ones of the same name through the usual layering.
/// </summary>
public sealed class CScenarios
{
	private const string Directory = "scenarios";

	private readonly List<CScenario> _scenarios = new();

	public int Count => _scenarios.Count;

	public IReadOnlyList<CScenario> All => _scenarios;

	public static CScenarios Load(CDirectorySettings directories, CLocalisation localisation,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var scenarios = new CScenarios();

		foreach (string file in directories.ListFiles(Directory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e)
			{
				warn($"Could not read scenario '{file}': {e.Message}");
				continue;
			}

			if (!CDate.TryParse(root.GetString("startdate"), out CDate start))
			{
				// Supporting files sit in the same folder; only the ones carrying a
				// start date are scenarios.
				continue;
			}
			CDate.TryParse(root.GetString("enddate"), out CDate end);

			scenarios._scenarios.Add(new CScenario
			{
				Id = Path.GetFileNameWithoutExtension(file),
				Name = localisation.Localise(root.GetString("name")),
				Description = localisation.Localise(root.GetString("desc")),
				Icon = root.GetString("icon"),
				StartDate = start,
				EndDate = end,
			});
		}

		scenarios._scenarios.Sort((a, b) => a.StartDate.CompareTo(b.StartDate));
		return scenarios;
	}
}
