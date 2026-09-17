namespace OpenTPW.UI;

/// <summary>
/// A meter - control type 9 in the layout stream, and the only one of the thirteen types that shows a
/// number as a quantity rather than as lettering.
///
/// <para>
/// <b>It carries no mesh, which is why it looked empty.</b> <c>FUN_0066d750</c> builds it as the plain
/// control with three more fields, and the original hands it a <c>meter.wct</c> skin through
/// <c>FUN_00477870</c> rather than a model - so a control drawing only <see cref="UiControl.Mesh"/> can
/// never show one. The texture ships in ui.wad at <c>textures\meter.wct</c>, 128x128.
/// </para>
/// <para>
/// <b>The value arrives through the vtable, clamped.</b> <c>PTR_FUN_00705c28</c> slot <c>+0x1c</c> is
/// <c>FUN_0066d885</c>: it writes the value to the control's own <c>+0x140</c>, holds it to
/// <b>0 to 0x400</b>, and then passes it to two sub-objects at <c>+0x98</c> and <c>+0x9c</c> through
/// their own vtable+0xc. So the original's full scale is <b>1024</b>, not a hundred, and whoever sets it
/// scales first - which is why <see cref="Max"/> is taken here rather than assumed.
/// </para>
/// <para>
/// <b>WHICH WAY IT FILLS IS A CHOICE, AND IT IS SAID SO RATHER THAN IMPLIED.</b> The two sub-objects
/// above are what actually paint, and their geometry has not been traced - slot <c>+0x08</c> of that
/// vtable is a destructor, not a paint, so the obvious road was a dead end. What is known is that the
/// housing is taller than it is wide (the gauge's meter is 59 by 224 on the virtual screen) and that the
/// original's gauge "rests at its lowest part" when it has nothing to show. Filling upward from the
/// bottom is the reading those two facts support; it is not a measurement, and if the artwork is ever
/// read back properly this is the line to correct.
/// </para>
/// </summary>
internal sealed class UiMeter : UiControl
{
	/// <summary>The skin the original gives it - see the class remarks.</summary>
	private const string Skin = "ui/textures/meter.wct";

	/// <summary>Loaded once and shared, as <see cref="UiEdit"/>'s selection highlight is.</summary>
	private static Texture? _skin;

	/// <summary>
	/// What counts as full. The original's own clamp is 1024 (see the class remarks), and a caller
	/// working in percent says so by leaving this at a hundred rather than by scaling on the way in.
	/// </summary>
	public int Max { get; init; } = 100;

	/// <summary>How far up it stands, in the same units as <see cref="Max"/>. Out-of-range values are held, not refused.</summary>
	public int Value { get; set; }

	protected override void OnDraw()
	{
		// Whatever the control itself carries first - it has no mesh of its own, but a meter given one
		// should still draw it, and its text rectangle is honoured the same way every other control's is.
		base.OnDraw();

		if ( Max <= 0 )
			return;

		var filled = Math.Clamp( Value, 0, Max ) / (float)Max;

		// Nothing at all at rest, rather than a sliver: the original's own empty reading is the bottom of
		// the housing, and a one-pixel band there would read as a value nobody set.
		if ( filled <= 0f )
			return;

		_skin ??= new Texture( Skin );
		Material.UI.Set( "Color", _skin );

		var area = Pixels;
		var height = area.Height * filled;
		var top = area.Y + area.Height - height;

		// The bottom of the texture for the bottom of the bar, so the artwork is cropped rather than
		// squashed - a squashed gauge shows every part of its face at every value, which is exactly what
		// a meter must not do.
		var uvs = new Rectangle( 0f, 1f - filled, 1f, filled );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( area.X, Screen.Height - top - height, area.Width, height ), uvs, Material.UI );
	}
}
