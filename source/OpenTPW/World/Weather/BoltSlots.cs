using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The bolts alive at once: sixteen slots, as the original's (the count at <c>+0x3308</c> of the bolt renderer, the
/// records <c>0x30</c> bytes from <c>+0x3008</c>; a draw with all sixteen taken is dropped, 0x0058032c). Each frame the
/// live ones lose the frame's ms, capped at 500 (0x0057ff74..0x0057ff99), and one whose life left reaches 0 is cut and
/// not drawn that frame, the last record copied over it (0x0057ee81..0x0057eeaf).
/// </summary>
internal sealed class BoltSlots
{
	public const int Capacity = 16;

	/// <summary>The most a frame can age a bolt, ms (0x0057ff83).</summary>
	public const float MaxFrameMs = 500f;

	public struct Bolt
	{
		public Vector3 Ground;
		public Vector3 Top;

		/// <summary>The life left in ms. The original's record holds nothing else.</summary>
		public float LifeMs;

		/// <summary>The lifetime it was struck with and the seconds since: only the lobby's sun lift is drawn from them, the bolt's own grey is not.</summary>
		public float DurationMs;
		public float AgeSeconds;

		/// <summary>The grey it was last drawn in, once drawn (<see cref="BoltEnvelope"/>).</summary>
		public bool Rolled;
		public byte R, G, B;
	}

	private readonly Bolt[] _bolts = new Bolt[Capacity];

	public int Count { get; private set; }

	public Bolt this[int slot] => _bolts[slot];

	/// <summary>Takes a bolt for <paramref name="lifetimeSeconds"/>; false, and nothing kept, when all slots are in use or the lifetime is nought.</summary>
	public bool Add( Vector3 ground, Vector3 top, float lifetimeSeconds )
	{
		if ( Count >= Capacity || lifetimeSeconds <= 0f )
			return false;

		_bolts[Count++] = new Bolt
		{
			Ground = ground,
			Top = top,
			LifeMs = lifetimeSeconds * 1000f,
			DurationMs = lifetimeSeconds * 1000f,
		};

		return true;
	}

	/// <summary>
	/// The colour <paramref name="slot"/> is drawn in this frame, 0 to 1 a channel, times <paramref name="fade"/>: its grey is
	/// the next draw's (<paramref name="rand"/>, a 15-bit draw of Lightning's own generator, standing for the CRT's <c>rand()</c>, 0x0067b5c0) when <paramref name="redraw"/> is set or it has none
	/// yet, else the last one kept. The channels are the grey's own, neutral, with no tint.
	/// </summary>
	public Vector3 Draw( int slot, int rand, bool redraw, float fade )
	{
		ref var bolt = ref _bolts[slot];

		if ( redraw || !bolt.Rolled )
		{
			(bolt.R, bolt.G, bolt.B) = BoltEnvelope.Colour( (int)bolt.LifeMs, rand );
			bolt.Rolled = true;
		}

		return new Vector3( bolt.R, bolt.G, bolt.B ) * (fade / 255f);
	}

	/// <summary>Ages every live bolt by a frame of <paramref name="seconds"/> and cuts those whose life left reaches 0, last record first as the original walks them.</summary>
	public void Advance( float seconds )
	{
		var ms = Math.Min( seconds * 1000f, MaxFrameMs );

		for ( var i = Count - 1; i >= 0; --i )
		{
			_bolts[i].LifeMs -= ms;
			_bolts[i].AgeSeconds += seconds;

			if ( _bolts[i].LifeMs > 0f )
				continue;

			_bolts[i] = _bolts[--Count];
		}
	}
}
