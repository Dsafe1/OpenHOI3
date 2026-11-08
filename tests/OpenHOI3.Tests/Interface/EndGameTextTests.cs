using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Screens;

namespace OpenHOI3.Tests.Interface;

/// <summary>What decides the end of a game's words, as <c>docs/mechanics/endgame.md</c> has it: the result by its tiers, and the two windows' figures and choice.</summary>
public class EndGameTextTests
{
	private const int ToWin = 12;

	[Fact]
	public void TwelveMetAndMorePointsThanTheOtherTwoIsAWorldOrder()
	{
		Assert.Equal((1, 0), CEndGameText.Result(new[] { 2, 12, 3 }, new[] { 100, 700, 200 }, ToWin));
	}

	[Fact]
	public void TwelveMetWithoutThePointsIsAStrategicResult()
	{
		Assert.Equal((1, 1), CEndGameText.Result(new[] { 2, 12, 3 }, new[] { 300, 400, 200 }, ToWin));
	}

	[Fact]
	public void MoreThanTheOtherTwosPointsIsOperationalWithNoConditions()
	{
		Assert.Equal((0, 2), CEndGameText.Result(new[] { 0, 0, 0 }, new[] { 700, 100, 200 }, ToWin));
	}

	[Fact]
	public void MostPointsAndMostMetIsTactical()
	{
		Assert.Equal((2, 3), CEndGameText.Result(new[] { 1, 2, 3 }, new[] { 200, 200, 300 }, ToWin));
	}

	[Fact]
	public void TheFirstDaysPointsAreAStalemate()
	{
		Assert.Equal((-1, -1), CEndGameText.Result(new[] { 0, 0, 0 }, new[] { 263, 115, 173 }, ToWin));
	}

	[Fact]
	public void TwelveMetButTiedIsNoWorldOrder()
	{
		Assert.Equal((-1, -1), CEndGameText.Result(new[] { 12, 12, 0 }, new[] { 300, 300, 100 }, ToWin));
	}

	[Fact]
	public void ABoxMeasuredFromTheCentreEndsASingleCentredLineAtItsMiddle()
	{
		// The original's own arithmetic, half the widest line taken off as well: "0 of 15 Victory Conditions" ends at x + 160 and not around it.
		Assert.Equal(160f - 186f, CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.Center, 0, 320, 186f));
	}

	[Fact]
	public void ALineOfSeveralInACentreBoxGoesByTheWidest()
	{
		Assert.Equal(160f - 50f - 20f, CGuiLayout.TextStart(EGuiTextFormat.Centre, EGuiOrientation.Center, 0, 320, 40f, widest: 100f));
	}

	[Theory]
	[InlineData(EGuiTextFormat.Left)]
	[InlineData(EGuiTextFormat.Right)]
	public void LeftAndRightInACentreBoxStartAtTheBorder(EGuiTextFormat format)
	{
		Assert.Equal(4f, CGuiLayout.TextStart(format, EGuiOrientation.Center, 4, 320, 100f));
	}

	[Theory]
	[InlineData(15, 15, "15 / 15")]
	[InlineData(3, 15, "§R3§W / 15")]
	public void TheLobbysCountIsRedWhereItIsNotTheNumber(int ticked, int number, string expected)
	{
		Assert.Equal(expected, CVictoryWindowText.SelectInfo(ticked, number));
	}

	[Fact]
	public void AcceptSaysNothingWhereTheTicksAreTheNumber()
	{
		Assert.Equal("", CVictoryWindowText.AcceptTooltip(key => key, 15, 15));
		Assert.Equal("VC_ACCEPT_DISABLED", CVictoryWindowText.AcceptTooltip(key => key, 3, 15));
	}

	[Fact]
	public void TooManyTicksLoseSomeAndTooFewTakeOthersOfTheFaction()
	{
		string[] all = { "a", "b", "c", "d", "e" };

		Assert.Equal(new[] { "b", "c" }, CVictoryWindowText.Fit(new[] { "a", "b", "c" }, all, 2, _ => 0));
		Assert.Equal(new[] { "c", "a", "b" }, CVictoryWindowText.Fit(new[] { "c" }, all, 3, _ => 0));
	}

	[Theory]
	[InlineData("axis", "vc_bg_axis")]
	[InlineData("allies", "vc_bg_allies")]
	[InlineData("comintern", "vc_bg_com")]
	[InlineData("", null)]
	[InlineData("Axis", null)]
	public void ThePictureGoesByTheExactTag(string faction, string? expected)
	{
		Assert.Equal(expected, CVictoryWindowText.Picture(faction));
	}
}
