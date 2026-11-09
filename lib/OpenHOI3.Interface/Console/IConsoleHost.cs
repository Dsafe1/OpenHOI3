using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

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

	/// <summary>The closest the camera may come, as a height.</summary>
	void SetLeastHeight(int height);

	/// <summary>A line for the log.</summary>
	void Log(string line);
}
