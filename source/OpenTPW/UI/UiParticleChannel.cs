namespace OpenTPW.UI;

/// <summary>
/// One of the original's UI particle channels (three records of 0x44 bytes at 0x00f86900): up to five effects of one
/// kind, spaced evenly round an ellipse that goes round, with its centre and radii eased from where they were to where
/// they were last set. Channel 0 is the advisor's golden-ticket glints (<see cref="Advisor"/>); channel 2 is the button
/// glints, which <see cref="ButtonGlint"/> places on its own.
///
/// <para>
/// Every frame UIParticles_PlaceChannels (0x005ed770) takes <c>f</c> = the time since the centre was last set over the
/// time it was given, and places effect <c>i</c> of <c>n</c> at the turn <c>i / n + f * turns</c>, its fraction only,
/// round the centre eased by <c>f</c> itself and radii eased by <c>f</c> held to 1: the centre runs on past its mark
/// once the time is up, as the original's does, where the radii stop.
/// </para>
/// <para>
/// <b>A deviation:</b> the original's channel moves on its one 4:3 screen, stretched to any mode. Here the interface keeps
/// its shape and pins by thirds (<see cref="VirtualScreen"/>), so the channel takes, from each centre it is set to, the pin
/// of the third that centre lies in, and eases its pin there with the radii (<see cref="PinShift"/>): the ticket's glints
/// go from his hand on the right to the window's middle and on to the ticket count in the top right.
/// </para>
/// </summary>
internal sealed class UiParticleChannel
{
	private readonly List<int> _handles = new();
	private readonly List<int> _effects = new();

	private (float X, float Y) _fromCentre;
	private (float X, float Y) _centre;
	private (float X, float Y) _fromRadius;
	private (float X, float Y) _radius;
	private float _setAt;
	private float _seconds;
	private float _turns;

	/// <summary>The pin the effects were started with, as fractions of the room beside and below the interface (0 left or top, 1 right or bottom).</summary>
	private (float Across, float Down) _startedPin = (0.5f, 0.5f);
	private bool _pinnedDown;
	private (float Across, float Down) _fromPin = (0.5f, 0.5f);
	private (float Across, float Down) _pin = (0.5f, 0.5f);

	public int Count => _handles.Count;

	/// <summary>The effect the channel's <paramref name="index"/>th was started with.</summary>
	public int EffectOf( int index ) => _effects[index];

	public (float X, float Y) Centre => _centre;
	public (float X, float Y) Radius => _radius;
	public float Turns => _turns;
	public float Seconds => _seconds;

	/// <summary>
	/// Starts or ends effects until there are <paramref name="count"/> (UIParticles_SetChannelCount, 0x005ed5a0): new
	/// ones of <paramref name="effect"/>, the last first when there are too many. Says how many were started, or minus
	/// how many were ended. The new ones are pinned across by <paramref name="anchor"/> and down by <paramref name="down"/>,
	/// wherever they are moved to; with no <paramref name="down"/> each takes its pin down from where it stands.
	/// </summary>
	public int SetCount( int count, int effect = 0, Anchor anchor = Anchor.Centre, UiWindow? owner = null, VerticalAnchor? down = null )
	{
		var system = ParticleSystem.Current;
		var started = 0;

		if ( count > _handles.Count )
		{
			_startedPin = (Fraction( anchor ), down is { } pinnedDown ? Fraction( pinnedDown ) : 0.5f);
			_pinnedDown = down != null;
		}

		while ( _handles.Count > count )
		{
			system?.Kill( _handles[^1] );
			_handles.RemoveAt( _handles.Count - 1 );
			_effects.RemoveAt( _effects.Count - 1 );
			--started;
		}

		while ( _handles.Count < count )
		{
			_handles.Add( system?.Spawn( effect, 0, 0, 0, anchor, owner, down: down ) ?? 0 );
			_effects.Add( effect );
			++started;
		}

		return started;
	}

	/// <summary>
	/// Where the ellipse is centred, as fractions of the half screen from its middle, down positive
	/// (UIParticles_SetChannelCentre, 0x005ed660): it eases there from where it was over <paramref name="seconds"/>, or
	/// is there at once with <paramref name="jump"/>. Nought seconds leaves the clock as it was.
	/// </summary>
	public void SetCentre( bool jump, float x, float y, float seconds, float now )
	{
		_fromCentre = jump ? (x, y) : _centre;
		_centre = (x, y);

		var pin = (Fraction( VirtualScreen.AnchorAt( (1f + x) * VirtualScreen.Width / 2f ) ),
			Fraction( VirtualScreen.VerticalAnchorAt( (1f + y) * VirtualScreen.Height / 2f ) ));
		_fromPin = jump ? pin : _pin;
		_pin = pin;

		if ( seconds != 0f )
		{
			_setAt = now;
			_seconds = seconds;
		}
	}

