using System;
using System.Collections.Generic;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// The message log as the original keeps it: the last 200 messages, and rows for those of the
/// categories shown - each message's line wrapped, a row for each piece, the newest at the bottom.
/// The rows are cut by one each time a message is added, and made again from the oldest message
/// when a category is shown or hidden, stopping after 101. See <c>docs/mechanics/messages.md</c>,
/// section 5.
/// </summary>
public sealed class CMessageLog
{
	public const int Kept = 200;
	public const int MostRows = 100;
	public const int MostRebuilt = 101;

	private readonly List<CMessage> _messages = new();
	private readonly List<(CMessage Message, string Text)> _rows = new();
	private readonly HashSet<EMessageCategory> _hidden = new();
	private readonly Func<string, string> _localise;
	private readonly float _width;
	private readonly Func<string, float> _measure;

	/// <param name="width">How wide a row's text may be: its button's width less 10.</param>
	/// <param name="measure">How wide a text is drawn, without its colour escapes.</param>
	public CMessageLog(Func<string, string> localise, float width, Func<string, float> measure)
	{
		_localise = localise;
		_width = width;
		_measure = measure;
	}

	public IReadOnlyList<CMessage> Messages => _messages;

	public IReadOnlyList<(CMessage Message, string Text)> Rows => _rows;

	public bool IsHidden(EMessageCategory category) => _hidden.Contains(category);

	public void Add(CMessage message)
	{
		_messages.Add(message);
		if (_messages.Count > Kept)
		{
			_messages.RemoveAt(0);
		}
		if (!_hidden.Contains(message.Type.Category))
		{
			AddRows(message);
		}
		// One row goes for each message added, however many it brought.
		if (_rows.Count > MostRows)
		{
			_rows.RemoveAt(0);
		}
	}

	/// <summary>Shows or hides a category, and makes the rows again from the oldest message kept.</summary>
	public void SetHidden(EMessageCategory category, bool hidden)
	{
		if (hidden)
		{
			_hidden.Add(category);
		}
		else
		{
			_hidden.Remove(category);
		}
		_rows.Clear();
		int shown = 0;
		foreach (CMessage message in _messages)
		{
			if (_hidden.Contains(message.Type.Category))
			{
				continue;
			}
			if (shown == MostRebuilt)
			{
				break;
			}
			AddRows(message);
			shown++;
		}
	}

	private void AddRows(CMessage message)
	{
		foreach (string paragraph in CMessageText.Log(_localise, message).Split('\n'))
		{
			foreach (string piece in CWordWrap.Wrap(paragraph, _width, _measure))
			{
				_rows.Add((message, piece));
			}
		}
	}
}
