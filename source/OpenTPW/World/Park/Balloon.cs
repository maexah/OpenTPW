namespace OpenTPW;

/// <summary>
/// A guest's balloon: a world sprite of kind 10, "balloons", held above them from a shop whose
/// <c>AppearanceEffect</c> is 1 until its life runs out, then burst where it was (<c>docs/exe/ride-operation.md</c>,
/// "The effects of a visit", 4, and "A held balloon").
///
/// <para>
/// <b>Its colour is the guest's id and nothing else.</b> The original reseeds the park's one generator with the id and
/// then draws twice, the bank and the set, both when a shop gives it and when leaving a thing builds it again, so the
/// same guest gets the same colour from every shop and back after every ride.
/// OpenTPW keeps no shared generator (<see cref="ParkGenerator"/>), so the reseed's other effect - every later draw
/// in the park following from that id - is not reproduced.
/// </para>
/// </summary>
public sealed class Balloon
{
	/// <summary>The sprite kind the table at <c>0x00763f88</c> names "balloons" (<c>0x00764090</c>).</summary>
	public const int SpriteKind = 10;

	/// <summary>The item's <c>UsageInfo.AppearanceEffect</c> that gives one (descriptor <c>+0x15c</c>).</summary>
	public const int AppearanceEffect = 1;

	/// <summary>
	/// The held balloon's script, <c>0x0074f480</c>: frame 0 for ever. It is <see cref="SpriteScript"/>'s animation 25.
	/// </summary>
	public const int HeldScript = 1650;

	/// <summary>The fewest needs sweeps a new balloon lasts - <c>DAT_0075d0f0</c>.</summary>
	public const int ShortestLife = 25;

	/// <summary>The most, and the multiplier on the shop's quality - <c>DAT_0075d0f4</c>.</summary>
	public const int LongestLife = 255;

	/// <summary>
	/// How far behind its guest a balloon trails, in thing sweeps - <c>0x0075c910</c>, taken off the frame's fraction
	/// of a sweep for the second sample.
	/// </summary>
	public const float Trail = 0.35f;

	/// <summary>How many steps the bob takes to come round - <c>0x0075c80c</c>.</summary>
	public const int BobSteps = 20;

	/// <summary>How many placements the bob's phase waits between steps: 0.1 added to under 1.0 ten times, then reset.</summary>
	public const int PlacementsPerBob = 11;

	/// <summary>
	/// How many placements a second one balloon makes here. <b>The original places once a rendered frame</b>, so
	/// its bob follows its frame rate and its count of holders; this takes 30 frames a second, the rate
	/// <c>ParkBuildMarkers</c> takes for its own per-frame step, and runs off <see cref="Time.Delta"/>.
	/// </summary>
	public const float PlacementsPerSecond = 30f;

	/// <summary>
	/// The bob's height column, each scaled by 1.5 (<c>0x0075c904</c>): the middle float of the twenty
	/// three-float rows at <c>0x0075c810</c>. The other two, across and down, are nought in every row.
	/// </summary>
	private static readonly float[] Bob =
	[
		0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.1f, 0.12f, 0.14f, 0.16f, 0.18f,
		0.2f, 0.18f, 0.16f, 0.14f, 0.12f, 0.1f, 0.08f, 0.06f, 0.04f, 0f
	];

	/// <summary>The sprite: the held script until it is let go, then the let-go one.</summary>
	public SpriteScript Sprite { get; }

	/// <summary>Where it was last placed, across, in world units - the instance's <c>+0x88</c>.</summary>
	public float X { get; set; }

	/// <summary>Where it was last placed, down - the instance's <c>+0x90</c>.</summary>
	public float Y { get; set; }

	/// <summary>How far above the ground under it - the instance's <c>+0x8c</c>, which the draw adds to the ground.</summary>
	public float Height { get; set; }

	private Balloon( SpriteScript sprite ) => Sprite = sprite;

