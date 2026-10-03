namespace OpenTPW;

/// <summary>FUN_00454550/004547c0/004548f0, using the packed saved flags; docs/exe/ride-hoardings.md.</summary>
internal sealed class RideHoardingState
{
	public float Progress { get; private set; }
	public uint Flags { get; private set; }
	public float Rate { get; private set; }
	public bool Active => (Flags & 1) != 0;
	public int Texture => (Flags & 8) != 0 ? 0 : (Flags & 16) != 0 ? 1 : (Flags & 32) != 0 ? 2 : (Flags & 64) != 0 ? 3 : 0;

	public void Close( int kind = 1 )
	{
		var bit = kind switch { 1 => 8u, 2 => 16u, 4 => 32u, 8 => 64u, _ => 0u };
		if ( bit == 0 ) throw new ArgumentOutOfRangeException( nameof( kind ) );
		if ( kind == 1 ? (Flags & 4) != 0 || (Flags & 0x78) == 0 : (Flags & bit) == 0 )
			Flags = (Flags & ~0x78u) | bit;
		Rate = 0.2f;
		Flags = (Flags | 3) & ~4u;
	}

	public void Open()
	{
		Rate = -0.3f;
		if ( Active ) Flags = (Flags & ~2u) | 4;
	}

	public void Restore( uint flags, float progress )
	{
		Flags = flags & 0x7f;
		Progress = progress;
		Rate = Active ? (Flags & 2) != 0 ? 0.2f : (Flags & 4) != 0 ? -0.3f : 0f : 0f;
		if ( (Flags & 0x78) == 0 ) Flags |= 8;
	}

	/// <summary>Cleanup happens on the update after clamping reaches an endpoint.</summary>
	public bool Advance( float delta )
	{
		if ( (Flags & 6) == 0 ) return false;
		var next = Math.Clamp( Progress + Rate * delta, 0f, 1f );
		if ( next != Progress )
		{
			Progress = next;
			return true;
		}
		if ( Progress == 1f ) Flags &= ~2u;
		else if ( Progress == 0f && (Flags & 4) != 0 ) Flags = (Flags & ~0x7du) | 8;
		return false;
	}

	public (float Height, float LowerV) Panel( int index, int count )
	{
		var amount = Math.Clamp( 4f * Progress - 3.2f * index / count, 0f, 1f ) * ((index & 1) == 0 ? 1f : 0.8f);
		return (10f * amount, 1f - amount);
	}
}
