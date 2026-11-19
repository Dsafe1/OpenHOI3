using System;
using System.IO;
using System.Text;
using OpenHOI3.Content;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Localisation files are semicolon separated, one language per column, and predate
/// UTF-8. Getting the encoding or the override order wrong shows up as mojibake or as
/// the wrong expansion's wording, never as an error.
/// </summary>
public class LocalisationTests : IDisposable
{
	private readonly string _root;

	public LocalisationTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-loc-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "localisation"));
		Directory.CreateDirectory(Path.Combine(_root, "tfh", "localisation"));
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
		}
		GC.SuppressFinalize(this);
	}

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
	}

	private CLocalisation Load() => CLocalisation.Load(CDirectorySettings.At(_root));

	[Fact]
	public void ReadsTheEnglishColumn()
	{
		Write("localisation/frontend.csv",
			"FE_SINGLE_PLAYER;Single Player;Un joueur;Einzelspieler;;;;;;;;;;;x\n");

		Assert.Equal("Single Player", Load().Localise("FE_SINGLE_PLAYER"));
	}

	[Fact]
	public void UnknownKeyComesBackUnchanged()
	{
		// Unlocalised interface stays identifiable instead of going blank.
		Assert.Equal("FE_NOT_THERE", Load().Localise("FE_NOT_THERE"));
	}

	[Fact]
	public void EmptyKeyGivesEmptyString()
	{
		Assert.Equal(string.Empty, Load().Localise(""));
	}

	[Fact]
	public void AKeyIsMatchedCaseAndAll()
	{
		// The original's text keys are an exact, case-sensitive set (reversing/findings/FINDINGS-localisation.md, section 2):
		// the base game's MANPOWER and manpower are two keys with two texts, not one given twice.
		Write("localisation/misc.csv", "MANPOWER;Manpower Increase;;;;;;;;;;;;;x\nmanpower;Manpower;;;;;;;;;;;;;x\n");

		CLocalisation localisation = Load();
		Assert.Equal("Manpower Increase", localisation.Localise("MANPOWER"));
		Assert.Equal("Manpower", localisation.Localise("manpower"));
		// A third spelling is no key: it comes back as itself.
		Assert.Null(localisation.Find("ManPower"));
		Assert.Equal(0, localisation.Duplicates);
	}

	[Fact]
	public void SkipsCommentsAndBlankLines()
	{
		Write("localisation/frontend.csv",
			"#comment;should not become a key\n\nFE_EXIT;Exit;;;;;;;;;;;;;x\n");

		CLocalisation localisation = Load();
		Assert.Equal("Exit", localisation.Localise("FE_EXIT"));
		Assert.Equal(1, localisation.Count);
	}

	[Fact]
	public void KeepsWesternEuropeanCharacters()
	{
		// The files are Windows-1252; a UTF-8 read would mangle these.
		Write("localisation/countries.csv", "GER;Deutschland;;Königreich;;;;;;;;;;;x\n");

		Assert.Equal("Deutschland", Load().Localise("GER"));
	}

	[Fact]
	public void ExpansionOverridesBaseWording()
	{
		Write("localisation/frontend.csv", "FE_EXIT;Base Exit;;;;;;;;;;;;;x\n");
		Write("tfh/localisation/frontend.csv", "FE_EXIT;Expansion Exit;;;;;;;;;;;;;x\n");

		// Same file name in both layers resolves to the expansion's copy only.
		Assert.Equal("Expansion Exit", Load().Localise("FE_EXIT"));
	}

	[Fact]
	public void OfTwoFilesTheOneSortingFirstGivesTheText()
	{
		// Different names, so both load, in the order of their names over all layers:
		// "aaa" sorts before "frontend", and the first text a key is given stands.
		Write("localisation/frontend.csv", "FE_EXIT;Base Exit;;;;;;;;;;;;;x\n");
		Write("tfh/localisation/aaa_tfh.csv", "FE_EXIT;Expansion Exit;;;;;;;;;;;;;x\n");

		Assert.Equal("Expansion Exit", Load().Localise("FE_EXIT"));
	}

	[Fact]
	public void ABaseFileSortingFirstBeatsAnExpansionFileSortingLater()
	{
		// No layer comes first: a base game file before an expansion's by name gives the text.
		Write("localisation/aaa_base.csv", "FE_EXIT;Base Exit;;;;;;;;;;;;;x\n");
		Write("tfh/localisation/zzz_tfh.csv", "FE_EXIT;Expansion Exit;;;;;;;;;;;;;x\n");

		Assert.Equal("Base Exit", Load().Localise("FE_EXIT"));
	}

	[Fact]
	public void ALaterLineOfAKeyWithTextIsDroppedAsTheOriginalDropsItsDuplicates()
	{
		// BlackICE's zDD-tech.csv gives TECH_INVEST twice; the original shows the first.
		Write("localisation/tech.csv",
			"TECH_INVEST;$TECH$ $DIRECTION$ $VALUE$;;;;;;;;;;;;;x\nTECH_INVEST;Gain Tech.\\n;;;;;;;;;;;;;x\n");

		CLocalisation localisation = Load();
		Assert.Equal("$TECH$ $DIRECTION$ $VALUE$", localisation.Localise("TECH_INVEST"));
		Assert.Equal(1, localisation.Duplicates);
	}

	[Fact]
	public void ALineWithNoTextInTheLanguageLeavesTheKeyToALaterOne()
	{
		// A placeholder - BlackICE's naval technology names have them - does not hold the key.
		Write("localisation/a.csv", "USA_smallwarship_asw_0;;;;;;;;;;;;;;x\n");
		Write("localisation/b.csv", "USA_smallwarship_asw_0;Mk4 DC;;;;;;;;;;;;;x\n");

		Assert.Equal("Mk4 DC", Load().Localise("USA_smallwarship_asw_0"));
	}

	[Fact]
	public void ADuplicateAndAnEmptyKeyAreEachReportedAtTheirFileAndLine()
	{
		Write("localisation/tech.csv",
			"#comment;;;;;;;;;;;;;;x\nTECH_INVEST;$TECH$ $DIRECTION$ $VALUE$;;;;;;;;;;;;;x\nTECH_INVEST;Gain Tech.\\n;;;;;;;;;;;;;x\nEMPTY;;;;;;;;;;;;;;x\n");
		var problems = new System.Collections.Generic.List<string>();

		CLocalisation localisation = CLocalisation.Load(CDirectorySettings.At(_root), warn: problems.Add);

		string file = Path.Combine(_root, "localisation", "tech.csv");
		Assert.Equal(new[]
		{
			$"{file}:3: 'TECH_INVEST' is given again and dropped; its first text, at {file}:2, stands",
			$"{file}:4: 'EMPTY' has no English text",
		}, problems);
		Assert.Equal(1, localisation.Duplicates);
		Assert.Equal(1, localisation.Empties);
	}

	[Fact]
	public void AProblemsLineNamesItsFileAndLineAsACompilerWould()
	{
		Assert.Equal("a.csv:3: what", CDiagnostics.Located("a.csv", 3, "what"));
		Assert.Equal("a.csv: what", CDiagnostics.Located("a.csv", 0, "what"));
		Assert.Equal("what", CDiagnostics.Located("", 3, "what"));
	}

	[Fact]
	public void IgnoresRowsWithoutATranslation()
	{
		Write("localisation/frontend.csv", "FE_EMPTY;;;;;;;;;;;;;;x\nFE_EXIT;Exit;;;;;;;;;;;;;x\n");

		CLocalisation localisation = Load();
		Assert.Equal("FE_EMPTY", localisation.Localise("FE_EMPTY"));
		Assert.Equal("Exit", localisation.Localise("FE_EXIT"));
	}

	[Fact]
	public void HandlesCarriageReturnsAndTrailingCommas()
	{
		// Real rows end with "x" and sometimes trailing commas, with CRLF endings.
		Write("localisation/frontend.csv", "FE_TUTORIAL;Tutorial;;;;;;;;;;;;;x,,,,,,\r\n");

		Assert.Equal("Tutorial", Load().Localise("FE_TUTORIAL"));
	}
}
