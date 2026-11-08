using System;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.StrategicWarfare;

namespace OpenHOI3.Screens;

/// <summary>A row of the Strategic Warfare Manager's list, a <c>swm_list_entry</c>: an enemy, its colour and the dates of the war, and a box that shows its lines. See <c>docs/mechanics/strategicwarfare.md</c>, section 4.</summary>
public sealed class CWarfareEntry
{
	private readonly CGuiButton? _present;
	private readonly CGuiButton? _past;

	private CWarfareEntry(CGuiWindow window, CGuiContext context, CGameState state, CWarfareRecord record, Action<CWarfareRecord, bool> toggled)
	{
		Window = window;
		Record = record;
		_present = window.TryFindButton("background_button_present");
		_past = window.TryFindButton("background_button_past");
		Func<string, string> localise = context.Localisation.Localise;
		CCountry? enemy = state.Country(record.Enemy);
		if (_present is not null)
		{
			_present.Visible = !record.Ended;
			_present.Pressed += () => toggled(record, _present.Checked);
		}
		if (_past is not null)
		{
			_past.Visible = record.Ended;
			_past.Pressed += () => toggled(record, _past.Checked);
		}
		if (enemy is not null)
		{
			window.TryFindIcon("country_flag")?.SetCountry(enemy);
			if (window.TryFindIcon("color_icon") is { } colour)
			{
				colour.Modulate = enemy.Color.ToGodot();
			}
		}
		if (window.FindTextBox("country") is { } name)
		{
			name.Text = enemy?.Name ?? record.Enemy.Value;
		}
		if (window.FindTextBox("date_begin") is { } begin)
		{
			begin.Text = CTextTemplate.Fill(localise("SWM_RECORDS_BEGIN_DATE"), ("DATE", CWarfareText.DateOf(record.StartHour).ToString()));
		}
		if (window.FindTextBox("date_end") is { } end)
		{
			string date = record.Ended ? CWarfareText.DateOf(record.EndHour).ToString() : localise("SWM_RECORDS_TODAY");
			end.Text = CTextTemplate.Fill(localise("SWM_RECORDS_END_DATE"), ("DATE", date));
		}
	}

	public CGuiWindow Window { get; }

	public CWarfareRecord Record { get; }

	public static CWarfareEntry? Create(CGuiContext context, CGameState state, CWarfareRecord record, Action<CWarfareRecord, bool> toggled)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CWarfareText.RowName);
		return window is null ? null : new CWarfareEntry(window, context, state, record, toggled);
	}

	public void SetSelected(bool selected)
	{
		if (_present is not null)
		{
			_present.Checked = selected;
		}
		if (_past is not null)
		{
			_past.Checked = selected;
		}
	}
}
