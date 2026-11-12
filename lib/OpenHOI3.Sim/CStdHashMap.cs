using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// A map from whole numbers in the order the original's runtime walks its <c>stdext::hash_map&lt;int, ...&gt;</c>, where a
/// tie goes to whichever entry that walk meets first. The key is scrambled as the runtime's <c>hash_compare</c> does - xor
/// with 0xDEADBEEF, then <c>ldiv</c> by 127773 and the remainder made 16807 x rem - 2836 x quot, wrapped into the
/// positive - and put in one of eight buckets by its low three bits. A key whose bucket holds nothing yet goes to the end
/// of the walk; any other joins its bucket's run in ascending order. Read off the running game's sealift vote, four maps
/// of up to nine keys (<c>reversing/findings/FINDINGS-liveread.md</c>, section 62). **Past 32 entries the original's map
/// grows its buckets and reorders, which is not built**: no map it is used for comes near that.
/// </summary>
public sealed class CStdHashMap
{
	private const int Buckets = 8;

	private readonly List<(int Key, long Value, int Bucket)> _entries = new();

	/// <summary>The entries in the order the original walks them.</summary>
	public IEnumerable<(int Key, long Value)> Walk()
	{
		foreach ((int key, long value, int _) in _entries)
		{
			yield return (key, value);
		}
	}

	public int Count => _entries.Count;

	/// <summary>Adds to the entry of the key, making it with nought where there is none.</summary>
	public void Add(int key, long amount)
	{
		int bucket = BucketOf(key);
		int last = -1;
		for (int i = 0; i < _entries.Count; i++)
		{
			if (_entries[i].Key == key)
			{
				_entries[i] = (key, _entries[i].Value + amount, bucket);
				return;
			}
			if (_entries[i].Bucket == bucket)
			{
				last = i;
			}
		}
		if (last < 0)
		{
			_entries.Add((key, amount, bucket));
			return;
		}
		// Into the bucket's run, before its first larger key.
		int at = last + 1;
		for (int i = 0; i <= last; i++)
		{
			if (_entries[i].Bucket == bucket && _entries[i].Key > key)
			{
				at = i;
				break;
			}
		}
		_entries.Insert(at, (key, amount, bucket));
	}

	/// <summary>The bucket of eight the runtime's hash puts the key in.</summary>
	public static int BucketOf(int key)
	{
		int h = (int)((uint)key ^ 0xDEADBEEF);
		int quot = h / 127773;
		int rem = h - quot * 127773;
		long r = 16807L * rem - 2836L * quot;
		if (r < 0)
		{
			r += 2147483647;
		}
		return (int)(r & (Buckets - 1));
	}
}
