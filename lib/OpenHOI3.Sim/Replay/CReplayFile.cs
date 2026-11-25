using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A recording on disk, compressed: a line naming the format, a line of what the game has to be made with - the countries
/// played and the seed - and then a line of JSON for each entry in the order it happened: one of the AI's steps (<c>s</c>)
/// with its hour, name, draws, unit numbers and commands; a player's command (<c>p</c>) with the drains before it; an hour
/// begun (<c>t</c>) with the drains before it. Written as the game runs, so a run that stops leaves what it got to; read an
/// entry at a time as a replay reaches it, so that what a command names exists by then. Ours; see <c>docs/replay.md</c>.
/// </summary>
public sealed class CReplayFile : IDisposable
{
	/// <summary>Written first, so that a file of something else, or of another version, is not taken for a recording.</summary>
	private const string Format = "OpenHOI3 replay 3";

	/// <summary>The format before a save's mark and a recording's name, which reads the same.</summary>
	private const string OlderFormat = "OpenHOI3 replay 2";

	private readonly StreamWriter? _writer;
	private readonly StreamReader? _reader;
	private readonly CGameState? _state;

	/// <summary>What the recording says its game is made with.</summary>
	public CReplayHeader Header { get; }

	private CReplayFile(StreamWriter writer, CReplayHeader header)
	{
		_writer = writer;
		Header = header;
		_writer.WriteLine(Format);
		_writer.WriteLine(HeaderLine(header));
		// The head is on disk at once: a game played on from a save reads its parent's while that may still be written.
		Flush();
	}

	private CReplayFile(StreamReader reader, CGameState state)
	{
		_reader = reader;
		_state = state;
		CReplayHeader header = ReadHeader(reader);
		Header = header;
		if (header.Seed != state.Seed)
		{
			throw new InvalidDataException($"the recording was made with seed {header.Seed} and the game has {state.Seed}");
		}
		var played = new HashSet<CCountryTag>(header.Played);
		if (!played.SetEquals(state.Played))
		{
			throw new InvalidDataException(
				$"the recording was made with {Who(header.Played)} playing and the game has {Who(state.Played)}");
		}
	}

