namespace OpenTPW.UI;

/// <summary>
/// The sparkles that circle a button while the pointer is over it.
///
/// <para>
/// UI_Init's hook on every interface message (0x00485780) starts them when the pointer comes onto a
/// button - a control of type 2 in the layout data, which is every <see cref="UiButton"/> here - and
/// only while Popup Help is switched on (0x0078d90e, the options screen's toggle labelled with
/// UIStrings 327, which also switches the interface's pop-up help as a whole). Here that is the help
/// bar's Ctrl+H, and screenshots of the park picker show the help bar and a glint together. Going off the button ends them (0x005edac0), and so does the button being hidden
/// (UIParticles_ButtonGlintStop), which also stops drawing them at once.
/// </para>
/// <para>
/// UIParticles_ButtonGlintStart (0x005ed920) runs between two and five button glints (effect 35) -
/// more for a bigger button, by <c>2 - (int)((width * height - 0.0016) / 0.0051 * -3)</c> with both
/// as fractions of the screen - and every frame 0x005ed770 places them round an ellipse about the
/// button's middle, evenly spaced and going round once a second. The ellipse starts at a third of the
/// button's size and grows to its edges over 400 milliseconds. The glints themselves spin, cycle
/// through a twinkle and change colour on their own.
/// </para>
/// </summary>
internal sealed class ButtonGlint
{
	private const float GrowSeconds = 0.4f;
	private const float TurnsPerGrowth = 0.4f;

	private readonly List<int> _emitters = new();

	private float _started;
	private float _centreX;
	private float _centreY;
	private float _halfWidth;
	private float _halfHeight;

	/// <summary>The pointer came onto <paramref name="button"/>, in <paramref name="window"/>, which the glints are drawn with.</summary>
	public void Start( UiControl button, UiWindow? window )
	{
		var rect = button.Rect;

		_centreX = ((rect.Width / 2) - 0x400 + rect.Left) / 1024f;
		_centreY = ((rect.Height / 2) - 0x300 + rect.Top) / 768f;
		_halfWidth = rect.Width / 2048f;
		_halfHeight = rect.Height / 1536f;

		var count = Math.Clamp( 2 - (int)((_halfWidth * _halfHeight - 0.0016f) / 0.0051f * -3f), 2, 5 );

		SetCount( count, button.Anchor, window );
		_started = Time.Now;

		// Placed before the system next ticks, or the new ones would throw out a glint at the corner.
		Update();
	}

	/// <summary>The pointer went off the button: the glints stop, and those already out fade.</summary>
	public void Leave() => SetCount( 0, default, null );

	/// <summary>The button was hidden: the glints go at once.</summary>
	public void Stop()
	{
		foreach ( var emitter in _emitters )
			ParticleSystem.Current?.Hide( emitter, true );

		SetCount( 0, default, null );
	}

	/// <summary>Places the glints round the button - 0x005ed770, every frame.</summary>
	public void Update()
	{
		if ( _emitters.Count == 0 || ParticleSystem.Current is not { } system )
			return;

		var elapsed = (Time.Now - _started) / GrowSeconds;
		var grown = MathF.Min( elapsed, 1f );

		var radiusX = (_halfWidth / 3f) + ((_halfWidth - (_halfWidth / 3f)) * grown);
		var radiusY = (_halfHeight / 3f) + ((_halfHeight - (_halfHeight / 3f)) * grown);

		for ( int i = 0; i < _emitters.Count; ++i )
		{
			var round = (i / (float)_emitters.Count) + (elapsed * TurnsPerGrowth);
			var angle = (int)((round - MathF.Floor( round )) * 512f);

			var x = _centreX + (radiusX * ParticleSystem.Sine[angle & 0x1ff] * 4 / 1024f);
			var y = _centreY - (radiusY * ParticleSystem.Sine[(angle + 0x80) & 0x1ff] * 4 / 1024f);

			system.Move( _emitters[i], (int)((x + 1f) * 50000f), 0, (int)((y + 1f) * 37500f) );
		}
	}

	/// <summary>Starts or ends glints until there are <paramref name="count"/> - UIParticles_SetChannelCount, 0x005ed5a0.</summary>
	private void SetCount( int count, Anchor anchor, UiWindow? window )
	{
		var system = ParticleSystem.Current;

		while ( _emitters.Count > count )
		{
			system?.Kill( _emitters[^1] );
			_emitters.RemoveAt( _emitters.Count - 1 );
		}

		while ( system != null && _emitters.Count < count )
			_emitters.Add( system.Spawn( (int)ParLib.P_EFFECT_Button, 0, 0, 0, anchor, window ) );
	}
}
