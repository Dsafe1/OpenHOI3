using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The rules a game carries, against <c>docs/enhanced.md</c>, <i>The shape</i>: the faithful game has no fix, an
/// enhanced one every fix but those it goes without, the one line both are written as, and the state's hash telling
/// two games of different rules apart.
/// </summary>
public class RulesTests
{
	private static CGameState State()
	{
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", false) }, Array.Empty<CCountry>());
	}

	[Fact]
	public void AGameIsTheFaithfulOneUntilItIsMadeAnEnhancedOne()
	{
		CGameState state = State();
		Assert.False(state.Rules.Enhanced);
		Assert.False(state.Rules.Has(EFix.MinisterTechDecay));
		Assert.Empty(state.Rules.Fixes);
		Assert.Equal("", state.Rules.Text);
	}

	[Fact]
	public void AnEnhancedGameHasEveryFixButThoseItGoesWithout()
	{
		var all = new CRules(Array.Empty<EFix>());
		Assert.True(all.Enhanced);
		Assert.True(all.Has(EFix.MinisterTechDecay));
		Assert.Equal("enhanced", all.Text);

		var without = new CRules(new[] { EFix.MinisterTechDecay });
		Assert.True(without.Enhanced);
		Assert.False(without.Has(EFix.MinisterTechDecay));
		Assert.Equal("enhanced without minister_tech_decay", without.Text);
	}

	[Fact]
	public void TheLineIsReadBackAsTheRulesItWasWrittenFrom()
	{
		foreach (CRules rules in new[] { new CRules(), new CRules(Array.Empty<EFix>()), new CRules(new[] { EFix.MinisterTechDecay }) })
		{
			CRules? read = CRules.Parse(rules.Text);
			Assert.NotNull(read);
			Assert.Equal(rules.Text, read!.Text);
		}
	}

	[Fact]
	public void AFixOfALaterEngineIsHandedBackAndLeftOutAndAnythingElseIsNoRules()
	{
		var unknown = new List<string>();
		CRules? read = CRules.Parse("enhanced without minister_tech_decay some_later_fix", unknown.Add);
		Assert.Equal("enhanced without minister_tech_decay", read!.Text);
		Assert.Equal(new[] { "some_later_fix" }, unknown);

		Assert.Null(CRules.Parse("faithful"));
		Assert.Null(CRules.Parse("enhanced minister_tech_decay"));
	}

	[Fact]
	public void EveryFixHasANameOfItsOwnThatFindsIt()
	{
		EFix[] fixes = Enum.GetValues<EFix>();
		Assert.Equal(fixes.Length, CFixes.All.Count());
		Assert.Equal(fixes.Length, fixes.Select(CFixes.Name).Distinct().Count());
		foreach (EFix fix in fixes)
		{
			Assert.Equal(fix, CFixes.Named(CFixes.Name(fix)));
		}
		Assert.Null(CFixes.Named("Minister_Tech_Decay"));
	}

	[Fact]
	public void TheHashTellsTwoGamesOfDifferentRulesApartInItsLastPart()
	{
		CGameState faithful = State();
		CGameState enhanced = State();
		enhanced.Rules.CopyFrom(new CRules(Array.Empty<EFix>()));
		CGameState without = State();
		without.Rules.CopyFrom(new CRules(new[] { EFix.MinisterTechDecay }));

		int rules = Array.IndexOf(CStateHash.Parts, "rules");
		Assert.Equal(CStateHash.Parts.Length - 1, rules);
		ulong[] a = CStateHash.Of(faithful);
		ulong[] b = CStateHash.Of(enhanced);
		ulong[] c = CStateHash.Of(without);
		Assert.Equal(CStateHash.Parts.Length, a.Length);
		Assert.NotEqual(a[rules], b[rules]);
		Assert.NotEqual(b[rules], c[rules]);
		Assert.Equal(a.Take(rules), b.Take(rules));
		Assert.Equal(a[rules], CStateHash.Of(State())[rules]);
	}
}