	/// <summary>A new recording at the path, replacing any there, of a game made as the header says.</summary>
	public static CReplayFile Create(string path, CReplayHeader header)
	{
		var stream = new GZipStream(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), CompressionLevel.Fastest);
		return new CReplayFile(new StreamWriter(stream, new UTF8Encoding(false)), header);
	}

	/// <summary>A recording's file opened to be read, one that is still being written among them.</summary>
	private static StreamReader Reader(string path)
	{
		return new StreamReader(new GZipStream(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
			CompressionMode.Decompress), Encoding.UTF8);
	}

	/// <summary>What a recording's game has to be made with: to be read before the game is made.</summary>
	public static CReplayHeader ReadHeader(string path)
	{
		using StreamReader reader = Reader(path);
		return ReadHeader(reader);
	}

	/// <summary>A recording to play back into this game, which has to have been made as its header says.</summary>
	public static CReplayFile Open(string path, CGameState state)
	{
		StreamReader reader = Reader(path);
		try
		{
			return new CReplayFile(reader, state);
		}
		catch
		{
			// A recording that does not fit the game is let go of before that is said.
			reader.Dispose();
			throw;
		}
	}

	public void Write(CReplayEntry entry)
	{
		_writer!.WriteLine(ToLine(entry));
		// A save's mark is on disk before the save is: the save names it, and whoever loads the save reads up to it.
		if (entry is CReplaySaved)
		{
			Flush();
		}
	}

	/// <summary>Everything written so far put on disk, readable by another reader of the file.</summary>
	public void Flush()
	{
		_writer!.Flush();
		_writer.BaseStream.Flush();
	}

	/// <summary>
	/// The next entry, or null at the end of the recording - also where the file breaks off, as one does that was being
	/// written when the game stopped.
	/// </summary>
	public CReplayEntry? Read()
	{
		string? line;
		try
		{
			line = _reader!.ReadLine();
		}
		catch (InvalidDataException)
		{
			return null;
		}
		return line is null ? null : FromLine(line, _state!);
	}

	/// <summary>
	/// An entry through the file's form and back, read against the game given: what a playback from disk gets. For a check
	/// that every command survives the journey.
	/// </summary>
	public static CReplayEntry Through(CReplayEntry entry, CGameState state)
	{
		return FromLine(ToLine(entry), state);
	}

	private static CReplayHeader ReadHeader(StreamReader reader)
	{
		if (reader.ReadLine() is not (Format or OlderFormat))
		{
			throw new InvalidDataException("not a recording of this version");
		}
		using JsonDocument document = JsonDocument.Parse(reader.ReadLine() ?? "{}");
		JsonElement root = document.RootElement;
		var played = root.GetProperty("played").EnumerateArray().Select(tag => new CCountryTag(tag.GetString()!)).ToList();
		return new CReplayHeader(played, root.GetProperty("seed").GetUInt32(),
			root.TryGetProperty("segment", out JsonElement segment) ? segment.GetString() : null,
			root.TryGetProperty("parent", out JsonElement parent) ? CReplayLink.Parse(parent.GetString()) : null,
			root.TryGetProperty("made", out JsonElement made) ? made.GetString() ?? "" : "",
			root.TryGetProperty("options", out JsonElement options)
				? options.EnumerateArray().Select(option => option.GetInt32()).ToList() : null);
	}

	private static string HeaderLine(CReplayHeader header)
	{
		return Json(json =>
		{
			json.WriteStartObject();
			json.WriteStartArray("played");
			foreach (CCountryTag tag in header.Played)
			{
				json.WriteStringValue(tag.Value);
			}
			json.WriteEndArray();
			json.WriteNumber("seed", header.Seed);
			if (header.Segment is not null)
			{
				json.WriteString("segment", header.Segment);
			}
			if (header.Parent is not null)
			{
				json.WriteString("parent", header.Parent.Text);
			}
			if (header.Made.Length > 0)
			{
				json.WriteString("made", header.Made);
			}
			if (header.Options is not null)
			{
				json.WriteStartArray("options");
				foreach (int option in header.Options)
				{
					json.WriteNumberValue(option);
				}
				json.WriteEndArray();
			}
			json.WriteEndObject();
		});
	}

	/// <summary>An entry as the one line it is in a recording: also what goes from one machine to another (<c>docs/multiplayer.md</c>).</summary>
	public static string ToLine(CReplayEntry entry)
	{
		return Json(json =>
		{
			json.WriteStartObject();
			switch (entry)
			{
				case CReplayStep step:
					json.WriteString("k", "s");
					json.WriteNumber("h", step.Hour);
					json.WriteString("s", step.Step);
					json.WriteNumber("d", step.Draws);
					json.WriteNumber("u", step.UnitIds);
					if (step.AiDraws != 0)
					{
						json.WriteNumber("a", step.AiDraws);
					}
					// Written only where the step's asking made one: `m` the table of who stands where, `n` the network.
					if (step.MadePositions)
					{
						json.WriteBoolean("m", true);
					}
					if (step.MadeSupplyNetwork)
					{
						json.WriteBoolean("n", true);
					}
					json.WriteStartArray("c");
					foreach (CCommand command in step.Commands)
					{
						CCommandCodec.Write(json, command);
					}
					json.WriteEndArray();
					// `x`: what the step did to the game at once, written only where it did anything.
					if (step.Done.Count > 0)
					{
						json.WriteStartArray("x");
						foreach (CCommand command in step.Done)
						{
							CCommandCodec.Write(json, command);
						}
						json.WriteEndArray();
					}
					break;
				case CReplayPost post:
					json.WriteString("k", "p");
					json.WriteNumber("t", post.Turn);
					json.WritePropertyName("c");
					CCommandCodec.Write(json, post.Command);
					break;
				case CReplayTick tick:
					json.WriteString("k", "t");
					json.WriteNumber("t", tick.Turn);
					break;
				case CReplayDrain drain:
					json.WriteString("k", "q");
					json.WriteNumber("t", drain.Turn);
					break;
				case CReplaySaved saved:
					json.WriteString("k", "v");
					json.WriteNumber("m", saved.Mark);
					json.WriteNumber("t", saved.Turn);
					break;
				default:
					throw new NotSupportedException($"a recording has no form for {entry.GetType().Name}");
			}
			json.WriteEndObject();
		});
	}

	/// <summary>A line of a recording read against a game: its commands name that game's own countries and units.</summary>
	public static CReplayEntry FromLine(string line, CGameState state)
	{
		using JsonDocument document = JsonDocument.Parse(line);
		JsonElement root = document.RootElement;
		switch (root.GetProperty("k").GetString())
		{
			case "s":
				var commands = new List<CCommand>();
				foreach (JsonElement command in root.GetProperty("c").EnumerateArray())
				{
					commands.Add(CCommandCodec.Read(command, state));
				}
				List<CCommand>? done = null;
				if (root.TryGetProperty("x", out JsonElement atOnce))
				{
					done = new List<CCommand>();
					foreach (JsonElement command in atOnce.EnumerateArray())
					{
						done.Add(CCommandCodec.Read(command, state));
					}
				}
				return new CReplayStep(root.GetProperty("h").GetInt64(), root.GetProperty("s").GetString()!, commands,
					root.GetProperty("d").GetInt64(), root.GetProperty("u").GetInt32(),
					root.TryGetProperty("a", out JsonElement ai) ? ai.GetInt64() : 0,
					root.TryGetProperty("m", out JsonElement positions) && positions.GetBoolean(),
					root.TryGetProperty("n", out JsonElement network) && network.GetBoolean(), done);
			case "p":
				return new CReplayPost(root.GetProperty("t").GetInt32(), CCommandCodec.Read(root.GetProperty("c"), state));
			case "t":
				return new CReplayTick(root.GetProperty("t").GetInt32());
			case "q":
				return new CReplayDrain(root.GetProperty("t").GetInt32());
			case "v":
				return new CReplaySaved(root.GetProperty("m").GetInt32(), root.GetProperty("t").GetInt32());
			default:
				throw new InvalidDataException($"a recording's line of an unknown kind: {line[..Math.Min(line.Length, 60)]}");
		}
	}

	private static string Json(Action<Utf8JsonWriter> write)
	{
		using var buffer = new MemoryStream();
		using (var json = new Utf8JsonWriter(buffer))
		{
			write(json);
		}
		return Encoding.UTF8.GetString(buffer.ToArray());
	}

	private static string Who(IEnumerable<CCountryTag> played)
	{
		string[] tags = played.Select(tag => tag.Value).OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
		return tags.Length == 0 ? "nobody" : string.Join(", ", tags);
	}

	public void Dispose()
	{
		_writer?.Dispose();
		_reader?.Dispose();
	}
}
