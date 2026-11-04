using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// What a load left unread: every key of the save nothing here takes yet, by the block it stands in - <c>top</c>,
/// <c>province</c>, <c>country</c> - with how many times it was met. The loader's to-do list, and what it reports
/// (<c>docs/mechanics/savegame.md</c>, section 4).
/// </summary>
public sealed class CSaveReport
{
	private readonly Dictionary<(string Block, string Key), int> _unread = new();

	/// <summary>The country the save says was played.</summary>
	public string Player { get; set; } = "";

	public void Unread(string block, string key)
	{
		_unread[(block, key)] = _unread.GetValueOrDefault((block, key)) + 1;
	}

	public IReadOnlyDictionary<(string Block, string Key), int> UnreadKeys => _unread;

	/// <summary>One line a block: its unread keys, the most met first.</summary>
	public IEnumerable<string> Lines()
	{
		foreach (IGrouping<string, KeyValuePair<(string Block, string Key), int>> block in _unread.GroupBy(entry => entry.Key.Block))
		{
			yield return block.Key + ": " + string.Join(", ", block.OrderByDescending(entry => entry.Value)
				.Select(entry => entry.Value == 1 ? entry.Key.Key : $"{entry.Key.Key} x{entry.Value}"));
		}
	}
}
