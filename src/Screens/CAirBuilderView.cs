using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a wing is ordered from: <c>air_builder_window</c>. A wing is built with
/// the equipment its country has; there is nothing to choose about one but its type.
/// See <c>docs/mechanics/builders.md</c>, section 7.
/// </summary>
public partial class CAirBuilderView : CTypeBuilderView
{
	protected override string WindowName => "air_builder_window";

	protected override EUnitBranch Branch => EUnitBranch.Air;

	protected override string EntryType => "build_air_entry";

	protected override string PanelName => "selected_air_window";

	protected override string LabelName => "selected_plane_label";

	protected override IReadOnlyDictionary<string, (string Text, string Delayed)> HeaderTooltips => CBuilderTooltips.Air;
}
