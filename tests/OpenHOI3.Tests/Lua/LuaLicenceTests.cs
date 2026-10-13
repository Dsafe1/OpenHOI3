using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the licence script is handed, against <c>docs/mechanics/aid.md</c>, section 4, on
/// <see cref="ScriptWorld"/>.
/// </summary>
public sealed class LuaLicenceTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-lualicence-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaLicenceTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string autoexec = "")
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), autoexec);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	[Fact]
	public void A_script_made_licence_holds_its_runs_and_money_and_no_type_until_one_is_set()
	{
		CGameState state = ScriptWorld.State();
		using CAiScripts scripts = Load();
		const string licence = "(function() local a = CLicenceTechnologyAction(CCountryDataBase.GetTag('GER'), CCountryDataBase.GetTag('POL')) "
			+ "a:SetSerial(3) a:SetParallel(2) a:SetMoney(CFixedPoint(1.5)) return a end)()";
		Assert.Equal(3.0, scripts.Ask(state, licence + ":GetSerial()"));
		Assert.Equal(2.0, scripts.Ask(state, licence + ":GetParalell()"));
		Assert.Equal(1.5, scripts.Ask(state, licence + ":GetMoney():Get()"));
		Assert.Null(scripts.Ask(state, licence + ":GetSubunit()"));
	}

	[Fact]
	public void The_seller_answers_handed_the_licence_itself_and_itself_as_the_observer()
	{
		CGameState state = ScriptWorld.State();
		using CAiScripts scripts = Load(
			"function DiploScore_LicenceTechnology(ai, actor, recipient, observer, command) "
			+ "if observer ~= recipient then return -1 end "
			+ "return command:GetSerial() * 10 + command:GetParalell() end");
		var offer = new COffer(EOfferKind.Licence, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, true, 0)
		{
			Licence = new CLicenceTerms("infantry_brigade", 4, 2, CFixedPoint.FromInt(1)),
		};
		Assert.Equal(42, scripts.LicenceScore(state, offer));
	}
}