	/// <summary>
	/// A new balloon for a guest - <c>FUN_00475a10( 0x0074f480, 10, bank, set, 0, 0, 0 )</c>: at the world's corner,
	/// shown from its first turn, one interval after <paramref name="now"/>. Null when the bank has no set to draw,
	/// as the original makes no sprite without its table, and the life is kept all the same.
	/// </summary>
	/// <param name="sets">The balloon bank's sets with pictures in them, <see cref="SetsIn"/>.</param>
	/// <param name="now">The sprite clock, in milliseconds.</param>
	public static Balloon? Make( int thingId, int sets, int now )
	{
		if ( sets <= 0 )
			return null;

		var sprite = new SpriteScript( HeldScript, HeldScript, ColourFor( thingId, sets ), frame: 0 );

		sprite.ScheduleFrom( now );

		return new Balloon( sprite );
	}

	/// <summary>
	/// A balloon the save kept, picked up where it was: the table is saved slot for slot and the guest's
	/// <c>mBalloonScript</c> names its slot (<c>FUN_00475730</c>). Its first turn is one interval after the load,
	/// as a person's is; the save's own due time was read off the clock of the session that wrote it.
	/// </summary>
	public static Balloon Saved( ParkWorld.Sprite saved )
	{
		var sprite = new SpriteScript( saved.Script, saved.Pc, saved.SpriteNumber, saved.Frame, saved.Alpha );

		sprite.ScheduleFrom( 0 );

		return new Balloon( sprite ) { X = saved.X, Y = saved.Y, Height = saved.Height };
	}

	/// <summary>
	/// Which set of the bank a guest's balloon is - the colour. The generator is reseeded with the guest's id word
	/// (<c>FUN_00516370</c>, <c>0x004fe6fc</c>), a draw chooses the bank out of the kind's one (<c>FUN_00541f70</c>,
	/// always nought and taken all the same) and a second the set out of the bank's (<c>FUN_00541fd0</c>), each as
	/// <c>(draw &gt;&gt; 2) % count</c>, unsigned.
	/// </summary>
	public static int ColourFor( int thingId, int sets )
	{
		var state = (uint)(thingId & 0xffff);

		_ = ParkGenerator.Draw( ref state );

		return (int)((ParkGenerator.Draw( ref state ) >> 2) % (uint)sets);
	}

	/// <summary>
	/// How many needs sweeps a new balloon lasts: the shop's quality byte (<c>mQualityOfGoods</c>, the object's
	/// <c>+0x18c</c>) times 255 over 100, truncated, held to 25..255 (<c>0x004fe737</c>..<c>0x004fe76f</c>). A bought
	/// shop's 50 gives 127.
	/// </summary>
	public static int LifeFor( int quality )
		=> Math.Clamp( (quality & 0xff) * LongestLife / 100, ShortestLife, LongestLife );

	/// <summary>
	/// How many sets of a balloon bank have pictures - the count the loader keeps at the bank's <c>+0x21e</c>, of the
	/// sixteen whose frames-per-direction byte is not nought, and what <see cref="ColourFor"/> draws over.
	/// </summary>
	public static int SetsIn( SpriteBankFile bank ) => bank.Sets.Count( set => set.FramesPerDirection != 0 );

	/// <summary>
	/// Where the balloon banks are. There is one in the whole game, <c>SPR_BL</c>, generic to every theme, with four
	/// sets: red, green, blue and yellow.
	/// </summary>
	public const string Folder = "esprites/Generic/Balloons";

