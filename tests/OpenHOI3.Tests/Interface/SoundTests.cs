using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Sound;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>How loud a sound effect plays, and which one is played: <c>docs/mechanics/sound.md</c>.</summary>
public class SoundTests
{
	private static readonly CCountryTag Ger = new("GER"), Pol = new("POL"), Ita = new("ITA");

	[Fact]
	public void AnAnswersSoundIsHeardByTheSideToldTheFirstKeyAlone()
	{
		var nap = new CPactMessage("WENAP", "THEYNAP", "OTHERNAP", Ger, Pol) { Sound = CPactMessage.Success };

		Assert.Equal(CPactMessage.Success, CMessages.PactSound(nap, Ger));
		Assert.Equal("", CMessages.PactSound(nap, Pol));
		Assert.Equal("", CMessages.PactSound(nap, Ita));
		Assert.Equal("", CMessages.PactSound(nap, null));
	}

	[Fact]
	public void AMessageToldToOneCountryIsHeardByItAndOneWithoutASoundBySomebodyNot()
	{
		var peace = new CPactMessage("PEACEACCEPT", "", "", Ger, Pol) { Told = Ita, Sound = CPactMessage.Success };
		Assert.Equal(CPactMessage.Success, CMessages.PactSound(peace, Ita));
		Assert.Equal("", CMessages.PactSound(peace, Ger));

		var guarantee = new CPactMessage("WEGUARAT", "GUARATOUS", "GUARATOOTHER", Ger, Pol);
		Assert.Equal("", CMessages.PactSound(guarantee, Ger));
	}

	[Theory]
	[InlineData(100f, 100f, 1f)]
	[InlineData(50f, 100f, 0.75f)]
	[InlineData(100f, 50f, 0.75f)]
	[InlineData(50f, 50f, 0.625f)]
	[InlineData(0f, 100f, 0f)]
	[InlineData(100f, 0f, 0f)]
	public void TheSettingsScaleEverySoundByHalfTheirProductAndAHalf(float master, float effects, float scale)
	{
		Assert.Equal(scale, CSoundVolume.Scale(master, effects), 5);
	}

	[Theory]
	[InlineData(100, 1f, 0f)]
	[InlineData(100, 0.75f, -25f)]
	[InlineData(100, 0.5f, -50f)]
	[InlineData(50, 1f, -50f)]
	[InlineData(150, 1f, 0f)]
	public void ASoundsVolumeIsLinearInDecibelsAndNeverAboveNought(int volume, float scale, float decibels)
	{
		Assert.Equal(decibels, CSoundVolume.Decibels(volume, scale), 3);
	}

	[Fact]
	public void TheJustAudibleSettingIsFiftyDecibelsDown()
	{
		// The floor the original leaves: a setting just above nought plays at half scale, -50 dB, and nought is silence.
		Assert.Equal(-50f, CSoundVolume.Decibels(100, CSoundVolume.Scale(1f, 1f)), 1);
	}

	[Theory]
	[InlineData("own", "sprite", "own")]
	[InlineData("", "sprite", "sprite")]
	[InlineData("", "", "click")]
	[InlineData("close_window", "", "close_window")]
	public void AButtonsSoundIsItsOwnThenItsSpritesThenClick(string own, string sprite, string chosen)
	{
		Assert.Equal(chosen, CClickSound.Choose(own, sprite));
	}

	[Fact]
	public void AUnitSoundsByItsKindAHeadquartersAsAnArmy()
	{
		var tag = new CCountryTag("GER");

		Assert.Equal("navy_selected", CSelectionSounds.Selected(new CUnit(EUnitKind.Navy, tag)));
		Assert.Equal("air_move", CSelectionSounds.Moved(new CUnit(EUnitKind.Air, tag)));
		Assert.Equal("army_selected", CSelectionSounds.Selected(new CUnit(EUnitKind.Corps, tag)));
		Assert.Equal("army_move", CSelectionSounds.Moved(new CUnit(EUnitKind.Division, tag)));
	}

	[Theory]
	[InlineData(ECombatKind.Land, 0, "land_combat_1")]
	[InlineData(ECombatKind.Land, 11, "land_combat_6")]
	[InlineData(ECombatKind.Land, -7, "land_combat_2")]
	[InlineData(ECombatKind.Naval, 5, "naval_combat_3")]
	[InlineData(ECombatKind.Air, 3, "air_combat_2")]
	[InlineData(ECombatKind.GroundBombing, 4, "bomb_2")]
	[InlineData(ECombatKind.NavalBombing, 2, "bomb_3")]
	public void ABattleSoundIsTheRollsMagnitudeModuloItsKindsCountFromOne(ECombatKind kind, int roll, string name)
	{
		Assert.Equal(name, CBattleSounds.Name(kind, roll));
	}
}
