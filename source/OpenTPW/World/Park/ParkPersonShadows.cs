namespace OpenTPW;

/// <summary>
/// A person's shadow on the ground: their picture's quad stood up in the world, laid flat along the sun's direction and
/// set on the land, drawn after the people (<c>SpriteList_Draw</c>'s second pass, <c>0x0058aa4f</c>; <c>SpriteShadow_Draw</c>
/// and <c>SpriteShadow_BuildQuad</c>; <c>docs/exe/park-engine.md</c>, "A person's shadow").
///
/// <para>
/// <b>Which picture.</b> The detail file's <c>GraphicalOptions.SPRITESHADOW</c> picks it: 0 draws none, 1 (medium) the
/// shipped <c>generic/shadow/alphkid</c> silhouette over every person, 2 and 3 the person's own picture
/// (<c>0x0044da97</c>). In the orbit view the own picture is <c>SpriteShadow_Draw</c>'s non-<c>0x40000</c> arm: the body's
/// picture in black at the darkness alpha with the picture's own blend, added through <c>0x00566220</c>, unfogged.
/// <b>In first person</b> (render bit <c>0x80</c>) the original does not
/// use the silhouette at all: <c>SpriteList_Draw</c> calls <c>FUN_00589770</c> (<c>0x0058a800</c>) after each body at any tier
/// but 0, which draws the body's own picture, alpha-blended, black at the darkness alpha
/// (<see cref="OwnPictureColour"/>), fogged through <c>0x00565f90</c>; <see cref="KindFor"/> picks it.
/// </para>
/// <para>
/// <b>How dark.</b> The original keeps one shadow a light for up to four lights travelling downwards
/// (<c>ShadowLights_Add</c> (<c>0x00584be0</c>)); a park makes one, the sun. A light's darkness is its downward share times 127, truncated,
/// and the shadow multiplies what lies under it by <c>255 - darkness</c> over 255 (state word low byte <c>0x13</c>,
/// SRCBLEND ZERO, DESTBLEND SRCCOLOR), cut to the silhouette by the alpha test at 16, writing no depth.
/// </para>
/// </summary>
internal static class ParkPersonShadows
{
	/// <summary>The silhouette <c>0x00550950</c> loads into the sprite list's <c>+0x2400</c> (it names the <c>.tga</c>; the loader finds the <c>.wct</c>).</summary>
	internal const string Silhouette = "generic/shadow/alphkid.wct";

	/// <summary>A picture pixel's length in the world: the span <c>0x0070200c</c>, 20, over the 128 every picture stores.</summary>
	internal const float Span = 20f / 128f;

	/// <summary>
	/// The across times <c>DAT_008bcbcc</c>, the original's screen height over its width, 0.75 at every mode it offers
	/// (all 4:3). The shadow lies in the world, so here it is that constant, not this window's shape.
	/// </summary>
	internal const float Across = 0.75f;

	/// <summary>How far above the land a shadow's corners stand: <c>0x008bd4ec</c>, 0.6 (<c>0x00576a72</c>).</summary>
	internal const float Lift = 0.6f;

	/// <summary>The lift under render word <c>0x2000</c>, the first-person and ride views: 0.25 (<c>0x00576a81</c>).</summary>
	internal const float FirstPersonLift = 0.25f;

	/// <summary>The detail file's <c>GraphicalOptions.SPRITESHADOW</c>: 0 in low.sam, 1 in med.sam, 2 in high.sam. A file that will not read gives medium's 1.</summary>
	internal static int Tier( string detailFile )
	{
		try
		{
			if ( int.TryParse( new SettingsFile( $"/{detailFile}" )["GraphicalOptions.SPRITESHADOW"], out var value ) )
				return value;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Shadows: {detailFile} would not load, so people cast medium's shadows - {e.Message}" );
		}

		return 1;
	}

	/// <summary>
	/// A light's darkness, 0 to 127, from the direction it travels (this engine's Z up): its downward share times 127,
	/// truncated (<c>0x00584d55</c>, the scale <c>0x00701ecc</c> -127), or null for a light that does not travel
	/// downwards, which casts none.
	/// </summary>
	internal static int? Darkness( Vector3 travels )
	{
		var scaled = travels.Z * -127f;
		return scaled > 0f ? (int)scaled : null;
	}

	/// <summary>The shadow's colour, 0xAARRGGBB: opaque, each channel 255 less the darkness (<c>0x00589ab5</c> to <c>0x00589acf</c>).</summary>
	internal static uint Colour( int darkness ) => 0xffffffffu - ((uint)darkness * 0x010101u);

