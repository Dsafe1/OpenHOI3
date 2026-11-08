using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a land brigade is ordered from on its own: <c>brigade_builder_window</c>,
/// which the production screen's <c>brig_attach</c> opens. It may be ordered as
/// reserves. See <c>docs/mechanics/builders.md</c>, section 7.
/// </summary>
public partial class CBrigadeBuilderView : CTypeBuilderView
{
	private CGuiButton? _reserves;

	protected override string WindowName => "brigade_builder_window";

	protected override EUnitBranch Branch => EUnitBranch.Land;

	protected override string EntryType => "brigade_entry_3";

	protected override string PanelName => "selected_brigade_window";

	protected override string LabelName => "selected_brigade_label";

	protected override string PhotoName => "selected_brigade_photo";

	protected override string? DescriptionName => "selected_brigade_description";

	protected override IReadOnlyDictionary<string, (string Text, string Delayed)> HeaderTooltips => CBuilderTooltips.Brigade;

	protected override string? GroupSprite => "GFX_brigade_listbutton";

	protected override bool IsReserve => _reserves?.Checked ?? false;

	protected override void Build(CGuiWindow window)
	{
		base.Build(window);
		_reserves = window.FindButton("as_reserves");
	}
}
