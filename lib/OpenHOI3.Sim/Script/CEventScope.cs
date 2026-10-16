using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// What a condition is asked of: a country, a province, where the question came from, and the
/// scope it started in. A block that changes the scope makes a copy of it; FROM, the root and the
/// look-ahead go into every copy unchanged. See <c>docs/mechanics/triggers.md</c>, section 1.
/// </summary>
public sealed class CEventScope
{
	private CEventScope(CCountryTag? country, int province, CCountryTag? fromCountry, int fromProvince, CEventScope? root)
	{
		Country = Known(country);
		Province = province;
		FromCountry = Known(fromCountry);
		FromProvince = fromProvince;
		Root = root ?? this;
	}

	/// <summary>A scope a question starts in, which is its own root.</summary>
	public static CEventScope Start(CCountryTag? country, int province = 0, CCountryTag? fromCountry = null,
		int fromProvince = 0, uint randomState = 0, int lookAheadDays = 0)
	{
		return new CEventScope(country, province, fromCountry, fromProvince, null)
		{
			RandomState = randomState,
			Seed = randomState,
			LookAheadDays = lookAheadDays,
		};
	}

	/// <summary>
	/// The state of chance the scope was made with. An event's answer and a decision set the
	/// state back to it before they run (<c>docs/mechanics/events.md</c>, sections 7 and 9).
	/// </summary>
	public uint Seed { get; private init; }

	/// <summary>
	/// A copy standing on its own, its own root: what a command keeps of the scope an event fired in
	/// or a decision is taken in, as the original copies one into its command.
	/// </summary>
	public CEventScope Detached()
	{
		return new CEventScope(Country, Province, FromCountry, FromProvince, null)
		{
			RandomState = RandomState,
			Seed = Seed,
			LookAheadDays = LookAheadDays,
			RebelFaction = RebelFaction,
		};
	}

	/// <summary>Sets the state of chance back to the seed.</summary>
	public void ResetToSeed()
	{
		RandomState = Seed;
	}

	/// <summary>The country, or null for none - the original's <c>---</c>.</summary>
	public CCountryTag? Country { get; }

	/// <summary>The province, or 0 for none.</summary>
	public int Province { get; }

	/// <summary>FROM's country, or null.</summary>
	public CCountryTag? FromCountry { get; }

	/// <summary>FROM's province, or 0.</summary>
	public int FromProvince { get; }

	/// <summary>The scope the question started in: THIS.</summary>
	public CEventScope Root { get; }

	/// <summary>
	/// The rebel faction a rebels' siege is asked with, or none: what <c>REB</c> means to <c>is_core</c>, <c>add_core</c>,
	/// <c>remove_core</c> and <c>secede_province</c> (<c>rebels.md</c>, section 10). Every copy keeps it.
	/// </summary>
	public Revolts.CRebelFaction? RebelFaction { get; private init; }

	/// <summary>A scope a rebels' question starts in: a province, no country, the faction and a state of chance.</summary>
	public static CEventScope ForRebels(Revolts.CRebelFaction? faction, int province, uint randomState)
	{
		return new CEventScope(null, province, null, 0, null)
		{
			RandomState = randomState,
			Seed = randomState,
			RebelFaction = faction,
		};
	}

	/// <summary>How many days ahead <c>date</c> looks.</summary>
	public int LookAheadDays { get; private init; }

	/// <summary>
	/// The state of chance <c>ally</c> and <c>local_enemy</c> draw on. A draw advances the state of
	/// the scope it is asked in; a copy starts where this one had got to.
	/// </summary>
	public uint RandomState { get; set; }

	/// <summary>A copy with another country and province, and everything else kept.</summary>
	public CEventScope With(CCountryTag? country, int province)
	{
		return new CEventScope(country, province, FromCountry, FromProvince, Root)
		{
			RandomState = RandomState,
			Seed = Seed,
			LookAheadDays = LookAheadDays,
			RebelFaction = RebelFaction,
		};
	}

	/// <summary>
	/// A number below <paramref name="count"/>, drawn as the original draws for <c>ally</c>,
	/// <c>local_enemy</c> and the <c>random_*</c> effects: two steps of MSVC's <c>rand</c>, the
	/// product of their two fifteen-bit outputs, and what that leaves over the count.
	/// </summary>
	public int Draw(int count)
	{
		return NextProduct() % count;
	}

	/// <summary>
	/// The original's <c>EventRandom_NextProduct</c>: two steps of the state, the product of their two fifteen-bit
	/// outputs. What <see cref="Draw"/> takes the remainder of, and what <c>country_event</c> seeds its event's scope with.
	/// </summary>
	public int NextProduct()
	{
		uint first = Step();
		uint second = Step();
		return (int)(((first >> 16) & 0x7FFF) * ((second >> 16) & 0x7FFF));
	}

	/// <summary>A tag that names a country, or null for none.</summary>
	public static CCountryTag? Known(CCountryTag? tag)
	{
		return tag is { IsValid: true } known && known.Value != "---" ? known : null;
	}

	private uint Step()
	{
		RandomState = unchecked(RandomState * 214013u + 2531011u);
		return RandomState;
	}

	public override string ToString()
	{
		return $"{Country?.Value ?? "---"} {Province}";
	}
}