	/// <summary>The darkness word the original's own-picture shadow is added with, 0xAARRGGBB: black, alpha the darkness (<c>0x00589750</c> reads it from <c>0x008c1808</c>, <c>ShadowLights_Add</c> writes <c>trunc(-127 Ly) &lt;&lt; 24</c> at <c>0x00584d97</c>), 0x67000000 under the jungle sun.</summary>
	internal static uint OwnPictureColour( int darkness ) => (uint)darkness << 24;

	/// <summary>What a person's shadow is drawn as.</summary>
	internal enum Kind
	{
		/// <summary>No shadow: tier 0 (<c>0x20000</c>), or a tier no file holds.</summary>
		None,

		/// <summary>Medium's silhouette multiplied onto the ground (<c>SpriteShadow_Draw</c> under <c>0x40000</c>).</summary>
		Silhouette,

		/// <summary>First person's: the body's own picture, alpha-blended in black at the darkness alpha (<c>FUN_00589770</c>).</summary>
		FirstPersonOwnPicture,

		/// <summary>
		/// The orbit view's at tiers 2 and 3 (high): the body's own picture in black at the darkness alpha, the picture's own
		/// blend and no multiply, unfogged (<c>SpriteShadow_Draw</c>'s non-<c>0x40000</c> arm, <c>0x005899c1</c>, <c>0x00589aec</c>,
		/// added with <c>0x00566220</c>).
		/// </summary>
		OrbitOwnPicture
	}

	/// <summary>
	/// The shadow a <paramref name="tier"/> casts in the view. Tier 0 sets <c>0x20000</c>, which ends <c>SpriteList_Draw</c>
	/// before any shadow in either view (<c>0x0058a7a1</c>); in first person (<c>0x80</c>) every other tier takes
	/// <c>FUN_00589770</c> (<c>0x0058a800</c>), which never reads <c>0x40000</c>; elsewhere 1 is the silhouette and 2 and 3 the
	/// own picture (<c>0x0044da97</c>). A tier under 0 would leave the render word as it was, which no file does: none.
	/// </summary>
	internal static Kind KindFor( int tier, bool firstPerson )
		=> tier <= 0 ? Kind.None
		: firstPerson ? Kind.FirstPersonOwnPicture
		: tier == 1 ? Kind.Silhouette
		: Kind.OrbitOwnPicture;

	/// <summary>Whether a <paramref name="kind"/> draws the body's own picture (black at the darkness alpha) rather than the silhouette.</summary>
	internal static bool IsOwnPicture( Kind kind ) => kind is Kind.FirstPersonOwnPicture or Kind.OrbitOwnPicture;

	/// <summary>
	/// A person's shadow's four corners: the picture's width across the light at the person's <paramref name="place"/>
	/// (reflected, both sides negated about it), its height stood up from there and laid flat along
	/// <paramref name="travels"/>, each corner then set <paramref name="lift"/> above <paramref name="ground"/>'s plane
	/// there. Corners 0 and 1 are the picture's top, at the far end; 2 and 3 its foot. Each carries the texture's u and v
	/// there: <paramref name="uv"/>'s Left, Top for corner 0, Right, Top for 1, Right, Bottom for 2 and Left, Bottom for 3,
	/// never flipped by the reflection (as <c>SpriteShadow_BuildQuad</c> does not), or the whole texture when null, as the
	/// silhouette is. False, writing nothing, for a light that does not travel downwards.
	/// </summary>
	internal static bool Corners( Span<Vertex> corners, Vector2 place, int width, int height, int originX, bool mirrored,
		Vector3 travels, Func<float, float, float> ground, float lift, uint colour,
		(float Left, float Top, float Right, float Bottom)? uv = null )
	{
		if ( Darkness( travels ) is null )
			return false;

		var (u0, v0, u1, v1) = uv ?? (0f, 0f, 1f, 1f);

		// The across (Lz, 0, -Lx) of the original's Y-up axes (0x00584d0f to 0x00584d45), unnormalised: shorter as the sun
		// stands higher.
		var across = new Vector2( travels.Y, -travels.X );
		var left = -originX * Span * Across;
		var right = (width - originX) * Span * Across;

		if ( mirrored )
			(left, right) = (-left, -right);

		// The picture's height, stood up from the foot and laid along the light by the matrix ShadowLights_Add builds.
		var tall = height * Span;
		var laid = new Vector2( travels.X, travels.Y ) * (-tall / travels.Z);

		corners[0] = Corner( place + (across * left) + laid, new Vector2( u0, v0 ) );
		corners[1] = Corner( place + (across * right) + laid, new Vector2( u1, v0 ) );
		corners[2] = Corner( place + (across * right), new Vector2( u1, v1 ) );
		corners[3] = Corner( place + (across * left), new Vector2( u0, v1 ) );

		return true;

		Vertex Corner( Vector2 at, Vector2 uv )
			=> new( new Vector3( at.X, at.Y, ground( at.X, at.Y ) + lift ), uv ) { MatFlags = colour };
	}
}
