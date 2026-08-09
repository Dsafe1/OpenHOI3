namespace OpenHOI3.FrontEnd;

/// <summary>The tutorial chooser, one button per lesson.</summary>
public sealed class CTutorialScreen : CFrontEndView
{
	private const int LessonCount = 6;

	public override string WindowName => "tutorial_panel";

	protected override void OnAttached()
	{
		BindBack("back_button");

		for (int lesson = 1; lesson <= LessonCount; lesson++)
		{
			BindPlaceholder($"tutorial_button_{lesson}");
		}
	}
}
