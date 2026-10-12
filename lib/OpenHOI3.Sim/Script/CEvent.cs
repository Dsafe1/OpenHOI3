using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// A <c>country_event</c> or a <c>province_event</c> as read: <c>docs/mechanics/script.md</c>,
/// section 3. When it fires is not built yet.
/// </summary>
public sealed class CEvent
{
	public CEvent(bool isProvinceEvent, string file, int line)
	{
		IsProvinceEvent = isProvinceEvent;
		File = file;
		Line = line;
	}

	/// <summary>A <c>province_event</c>, which never fires by its mean time to happen.</summary>
	public bool IsProvinceEvent { get; }

	/// <summary>The file it was read from, relative to the install, and the line it starts on.</summary>
	public string File { get; }

	public int Line { get; }

	public int Id { get; set; }

	public string Title { get; set; } = "";

	public string Subtitle { get; set; } = "";

	public string Description { get; set; } = "";

	public string Picture { get; set; } = "";

	public bool IsTriggeredOnly { get; set; }

	public bool Major { get; set; }

	public bool FireOnlyOnce { get; set; }

	/// <summary>An <c>and</c>; every <c>trigger</c> block of the event goes into it.</summary>
	public CTrigger Trigger { get; } = CTrigger.All();

	public CMeanTimeToHappen MeanTimeToHappen { get; } = new();

	public CEventOption Immediate { get; } = new();

	public List<CEventOption> Options { get; } = new();
}
