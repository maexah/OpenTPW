using Veldrid;

namespace OpenTPW.UI;

/// <summary>
/// The root of the HUD, and deliberately not an <see cref="Entity"/>.
///
/// The HUD has no place in world space, and it draws with no depth test at all - Material.UI
/// disables both halves of it - so nothing but the order it is drawn in keeps it in front of the
/// world. Staying out of <see cref="Entity.All"/> is what makes that order safe: a pass over the
/// world cannot reach the HUD, so adding one cannot put geometry on top of it.
///
/// It was an entity once, and the only thing holding it down was its place at the end of the
/// creation order. Adding a second world pass for see-through geometry put every flyer and every
/// palm crown on top of it. <see cref="Level"/> drives it by hand instead, after the world.
/// </summary>
public class RootPanel
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

	public void Render()
	{
		if ( Hidden )
			return;

		Children.ForEach( child => child.Draw() );
	}

	public void Update()
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
