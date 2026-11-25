using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A run of finished battles packed for a save (<c>docs/mechanics/combatreports.md</c>, section 3): every figure exact.
/// The format's number, 1; how many reports; the countries named, each once; then the reports column by column - the
/// hours as their differences, the kind, the province, each side's losses and men, the winner, and each side's country
/// by its place among those named, nought for none - every number in as few bytes as it needs, seven bits to a byte
/// with its sign folded into its lowest bit; and the whole deflated at the smallest size.
/// </summary>
public static class CCombatReportBlock
{
	private const byte Format = 1;

	public static byte[] Pack(IReadOnlyList<CCombatReport> reports)
	{
		var tags = new List<string>();
		var places = new Dictionary<string, int>(StringComparer.Ordinal);
		int Place(CCountryTag? tag)
		{
			if (tag is not { } named)
			{
				return 0;
			}
			if (!places.TryGetValue(named.Value, out int place))
			{
				tags.Add(named.Value);
				place = tags.Count;
				places[named.Value] = place;
			}
			return place;
		}
		var attackers = new int[reports.Count];
		var defenders = new int[reports.Count];
		for (int index = 0; index < reports.Count; index++)
		{
			attackers[index] = Place(reports[index].Attacker);
			defenders[index] = Place(reports[index].Defender);
		}

		using var plain = new MemoryStream();
		plain.WriteByte(Format);
		WriteNumber(plain, reports.Count);
		WriteNumber(plain, tags.Count);
		foreach (string tag in tags)
		{
			byte[] letters = Encoding.Latin1.GetBytes(tag);
			WriteNumber(plain, letters.Length);
			plain.Write(letters, 0, letters.Length);
		}
		long last = 0;
		foreach (CCombatReport report in reports)
		{
			WriteNumber(plain, report.Hour - last);
			last = report.Hour;
		}
		Column(plain, reports, report => (long)report.Kind);
		Column(plain, reports, report => report.Province);
		Column(plain, reports, report => report.AttackerLosses);
		Column(plain, reports, report => report.AttackerMen);
		Column(plain, reports, report => report.DefenderLosses);
		Column(plain, reports, report => report.DefenderMen);
		Column(plain, reports, report => (long)report.Winner);
		foreach (int place in attackers)
		{
			WriteNumber(plain, place);
		}
		foreach (int place in defenders)
		{
			WriteNumber(plain, place);
		}

		using var packed = new MemoryStream();
		using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
		{
			plain.Position = 0;
			plain.CopyTo(deflate);
		}
		return packed.ToArray();
	}

	/// <summary>The reports of a block in their order; null where the bytes are no such block.</summary>
	public static List<CCombatReport>? Unpack(byte[] block)
	{
		try
		{
			using var plain = new MemoryStream();
			using (var deflate = new DeflateStream(new MemoryStream(block), CompressionMode.Decompress))
			{
				deflate.CopyTo(plain);
			}
			plain.Position = 0;
			if (plain.ReadByte() != Format)
			{
				return null;
			}
			int count = checked((int)ReadNumber(plain));
			int named = checked((int)ReadNumber(plain));
			if (count < 0 || named < 0 || count > plain.Length || named > plain.Length)
			{
				return null;
			}
			var tags = new CCountryTag[named];
			for (int index = 0; index < named; index++)
			{
				var letters = new byte[checked((int)ReadNumber(plain))];
				if (plain.Read(letters, 0, letters.Length) != letters.Length)
				{
					return null;
				}
				tags[index] = new CCountryTag(Encoding.Latin1.GetString(letters));
			}
			var hours = new long[count];
			long last = 0;
			for (int index = 0; index < count; index++)
			{
				last += ReadNumber(plain);
				hours[index] = last;
			}
			long[] kinds = Column(plain, count);
			long[] provinces = Column(plain, count);
			long[] attackerLosses = Column(plain, count);
			long[] attackerMen = Column(plain, count);
			long[] defenderLosses = Column(plain, count);
			long[] defenderMen = Column(plain, count);
			long[] winners = Column(plain, count);
			long[] attackers = Column(plain, count);
			long[] defenders = Column(plain, count);

			var reports = new List<CCombatReport>(count);
			for (int index = 0; index < count; index++)
			{
				if (kinds[index] < 0 || kinds[index] > (long)ECombatKind.NavalBombing || winners[index] < 0
					|| winners[index] > (long)ECombatWinner.Defender || attackers[index] < 0 || attackers[index] > named
					|| defenders[index] < 0 || defenders[index] > named)
				{
					return null;
				}
				reports.Add(new CCombatReport(hours[index], (ECombatKind)kinds[index], checked((int)provinces[index]),
					attackers[index] == 0 ? null : tags[attackers[index] - 1], defenders[index] == 0 ? null : tags[defenders[index] - 1],
					attackerLosses[index], attackerMen[index], defenderLosses[index], defenderMen[index], (ECombatWinner)winners[index]));
			}
			return reports;
		}
		catch (Exception failed) when (failed is InvalidDataException or EndOfStreamException or OverflowException)
		{
			return null;
		}
	}

	private static void Column(Stream stream, IReadOnlyList<CCombatReport> reports, Func<CCombatReport, long> figure)
	{
		foreach (CCombatReport report in reports)
		{
			WriteNumber(stream, figure(report));
		}
	}

	private static long[] Column(Stream stream, int count)
	{
		var column = new long[count];
		for (int index = 0; index < count; index++)
		{
			column[index] = ReadNumber(stream);
		}
		return column;
	}

	/// <summary>A number with its sign folded into its lowest bit, seven bits to a byte, the lowest first.</summary>
	private static void WriteNumber(Stream stream, long value)
	{
		ulong folded = (ulong)((value << 1) ^ (value >> 63));
		while (folded >= 0x80)
		{
			stream.WriteByte((byte)(folded | 0x80));
			folded >>= 7;
		}
		stream.WriteByte((byte)folded);
	}

	private static long ReadNumber(Stream stream)
	{
		ulong folded = 0;
		for (int shift = 0; shift < 64; shift += 7)
		{
			int read = stream.ReadByte();
			if (read < 0)
			{
				throw new EndOfStreamException();
			}
			folded |= (ulong)(read & 0x7F) << shift;
			if ((read & 0x80) == 0)
			{
				return (long)(folded >> 1) ^ -(long)(folded & 1);
			}
		}
		throw new InvalidDataException();
	}
}
