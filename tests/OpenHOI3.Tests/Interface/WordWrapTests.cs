using System.Linq;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// How a line too wide is broken: at its spaces, a colour carried over. A letter is one wide.
/// </summary>
public class WordWrapTests
{
	private static float Measure(string text) => text.Length;

	[Fact]
	public void ALineThatFitsIsLeftWhole()
	{
		Assert.Equal(new[] { "one two" }, CWordWrap.Wrap("one two", 7, Measure).ToArray());
	}

	[Fact]
	public void ALineTooWideBreaksAtItsSpaces()
	{
		Assert.Equal(new[] { "one two", "three" }, CWordWrap.Wrap("one two three", 8, Measure).ToArray());
	}

	[Fact]
	public void AWordWiderThanTheWidthIsALineOfItsOwn()
	{
		Assert.Equal(new[] { "a", "enormous", "b" }, CWordWrap.Wrap("a enormous b", 4, Measure).ToArray());
	}

	[Fact]
	public void EscapesAreNotMeasured()
	{
		Assert.Equal(new[] { "§Rone two" }, CWordWrap.Wrap("§Rone two", 7, Measure).ToArray());
	}

	[Fact]
	public void AColourOnAtTheBreakIsOnAgainOnTheNextLine()
	{
		Assert.Equal(new[] { "§Rred red", "§Rred" }, CWordWrap.Wrap("§Rred red red", 7, Measure).ToArray());
	}
}
