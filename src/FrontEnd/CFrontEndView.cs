using System;
using Godot;
using OpenHOI3.Gui;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// One screen of the front end, mirroring the original's <c>CFrontEndView</c>. Each
/// subclass owns a window from the interface files and handles that window's buttons;
/// <see cref="CFrontEnd"/> only decides which is active.
///
/// The original routes presses through a nested <c>__CButtonObserverGlue</c> adapter
/// per view - there are 172 such adapters in the binary. They exist because C++ had no
/// delegates, so a class wanting button callbacks would otherwise have to inherit the
/// observer interface. C# events already provide that, so the adapters are deliberately
/// not reproduced.
/// </summary>
public abstract class CFrontEndView
{
	/// <summary>Name of the <c>windowType</c> this view drives.</summary>
	public abstract string WindowName { get; }

	/// <summary>
	/// Game-relative <c>.gui</c> file holding the window. Null means it is one of the
	/// panels declared inside the front end's own <c>country_selection_panel</c>.
	/// </summary>
	public virtual string? GuiFile => null;

	protected CFrontEnd Host { get; private set; } = null!;

	protected internal TWindow Window { get; private set; } = null!;

	internal void Attach(CFrontEnd host, TWindow window)
	{
		Host = host;
		Window = window;
		Window.Visible = false;
		OnAttached();
	}

	internal void SetActive(bool active)
	{
		Window.Visible = active;
		if (active)
		{
			OnActivated();
		}
		else
		{
			OnDeactivated();
		}
	}

	/// <summary>Called once the window exists. Bind buttons here.</summary>
	protected abstract void OnAttached();

	public virtual void OnActivated()
	{
	}

	public virtual void OnDeactivated()
	{
	}

	/// <summary>
	/// Attaches a handler to a button by name. Missing buttons warn rather than throw,
	/// because the interface files differ between expansions and a view should survive
	/// a control it does not recognise.
	/// </summary>
	protected void Bind(string buttonName, Action handler)
	{
		TButton? button = Window.FindButton(buttonName);
		if (button is null)
		{
			GD.PushWarning($"'{WindowName}' has no button '{buttonName}'");
			return;
		}
		button.Pressed += () => handler();
	}

	/// <summary>Returns to the main menu.</summary>
	protected void BindBack(string buttonName)
	{
		Bind(buttonName, Host.Show<CFrontEndState>);
	}

	protected void BindLink(string buttonName, string url)
	{
		Bind(buttonName, () => OS.ShellOpen(url));
	}

	/// <summary>Records a button that exists but has nothing behind it yet.</summary>
	protected void BindPlaceholder(string buttonName)
	{
		Bind(buttonName, () => GD.Print($"{WindowName}/{buttonName}: not implemented yet"));
	}

	/// <summary>
	/// Hides a control the interface files declare but the original does not show.
	/// The game hides such controls in code rather than omitting them from the data,
	/// so matching its appearance means doing the same.
	/// </summary>
	protected void HideElement(string name)
	{
		CGuiObject? element = Window.FindElement(name);
		if (element is null)
		{
			GD.PushWarning($"'{WindowName}' has no element '{name}' to hide");
			return;
		}
		element.Visible = false;
	}
}
