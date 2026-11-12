namespace OpenTPW;

/// <summary>
/// The black the park-entry fly-in fades to: <c>docs/exe/lobby.md</c>, "Escape cancels the fly-in", its paragraph "And the
/// fly-in darkens the screen".
///
/// <para>
/// The island camera's state 2 writes a target to the render camera's <c>+0x60</c> (<c>0x005e052f</c>): <see cref="Target"/>
/// while the radius is 8 or more, the whole of it just before the park is asked for; states 0 and 1 and the attract flight
/// write nought. The render camera's update (<c>0x005d8cb0</c>) eases its <c>+0x5c</c> toward that target and draws a black
/// rectangle over the view with alpha <see cref="AlphaOf"/> (<c>0x008bd4e4</c>, drawn at <c>0x00576e54</c>). The value here is
/// a fraction of the original's <c>0x10000</c>.
/// </para>
/// <para>
/// <b>The ease is per pass in the original</b> (an eighth of the gap, <c>SAR 3</c>, not scaled by its delta), so its speed is
/// the frame rate's. Converted through <see cref="Time.SmoothingFactor"/> at <see cref="Rate"/> (rule 10): an eighth a pass at the
/// 30 passes a second the original runs at (its cap, measured in Q670) is the same curve per second on any machine.
/// </para>
/// </summary>
internal static class LobbyFade
{
	/// <summary>
	/// How fast the fade closes on its target, per second: <c>-30 ln( 7/8 )</c>, the original's eighth of the gap per lobby pass
	/// (<c>0x005d8cb0</c>) at its 30 passes a second. A cancel's lift from full black to an alpha of nought takes the
	/// original's ~42 passes, about 1.4 s, at this rate.
	/// </summary>
	internal const float Rate = 4.0059f;

	/// <summary>The radius the target counts from, the camera's arrival radius (<c>_DAT_00702c68</c>, 8.0).</summary>
	internal const float NearRadius = 8f;

	/// <summary>The most the target reaches while the radius is 8 or more: <c>0xffff</c> of <c>0x10000</c>, the clamp's top.</summary>
	internal const float MostBeforeArrival = 65535f / 65536f;

	/// <summary>How dark the view is now, 0 to 1 (1 being the original's <c>0x10000</c>).</summary>
	internal static float Value { get; private set; }

	/// <summary>What <see cref="Value"/> is easing toward.</summary>
	internal static float Wanted { get; private set; }

	private static byte _lastAlpha;
	private static Texture? _black;
	private static readonly byte[] _pixel = [0, 0, 0, 0];

	/// <summary>
	/// State 2's target at radius <paramref name="radius"/>: <c>clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )</c>
	/// (<c>0x005e052f</c>-<c>0x005e05b4</c>), as a fraction of <c>0x10000</c>.
	/// </summary>
	internal static float Target( float radius, float spinRadius )
		=> Math.Clamp( 1f - ((radius - NearRadius) / (spinRadius - NearRadius)), 0f, MostBeforeArrival );

	/// <summary>The rectangle's alpha for a fade of <paramref name="value"/>: <c>min( +0x5c >> 8, 255 )</c> (<c>0x008bd4e4</c>).</summary>
	internal static byte AlphaOf( float value )
		=> (byte)Math.Clamp( (int)(value * 65536f) >> 8, 0, 255 );

	/// <summary>The rectangle's alpha now.</summary>
	internal static byte Alpha => AlphaOf( Value );

	/// <summary>One lobby pass: eases <see cref="Value"/> toward <paramref name="target"/> over this frame's <see cref="Time.Delta"/>.</summary>
	internal static void Step( float target )
	{
		Wanted = target;
		Value += (target - Value) * Time.SmoothingFactor( Rate );

		var alpha = Alpha;

		if ( (alpha == 0) != (_lastAlpha == 0) || (alpha == 255) != (_lastAlpha == 255) )
			Log.Info( $"Lobby fade: alpha {_lastAlpha} -> {alpha} (fade {Value:F4}, target {target:F4})" );

		_lastAlpha = alpha;
	}

	/// <summary>Clears the fade, as every lobby's render camera is built with none (<c>FrontEnd_Init</c>).</summary>
	internal static void Forget()
	{
		Value = 0f;
		Wanted = 0f;
		_lastAlpha = 0;
	}

	/// <summary>For the debug console. A pure getter.</summary>
	internal static string Describe() => $"fade={Value:F4} fadeTarget={Wanted:F4} fadeAlpha={Alpha}";

	/// <summary>
	/// The black rectangle over the whole view, drawn after the world and before the interface, as the original's view draws it
	/// before its interface. Nothing is drawn at alpha nought.
	/// </summary>
	internal static void Draw()
	{
		var alpha = Alpha;

		if ( alpha == 0 )
			return;

		_pixel[3] = alpha;

		if ( _black == null )
			_black = new Texture( _pixel, 1, 1, TextureFlags.OwnPixels );
		else
			_black.UpdatePixels( _pixel );

		Material.UI.Set( "Color", _black );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( 0, 0, Screen.Width, Screen.Height ), Material.UI );
	}
}
