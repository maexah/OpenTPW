using Veldrid;

namespace OpenTPW.UI;

public class RootPanel : Entity
{
	public List<Panel> Children { get; set; } = new();
	public static RootPanel? Instance { get; set; }

	public RootPanel()
	{
		Instance ??= this;
	}

	/// <summary>
	/// Hides the whole HUD, so a screenshot shows the scene with nothing in front of it.
	/// Toggled with <see cref="InputButton.HideUI"/> (F2). Static so it survives the panel
	/// being rebuilt when a level is loaded.
	/// </summary>
	public static bool Hidden { get; set; }

	protected override void OnRender()
	{
		if ( Hidden )
			return;

		Children.ForEach( child => child.Draw() );
	}

	protected override void OnUpdate()
	{
		if ( Input.Pressed( InputButton.HideUI ) )
			Hidden = !Hidden;

		if ( Hidden )
			return;

		// Children stop updating too, so a hidden button can't be clicked by accident.
		Children.ForEach( child => child.Update() );
	}

	public T AddChild<T>( T? obj = null ) where T : Panel
	{
		if ( obj is null )
			obj = Activator.CreateInstance( typeof( T ) ) as T;

		Children.Add( obj );

		return obj;
	}
}
