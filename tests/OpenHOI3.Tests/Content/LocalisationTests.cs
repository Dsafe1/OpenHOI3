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
	public void LookupIsCaseInsensitive()
	{
		Write("localisation/frontend.csv", "FE_EXIT;Exit;;;;;;;;;;;;;x\n");
		Assert.Equal("Exit", Load().Localise("fe_exit"));
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
	public void ExpansionOnlyFileOverridesBaseOnlyFile()
	{
		// Different names, so both load; the expansion's must be applied last even
		// though "aaa" sorts before "frontend".
		Write("localisation/frontend.csv", "FE_EXIT;Base Exit;;;;;;;;;;;;;x\n");
		Write("tfh/localisation/aaa_tfh.csv", "FE_EXIT;Expansion Exit;;;;;;;;;;;;;x\n");

		Assert.Equal("Expansion Exit", Load().Localise("FE_EXIT"));
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