	/// <summary>
	/// The ellipse's radii, as fractions of the half screen, and how many times it goes round in the centre's time
	/// (UIParticles_SetChannelRadius, 0x005ed700): eased from what they were, or at once with <paramref name="jump"/>.
	/// </summary>
	public void SetRadius( bool jump, float x, float y, float turns )
	{
		_fromRadius = jump ? (x, y) : _radius;
		_radius = (x, y);
		_turns = turns;
	}

	/// <summary>Places the channel's effects for the time <paramref name="now"/> (UIParticles_PlaceChannels, 0x005ed770).</summary>
	public void Place( float now )
	{
		if ( _handles.Count == 0 || ParticleSystem.Current is not { } system )
			return;

		for ( int i = 0; i < _handles.Count; ++i )
		{
			var (x, z) = MovedTo( i, now );
			system.Move( _handles[i], x, 0, z );
		}
	}

	/// <summary>Where effect <paramref name="index"/> is moved to at <paramref name="now"/>: the original's place (<see cref="PlaceOf"/>) and the pin's shift (<see cref="PinShift"/>).</summary>
	internal (int X, int Z) MovedTo( int index, float now )
	{
		var (x, z) = PlaceOf( index, now );
		var (shiftX, shiftZ) = PinShift( now );
		return (x + shiftX, z + shiftZ);
	}

	/// <summary>Where the channel's pin stands at <paramref name="now"/>: eased from the last centre's third to this one's as the radii are.</summary>
	internal (float Across, float Down) PinAt( float now )
	{
		var held = _seconds != 0f ? MathF.Min( (now - _setAt) / _seconds, 1f ) : 0f;
		return (_fromPin.Across + ((_pin.Across - _fromPin.Across) * held), _fromPin.Down + ((_pin.Down - _fromPin.Down) * held));
	}

	/// <summary>
	/// How far, in the units effects are moved in, the channel's effects stand from where their own pin puts them, for the
	/// pin it has eased to (<see cref="PinAt"/>). Nought on a 4:3 window, which has no room beside or below the interface;
	/// down it is nought where the effects were started with no pin down of their own.
	/// </summary>
	internal (int X, int Z) PinShift( float now )
	{
		var scale = VirtualScreen.Scale;
		if ( scale <= 0f )
			return (0, 0);

		var (across, down) = PinAt( now );
		var acrossPixels = VirtualScreen.Offset( Anchor.Right ) * (across - _startedPin.Across);
		var downPixels = _pinnedDown ? VirtualScreen.OffsetDown( VerticalAnchor.Bottom ) * (down - _startedPin.Down) : 0f;

		// A virtual pixel across is 50000 / 1024 of these units, down 37500 / 768.
		return ((int)(acrossPixels / scale * 50000f / 1024f), (int)(downPixels / scale * 37500f / 768f));
	}

	private static float Fraction( Anchor anchor ) => anchor switch { Anchor.Left => 0f, Anchor.Right => 1f, _ => 0.5f };

	private static float Fraction( VerticalAnchor down ) => down switch { VerticalAnchor.Top => 0f, VerticalAnchor.Bottom => 1f, _ => 0.5f };

	/// <summary>Where effect <paramref name="index"/> stands at <paramref name="now"/>, in the units effects are moved in.</summary>
	internal (int X, int Z) PlaceOf( int index, float now )
	{
		var f = _seconds != 0f ? (now - _setAt) / _seconds : 0f;
		var held = MathF.Min( f, 1f );

		var centreX = _fromCentre.X + ((_centre.X - _fromCentre.X) * f);
		var centreY = _fromCentre.Y + ((_centre.Y - _fromCentre.Y) * f);
		var radiusX = _fromRadius.X + ((_radius.X - _fromRadius.X) * held);
		var radiusY = _fromRadius.Y + ((_radius.Y - _fromRadius.Y) * held);

		// CRT_modf (0x0067b280) keeps the fraction, and __ftol truncates the 512ths.
		var round = (index * (1f / _handles.Count)) + (f * _turns);
		var angle = (int)((round - MathF.Truncate( round )) * 512.0);

		var x = centreX + (radiusX * (ParticleSystem.Sine[angle & 0x1ff] * 4 / 1024f));
		var y = centreY - (radiusY * (ParticleSystem.Sine[(angle + 0x80) & 0x1ff] * 4 / 1024f));

		return ((int)((x + 1f) * 50000f), (int)((y + 1f) * 37500f));
	}
}
