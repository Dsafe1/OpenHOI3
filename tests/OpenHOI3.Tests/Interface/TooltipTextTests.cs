using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the tooltip box shows of a tooltip, against <c>docs/mechanics/tooltips.md</c>,
/// sections 3 and 6.
/// </summary>
public class TooltipTextTests
{
	private const string Line = "--------------";

	[Fact]
	public void TheLineIsFourteenDashes()
	{
		Assert.Equal(14, CTooltipText.Rule.Length);
		Assert.Equal(Line, CTooltipText.Rule);
	}

	[Fact]
	public void BeforeTheMouseIsStillOnlyTheTextShows()
	{
		var tooltip = new CTooltip { Text = "Close", Delayed = "More", DelayedExtra = "Shortcut: F2" };

		Assert.Equal("Close", CTooltipText.Compose(tooltip, still: false));
	}

	[Fact]
	public void OnceStillEachHalfComesUnderTheLine()
	{
		var tooltip = new CTooltip { Text = "Close", Delayed = "More", DelayedExtra = "Shortcut: F2" };

		Assert.Equal("Close\n" + Line + "\nMore\n" + Line + "\nShortcut: F2", CTooltipText.Compose(tooltip, still: true));
	}

	[Fact]
	public void TheExtraHasNoLineWhenNothingComesBeforeIt()
	{
		var tooltip = new CTooltip { DelayedExtra = "Shortcut: F2" };

		// "\n" + the extra, and the newline is trimmed away.
		Assert.Equal("Shortcut: F2", CTooltipText.Compose(tooltip, still: true));
	}

	[Fact]
	public void TheDelayedHalfAloneStartsWithItsLine()
	{
		var tooltip = new CTooltip { Delayed = "More" };

		// The line is not trimmed: only blanks are.
		Assert.Equal(Line + "\nMore", CTooltipText.Compose(tooltip, still: true));
	}

	[Fact]
	public void BlanksAreTrimmedAtBothEndsAndNowhereElse()
	{
		var tooltip = new CTooltip { Text = " \t\r\nA  b\n\n " };

		Assert.Equal("A  b", CTooltipText.Compose(tooltip, still: false));
	}

	[Fact]
	public void NothingButBlanksIsNothing()
	{
		Assert.Equal("", CTooltipText.Compose(new CTooltip { Text = " \n " }, still: true));
		Assert.Equal("", CTooltipText.Compose(CTooltip.Empty, still: true));
	}

	[Fact]
	public void AShortcutGoesInTheExtraWhereItIsEmpty()
	{
		CTooltip result = CTooltipText.WithShortcut(new CTooltip { Text = "Production" }, "F2", "§WShortcut: §Y");

		Assert.Equal("§WShortcut: §YF2", result.DelayedExtra);
		Assert.Equal("Production", result.Text);
	}

	[Fact]
	public void AShortcutDoesNotDisplaceAnExtraAlreadyThere()
	{
		var tooltip = new CTooltip { DelayedExtra = "Its own" };

		Assert.Equal("Its own", CTooltipText.WithShortcut(tooltip, "F2", "Shortcut: ").DelayedExtra);
	}

	[Fact]
	public void NoShortcutLeavesTheTooltipAlone()
	{
		var tooltip = new CTooltip { Text = "A" };

		Assert.Same(tooltip, CTooltipText.WithShortcut(tooltip, "", "Shortcut: "));
	}

	[Fact]
	public void LinesAreBrokenAtTheMostLessThreeBorders()
	{
		// core.gui's ToolTip: maxWidth 400, a border of 16.
		Assert.Equal(352, CTooltipText.WrapWidth(400, 16));
	}
}
