using System.Linq;
using OpenHOI3.Interface.Chat;
using Xunit;

namespace OpenHOI3.Tests.Interface;

/// <summary>The chat in play as one machine holds it, against <c>docs/mechanics/chat.md</c>.</summary>
public class ChatTests
{
	private static readonly string[] Users = { "Host", "Anna Maria", "Anna" };

	private static CChatLine Line(int channel, string name = "Anna", string text = "hello",
		EChatLineKind kind = EChatLineKind.Line) => new(kind, channel, name, text, "12:34");

	[Fact]
	public void AFactionsTagPutsItsPlayersInOneOfThreeChannelsAndEveryOtherInNeutral()
	{
		Assert.Equal(CChatChannels.Axis, CChatChannels.OfFaction("axis"));
		Assert.Equal(CChatChannels.Allies, CChatChannels.OfFaction("allies"));
		Assert.Equal(CChatChannels.Comintern, CChatChannels.OfFaction("comintern"));
		Assert.Equal(CChatChannels.Neutral, CChatChannels.OfFaction(""));
		Assert.Equal(CChatChannels.Neutral, CChatChannels.OfFaction(null));
		// A mod's own faction shares it.
		Assert.Equal(CChatChannels.Neutral, CChatChannels.OfFaction("pact_of_steel"));
		Assert.Equal(new[] { "CHAT_CHANNEL_GENERAL", "CHAT_AXIS", "CHAT_ALLIES", "CHAT_COMINTERN", "CHAT_NEUTRAL" }, CChatChannels.Keys);
	}

	[Fact]
	public void ALineIsUnreadUntilItsChannelIsTheOneShownInAnOpenWindow()
	{
		var log = new CChatLog();
		log.SetFactionChannel(CChatChannels.Axis);
		Assert.Equal(new[] { CChatChannels.General, CChatChannels.Axis }, log.Joined);

		// Closed, a line is unread, and the inbox button says so.
		log.Add(Line(CChatChannels.General));
		Assert.True(log.AnyUnread);
		log.Open();
		Assert.False(log.AnyUnread);

		// Open on General, a line there is read at once and one in the faction's channel is not, until it is chosen.
		log.Add(Line(CChatChannels.General));
		Assert.False(log.Unread(CChatChannels.General));
		log.Add(Line(CChatChannels.Axis));
		Assert.True(log.Unread(CChatChannels.Axis));
		log.Next(back: false);
		Assert.Equal(CChatChannels.Axis, log.Current);
		Assert.False(log.AnyUnread);
		// Round again, and back the other way.
		log.Next(back: false);
		Assert.Equal(CChatChannels.General, log.Current);
		log.Next(back: true);
		Assert.Equal(CChatChannels.Axis, log.Current);

		// A channel the player is not in cannot be chosen; a faction left puts him back on General.
		log.Select(CChatChannels.Comintern);
		Assert.Equal(CChatChannels.Axis, log.Current);
		log.SetFactionChannel(CChatChannels.Neutral);
		Assert.Equal(CChatChannels.General, log.Current);
		Assert.Equal(new[] { CChatChannels.General, CChatChannels.Neutral }, log.Joined);
	}

	[Fact]
	public void AWhisperIsShownInTheCurrentChannelAndRemembersWhoToReplyTo()
	{
		var log = new CChatLog();
		log.Open();
		Assert.Null(log.LastWhisperer);
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_REPLY_MISSINGUSER"), log.Read("/reply hi", Users));

		log.Add(Line(0, "Anna", "psst", EChatLineKind.WhisperIn));
		Assert.Equal("Anna", log.LastWhisperer);
		Assert.Single(log.Lines(CChatChannels.General));
		Assert.Equal((EChatTyped.Whisper, "Anna", "hi"), log.Read("/reply hi", Users));
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_REPLY_MISSINGARG"), log.Read("/reply", Users));
		// Gone from the game, there is nobody to reply to.
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_REPLY_MISSINGUSER"), log.Read("/reply hi", new[] { "Host" }));
	}

	[Fact]
	public void ATypedLineIsTrimmedAndASlashBeginsACommand()
	{
		var log = new CChatLog();
		Assert.Equal((EChatTyped.Nothing, "", ""), log.Read("   ", Users));
		Assert.Equal((EChatTyped.Line, "", "hello there"), log.Read("  hello there ", Users));

		// The name is the longest player's name the rest begins with, whatever its case, and may hold a space.
		Assert.Equal((EChatTyped.Whisper, "Anna Maria", "come north"), log.Read("/whisper anna maria come north", Users));
		Assert.Equal((EChatTyped.Whisper, "Anna", "Mariana is here"), log.Read("/whisper Anna Mariana is here", Users));
		Assert.Equal((EChatTyped.Error, "Bert", "CHAT_ERROR_UNKNOWNUSER"), log.Read("/whisper Bert hi", Users));
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_WHISPER_MISSINGARG"), log.Read("/whisper", Users));
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_WHISPER_MISSINGARG"), log.Read("/whisper Host", Users));
		// The other commands are not built and answer as one nobody knows.
		Assert.Equal((EChatTyped.Error, "", "CHAT_ERROR_UNKNOWNSLASH"), log.Read("/roll 6", Users));
	}

	[Fact]
	public void UpAndDownGoThroughTheLinesSent()
	{
		var log = new CChatLog();
		Assert.Null(log.Recall(up: true));
		log.Read("first", Users);
		log.Read("second", Users);
		Assert.Equal("second", log.Recall(up: true));
		Assert.Equal("first", log.Recall(up: true));
		Assert.Null(log.Recall(up: true));
		Assert.Equal("second", log.Recall(up: false));
		// Past the last, the empty box.
		Assert.Equal("", log.Recall(up: false));
		Assert.Null(log.Recall(up: false));
	}

	[Fact]
	public void AChannelKeepsItsLastLinesAndARowSaysClockNameAndText()
	{
		var log = new CChatLog();
		for (int i = 0; i < CChatLog.MostLines + 5; i++)
		{
			log.Add(Line(CChatChannels.General, text: i.ToString()));
		}
		Assert.Equal(CChatLog.MostLines, log.Lines(CChatChannels.General).Count);
		Assert.Equal("5", log.Lines(CChatChannels.General)[0].Text);

		static string Localise(string key) => key switch
		{
			"CHAT_SYSTEM" => "System",
			"CHAT_WHISPER_TO" => "To $NAME$",
			"CHAT_USERJOINEDCHANNEL" => "$NAME$ joined channel",
			_ => key,
		};
		Assert.Equal("[12:34] §YAnna§!: hello", CChatText.Row(Localise, Line(0)));
		Assert.Equal("[12:34] §YAnna*§!: hello", CChatText.Row(Localise, Line(0, kind: EChatLineKind.WhisperIn)));
		Assert.Equal("[12:34] §YTo Anna§!: hello", CChatText.Row(Localise, Line(0, kind: EChatLineKind.WhisperOut)));
		Assert.Equal("[12:34] §YSystem§!: Anna joined channel",
			CChatText.Row(Localise, Line(0, text: "CHAT_USERJOINEDCHANNEL", kind: EChatLineKind.System)));
		Assert.Equal(5, CChatChannels.Keys.Select(Localise).Count());
	}
}
