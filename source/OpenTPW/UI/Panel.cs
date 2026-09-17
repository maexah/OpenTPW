using System.Numerics;
using NeoVeldrid;

namespace OpenTPW.UI;

public class Panel
{
	public Vector2 position;
	public Vector2 size;

	public Panel()
	{
		position = new();
		size = new( 1.0f, 1.0f );
	}

	private Vector2 SizeToNDC( Vector2 size )
	{
		var p = size / new Vector2( Screen.Size.X, Screen.Size.Y );
		return p;
	}

	private Vector2 PositionToNDC( Vector2 position )
	{
		var p = position / new Vector2( Screen.Size.X, Screen.Size.Y );
		p += new Vector2( -0.5f, -0.5f );
		p *= 2.0f;
		return p;
	}

	public void Update()
	{
		OnUpdate();
	}

	protected virtual void OnRender() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	/// <summary>
	/// Draws this panel, which means letting whatever derives from it draw.
	/// </summary>
	/// <remarks>
	/// <b>There used to be a matrix built here and nothing ever read it.</b> Every HUD child computed two
	/// NDC conversions and a scale-times-translation into a <c>modelMatrix</c> field with no reader anywhere
	/// in the tree - every frame, for every panel. The field is gone and the arithmetic with it; the two
	/// conversions it used are kept because they are this class's answer to "where would a panel be", and a
	/// subclass that starts positioning itself will want them.
	/// </remarks>
	public void Draw()
	{
		OnRender();
	}

	public void Delete()
	{
		OnDelete();
		Level.Current.Hud.Children.Remove( this );
	}
}