	/// <summary>
	/// How many colours the game's balloon bank has, read from its first bank; nought when there is none to read,
	/// which gives balloons no sprite.
	/// </summary>
	public static int SetsIn( BaseFileSystem data )
	{
		try
		{
			var banks = ParkGuestSprites.BanksIn( data, Folder, SpriteKind );

			if ( banks.Length == 0 )
				return 0;

			using var stream = data.OpenRead( banks[0] );

			return SetsIn( new SpriteBankFile( stream ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Balloons: the balloon bank would not load - {e.Message}" );
			return 0;
		}
	}

	/// <summary>
	/// Let go: the same sprite put on the let-go script, which shows frame 1, the burst, where it was last placed and
	/// fades it out - <c>FUN_004fe950</c> through <c>FUN_00475b80</c>. It does not rise or drift.
	/// </summary>
	public void LetGo() => Sprite.StartAt( SpriteScript.LetGoBalloonEntry );

	/// <summary>
	/// Where a held balloon is drawn this frame, and the trailing sample it keeps for the next - <c>FUN_004fa030</c>'s
	/// balloon half (<c>0x004fa184</c>..<c>0x004fa244</c>) and <c>FUN_004fe900</c>.
	///
	/// <para>
	/// The guest is sampled twice, as the drawing places them (<c>FUN_004f9f00</c>): at the frame's fraction of the
	/// sweep, held to 0..1, and <see cref="Trail"/> of a sweep earlier, held to -1..2, so early in a sweep the second
	/// sample lies behind where the sweep began. The balloon goes where the LAST frame's trailing sample was
	/// (<paramref name="lastX"/>, <paramref name="lastY"/>, the guest's <c>mLastPosX</c> and <c>mLastPosY</c>) and
	/// this frame's is handed back for the next: so it trails its guest by a frame and a third of a sweep. Its
	/// height is one, less twice how far the two samples are apart, across plus down, plus the bob; nothing holds
	/// it, so a walking guest's balloon hangs lower.
	/// </para>
	/// </summary>
	/// <param name="alpha">How far through the thing sweep this frame is (<see cref="ParkPeople.ThingTickFraction"/>).</param>
	/// <param name="phase">The bob's phase, shared by every balloon in the park (<see cref="AdvanceBob"/>).</param>
	public static (float X, float Y, float Height, float LastX, float LastY) Place(
		FixedVector previous, FixedVector position, float alpha, float cellX, float cellY,
		int thingId, int phase, float lastX, float lastY )
	{
		var (nowX, nowY) = Sample( previous, position, Math.Clamp( alpha, 0f, 1f ), cellX, cellY );
		var (trailX, trailY) = Sample( previous, position, Math.Clamp( alpha - Trail, -1f, 2f ), cellX, cellY );

		// 0.5 at 0x0075c90c, 1.0 at 0x0075c808; the bob's row is (id + phase) % 20, unsigned.
		var apart = (MathF.Abs( trailX - nowX ) + MathF.Abs( trailY - nowY )) / 0.5f;
		var bob = 1.5f * Bob[(uint)((thingId & 0xffff) + phase) % BobSteps];

		return (lastX, lastY, 1f - apart + bob, trailX, trailY);
	}

	/// <summary>
	/// The bob's phase after some placements - the globals <c>0x007cedd4</c> and <c>0x007cedd8</c>, which every
	/// placement counts toward (<c>0x004fa244</c>..<c>0x004fa28b</c>): a step each <see cref="PlacementsPerBob"/>,
	/// round <see cref="BobSteps"/>. Nothing resets them at a park's load, here or there.
	/// </summary>
	public static (int Phase, float Count) AdvanceBob( int phase, float count, float placements )
	{
		count += placements;

		while ( count >= PlacementsPerBob )
		{
			count -= PlacementsPerBob;
			phase = (phase + 1) % BobSteps;
		}

		return (phase, count);
	}

	/// <summary>
	/// A guest's position at a fraction of the sweep, in world units: <c>prev + (cur - prev) * t</c> truncated to the
	/// fixed point (<c>__ftol</c>, <c>0x004f9fa9</c>, <c>0x004f9fd2</c>), then scaled by the cell.
	/// </summary>
	private static (float X, float Y) Sample( FixedVector previous, FixedVector position, float t, float cellX, float cellY )
	{
		var x = (int)(previous.X + ((double)position.X - previous.X) * t);
		var y = (int)(previous.Y + ((double)position.Y - previous.Y) * t);

		return (x / (float)FixedVector.One * cellX, y / (float)FixedVector.One * cellY);
	}
}
