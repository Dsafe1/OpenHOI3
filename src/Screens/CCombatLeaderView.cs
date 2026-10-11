using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Screens;

/// <summary>
/// The leader of one side of a battle's window: <c>leader_name</c> and
/// <c>leader_photo</c>, the side's highest ranked leader. Like the original it writes
/// both again only when he changes, and with nobody it says so and leaves the picture.
/// See <c>docs/mechanics/combatview.md</c>, section 4.
/// </summary>
public sealed class CCombatLeaderView
{
	private readonly CGuiContext _context;
	private readonly CGuiTextBox? _name;
	private readonly CGuiIcon? _photo;
	private CLeader? _shown;

	public CCombatLeaderView(CGuiWindow window, CGuiContext context)
	{
		_context = context;
		_name = window.FindTextBox("leader_name");
		_photo = window.FindIcon("leader_photo");
	}

	public void Update(CCombatSide side)
	{
		CLeader? leader = CCombatLeaders.HighestRanked(side);
		if (leader is not null && leader == _shown)
		{
			return;
		}
		_shown = leader;
		if (CCombatViewText.LeaderName(_context.Localisation.Localise, side) is { } name && _name is not null && _name.Text != name)
		{
			_name.Text = name;
		}
		if (CCombatViewText.LeaderPortrait(side) is { } path && _photo is not null && _context.Textures.Exists(path)
			&& _context.Textures.LoadFromGamePath(path) is { } picture)
		{
			_photo.SetTexture(picture);
		}
	}
}
