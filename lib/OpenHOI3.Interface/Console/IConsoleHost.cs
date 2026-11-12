using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Console;

/// <summary>What the console's commands ask of the screen they are typed on.</summary>
public interface IConsoleHost
{
	CGameState State { get; }

	CCountry? Player { get; }

	/// <summary>The provinces selected on the map.</summary>
	IReadOnlyList<int> SelectedProvinces { get; }

	bool TooltipsOn { get; set; }

	bool TooltipDebug { get; set; }

	bool ContinentPainting { get; set; }

	/// <summary>Runs a change to the game at once, as every change goes: through the queue.</summary>
	void Post(CCommand command);

	void CopyToClipboard(string text);

	/// <summary>The first unit selected on the map.</summary>
	CUnit? SelectedUnit { get; }

	/// <summary>Whether a command typed now would be run: a replay takes none.</summary>
	bool TakesCommands { get; }

	/// <summary>The bits of the map's layers the console has left on.</summary>
	int RenderLayers { get; set; }

	/// <summary>The bits the map mode now shown leaves on.</summary>
	int ModeLayers { get; }

	bool Fullscreen { get; set; }

	bool Wireframe { get; set; }

	/// <summary>Whether an effect file of that name, or of that name in <c>gfx/FX</c>, is there.</summary>
	bool EffectExists(string name);

	/// <summary>The screens made again for the country now played.</summary>
	void ShowCountryPlayed();

	/// <summary>The interface and the map's effects read again from the install, and everything made again from them.</summary>
	void ReloadInterface();

	/// <summary>
	/// The soundtrack's next song picked as a track's end picks it and played at once: its path, or nothing where none could be
	/// (<c>docs/mechanics/music.md</c>, section 2.4).
	/// </summary>
	string NextSong();

	/// <summary>The map's colours made again.</summary>
	void RefreshMapColours();

	/// <summary>Writes the whole game to <c>logs/&lt;name&gt;.log</c> in the player's folder.</summary>
	void WriteDetailedLog(string name);

	/// <summary>The closest the camera may come, as a height.</summary>
	void SetLeastHeight(int height);

	/// <summary>A line for the log.</summary>
	void Log(string line);
}
