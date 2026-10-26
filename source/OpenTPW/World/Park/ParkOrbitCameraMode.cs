using NeoVeldrid;
using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The park's own camera, as the original flies it.
///
/// <para>
/// The original keeps this camera as a save module of its own ("SAD_CAMERA", chunk magic <c>KAME</c>,
/// code 0x0042a190-0x0042d130), and its persisted state is six globals: where it is looking, how far
/// round it has been spun, how far out it is zoomed, a flags word, and the look-at point and spin that
/// first person saves to put back. Everything else below is
/// derived from those each frame, which is why this has so few fields.
/// </para>
///
/// <para>
/// Zoom drives pitch, so there is only one control for "how close am I": pulling in also lowers the
/// camera towards the ground, and pushing out raises it towards a plan view.
/// </para>
///
/// <para>
/// The three pieces of state are static so that the debug console can drive the camera without holding
/// the instance, the same way <see cref="LobbyCameraMode"/> exposes its own.
/// </para>
/// </summary>
public sealed class ParkOrbitCameraMode : CameraMode
{
	/// <summary>
	/// A park under this camera is heard from halfway between the eye and the point it looks at, as the
	/// original's is while its camera-mode mask is clear (<c>0x0054f9e4</c>; <c>docs/exe/audio.md</c>, "The
	/// listener, and what a pause does to it").
	/// </summary>
	public override Vector3 Ears => (Position + PointOfInterest) * 0.5f;

	/// <summary>
	/// What the camera looks at, on the ground. The original's default is (475, 175) in the park's own
	/// axes, which is grid cell (47, 17) - the park entrance's <c>EntranceA</c> in <c>Standard.sam</c>, one
	/// cell west of the (48, 17) that <c>MapInfo.FixedItemOrigin</c> names. A cell is 10 units across and
	/// its centre is at <c>grid * 10 + 5</c>, which is exactly where those two numbers come from.
	/// </summary>
	public static Vector3 PointOfInterest { get; set; } = new( 475f, 175f, 0f );

	/// <summary>How far the camera has been spun about the point of interest, in radians.</summary>
	public static float Yaw { get; set; }

	/// <summary>
	/// Distance from the point of interest, and the only "how close" control - <see cref="Pitch"/> is
	/// derived from it. The original starts at 110 and clamps to 70..130.
	/// </summary>
	public static float Zoom { get; set; } = 110f;

	public const float MinZoom = 70f;
	public const float MaxZoom = 130f;

	/// <summary>Pitch pulled all the way in, in degrees.</summary>
	private const float PitchAtMinZoom = 45f;

	/// <summary>Pitch pushed all the way out, in degrees.</summary>
	private const float PitchAtMaxZoom = 65f;

	/// <summary>
	/// The original's own ceiling on pitch. An oddly precise number because it clamps the result of the
	/// interpolation rather than being a design value, and it is kept exactly as it reads there so that
	/// nothing here quietly rounds the camera somewhere the original would not put it. With the range
	/// above it is never actually reached; it will matter if zoom is ever driven outside 70..130.
	/// </summary>
	private const float MaxPitch = 84.2673f;

	/// <summary>
	/// Pitch for the current zoom: 45 degrees pulled in, 65 pushed out, straight line between, then
	/// clamped. Zoom and pitch are one control in the original and they stay one control here.
	/// </summary>
	public static float Pitch => PitchAt( Zoom );

	/// <summary>The pitch <see cref="Pitch"/> gives at a zoom, in degrees.</summary>
	internal static float PitchAt( float zoom )
	{
		var t = ((zoom - MinZoom) / (MaxZoom - MinZoom)).Clamp( 0f, 1f );
		var degrees = PitchAtMinZoom + ((PitchAtMaxZoom - PitchAtMinZoom) * t);

		return degrees.Clamp( 0f, MaxPitch );
	}

	/// <summary>
	/// How fast the eye catches up with the ground beneath it.
	///
	/// <para>
	/// The original eases it with a one-pole filter at alpha 0.5 - half the remaining distance every
	/// tick - and its park loop ticks at a fixed 31ms. That is a half-life of 31ms, so the rate here
	/// is <c>ln(2) / 0.031</c>. Written as a rate rather than as a per-frame fraction because a fixed
	/// fraction settles at a different speed on every machine; see <see cref="Time.SmoothingFactor"/>.
	/// </para>
	/// </summary>
	private const float GroundFollowRate = 22.36f;

	/// <summary>The eased ground height, and whether it has ever been sampled - see <see cref="GroundUnderPoi"/>.</summary>
	private float _groundHeight;

	private bool _groundSampled;

	/// <summary>The ground under the point of interest right now, without touching the easing.</summary>
	/// <remarks>See <c>ParkCamcorderCameraMode.PeekGround</c> - the first un-eased sample belongs to
	/// the first <see cref="Update"/>, not to a constructor.</remarks>
	private static float PeekGround()
		=> ParkGround.Current?.Heightfield?.HeightAtWorld( PointOfInterest.X, PointOfInterest.Y ) ?? 0f;

	/// <summary>
	/// The ground under the point of interest, eased. The original raises the eye by this so that
	/// scrolling over a cliff lifts the camera with the land rather than burying it.
	///
	/// <para>
	/// The first sample is taken outright rather than eased into, so that a park opens with the camera
	/// already at the right height instead of rising into place over the first second. Zero where
	/// there is no ground to ask - which is any scene that is not a park.
	/// </para>
	/// </summary>
	private float GroundUnderPoi()
	{
		var field = ParkGround.Current?.Heightfield;

		if ( field == null )
			return 0f;

		var sample = field.HeightAtWorld( PointOfInterest.X, PointOfInterest.Y );

		if ( !_groundSampled )
		{
			_groundHeight = sample;
			_groundSampled = true;
		}
		else
		{
			_groundHeight = _groundHeight.LerpTo( sample, Time.SmoothingFactor( GroundFollowRate ) );
		}

		return _groundHeight;
	}

	/// <summary>
	/// How fast Up or Down held moves the zoom, in units a second: the original's 0.05 a millisecond (<c>0x006fddbc</c>,
	/// <c>0x0042b4e2</c>..<c>0x0042b512</c>), so the whole 70 to 130 in 1.2 s.
	/// </summary>
	internal const float ZoomKeyRate = 50f;

	/// <summary>
	/// How fast the pan's term catches up with a held key: half the gap every 31 ms. The original halves it every frame
	/// (<c>0x006fddfc</c>), so its rise is quicker at a higher frame rate; this one is the same at every rate, its frame
	/// taken as the park's 31 ms tick.
	/// </summary>
	private const float PanRiseRate = 22.36f;

	/// <summary>
	/// How fast the pan's term coasts away with no key held, a second: the original divides it by <c>1 + 0.01 dt</c> a
	/// frame (<c>0x006fddc8</c>), which this is at a frame of no length. The original's coast totals exactly 100 times the
	/// term at any frame rate; this one steps the term and then moves by it, so it totals less, and by an amount that
	/// depends on the frame rate: 10.1 units from a term of 104.9 a second at 144 frames a second, 9.6 at 60 and 8.8 at 30,
	/// against the original's 10.5.
	/// </summary>
	private const float PanCoastRate = 10f;

	/// <summary>
	/// The pan's term, in world units a second along the map's own axes: the original's two scroll terms
	/// (<c>0x00790a88</c>, <c>0x00790a90</c>; <c>docs/exe/park-engine.md</c>, "The point of interest's edge"), so a pan
	/// eases up and coasts out.
	/// </summary>
	internal static Vector2 Scroll { get; set; }

	/// <summary>
	/// Whether the last update had a pan key held: the original ends a locator's follow on any frame with a pan's input
	/// (<c>0x0042ba37</c>, <c>0x0042ba92</c>), which <see cref="UI.ParkLocator"/> reads at its next frame.
	/// </summary>
	public static bool Panning { get; private set; }

	/// <summary>The zoom keys' and the pan keys' last frame, so the log says when each starts and ends.</summary>
	private static float _zoomHeld;

	private static (float Forward, float Right) _panHeld;

	/// <summary>Whether the reset keys were held last frame, so the count is one a press.</summary>
	private static bool _resetHeld;

	private static float _panSeconds;

	/// <summary>Whether the edge held the pan last frame, so the log says once when it starts to.</summary>
	private static bool _atEdge;

	/// <summary>
	/// The angle the original's park camera hands its projection: pi/2 at its reset (<c>FUN_0042a990</c>,
	/// <c>0x0042aa2f</c>), near 0.1, far 1000.
	/// </summary>
	internal const float HandedAngle = 90f;

	/// <summary>
	/// The picture the park draws, 53.13 degrees top to bottom at 4:3 and 67.38 across: the handed 90
	/// through the original's screen scale (<see cref="Camera.DrawnAngle"/>). The camcorder's first person
	/// draws with the same lens.
	/// </summary>
	/// <remarks>
	/// The far plane stays Camera's own 10000 rather than the original's 1000. Nothing here depends on it -
	/// it costs only depth precision - so it is left alone rather than reaching into shared code to suit one
	/// camera.
	/// </remarks>
	public static float Lens => Camera.DrawnAngle( HandedAngle );

	public ParkOrbitCameraMode()
	{
		FieldOfView = Lens;

		// Already over the point of interest before anything reads the camera - see Place. The ground
		// is PEEKED rather than sampled, for the reason ParkCamcorderCameraMode.PeekGround gives: the
		// one un-eased sample belongs to the first Update, when the statics have settled.
		Place( PeekGround() );
	}

	public override void Update()
	{
		// Down to the ground and back, the way the original's 'C' does - see ParkCamcorderCameraMode.
		// Handled here rather than somewhere central because a camera mode is what knows when it should
		// give way, which is how LobbyCameraMode takes its own keys. Not while a menu holds the park,
		// or the key would swap the camera out from under an open window. On the key's release: C is the shortcuts
		// table's row 16 (0x0040c5c0), and the park runs its tables on a key-up (FUN_0040c990; docs/exe/scenes.md,
		// "The park Escape route"). Under the full-screen view only the camera's table is run (0x004a2918), and C is not
		// in it.
		if ( Level.Current?.PausedByWindow() != true && Level.Current?.FullScreenView != true
			&& Input.KeyUp( InputButton.CamcorderMode ) )
		{
			ParkCamcorderCameraMode.Enter();
			return;
		}

		// The turn reuses the Left and Right arrows' bindings, the original's keys; its step and its easing are not the
		// original's (docs/exe/park-engine.md, "The turn"; Q23).
		//
		// Not while one of the park's screens is open: opening one switches the camera's table off and clears its
		// rows' latches (FUN_00485b70, 0x00485ccd), and the screen's handler runs the shortcuts' table alone, so no
		// camera key is heard until it closes. The mouse still reaches the camera from the park beside the screen. The park
		// map is handed to the same switch (0x005f0c01), and its own handler takes the arrow keys.
		//
		// Nor under a message box or the Save or Load screen: each opener calls FUN_00486b70 (0x0047efa0, 0x0049f406),
		// which switches the camera's table off and clears its latches (docs/exe/hud.md, "The research opener and its two boxes";
		// docs/exe/saves.md, "One screen, two uses"). That is WindowStack.CameraTableOff, the narrower test than
		// Level.PausedByWindow: the game menu and the options screen also pause, but whether their openers switch the
		// table off is not read, so the keys are left to them.
		var keys = !UI.WindowStack.ParkScreenOpen && !UI.WindowStack.MapOpen && !UI.WindowStack.CameraTableOff;

		if ( keys && Input.Pressed( InputButton.RotateLeft ) )
			Yaw -= MathF.PI / 4f;

		if ( keys && Input.Pressed( InputButton.RotateRight ) )
			Yaw += MathF.PI / 4f;

		// Not if the interface already used it, or the wheel over a scrolling list would scroll the list
		// AND zoom the park behind it. Read in the same frame it is written: the HUD updates in Level.Update
		// and this runs from Level.Render, in that order, so there is no lag.
		//
		// Handled here rather than centrally for the reason the camcorder key above gives: a camera
		// mode is what knows when it should give way.
		if ( Input.Mouse.Wheel != 0 && !UI.WindowStack.WheelTaken )
			Zoom = (Zoom - (Input.Mouse.Wheel * 5f)).Clamp( MinZoom, MaxZoom );

		// The keys a box being typed into holds are the box's.
		var down = keys && !Input.TextCaptured ? Input.Keyboard.KeysDown : [];
		var zoom = ZoomInput( down );

		if ( zoom != 0f )
			Zoom = (Zoom + (zoom * ZoomKeyRate * Time.Delta)).Clamp( MinZoom, MaxZoom );

		if ( zoom != _zoomHeld )
			Log.Info( $"Camera: zoom by key {(zoom < 0f ? "in" : zoom > 0f ? "out" : "let go")}, zoom {Zoom:F2}" );

		_zoomHeld = zoom;

		// Keypad 5 and Home reset the camera in the original (camera rows 19 and 24, 0x0042a500, applied at 0x0042b518: zoom
		// 110, rotation 0, the point (475, 175), the follow cleared; docs/exe/park-engine.md, "The point of interest's
		// edge"). Not built: counted, so a press that does nothing here is not silent.
		var reset = ResetInput( down );

		if ( reset && !_resetHeld )
			Unimplemented.Report( "CAMERA_HOME_RESET" );

		_resetHeld = reset;

		var (forward, right) = PanInput( down );
		var panned = Panning;
		Panning = forward != 0f || right != 0f;
		var moving = Scroll != Vector2.Zero;
		(PointOfInterest, Scroll, var atEdge) = StepPan( PointOfInterest, Scroll, PanTarget( forward, right, Yaw, Zoom ), Panning,
			EdgeOf( ParkGround.Current?.Heightfield ) );

		if ( atEdge && !_atEdge )
			Log.Info( $"Camera: pan held at the edge, point ({PointOfInterest.X:F3},{PointOfInterest.Y:F3}), scroll {Scroll.Length:F1} a second" );

		_atEdge = atEdge;

		if ( moving && Scroll == Vector2.Zero )
			Log.Info( $"Camera: pan at rest, point ({PointOfInterest.X:F3},{PointOfInterest.Y:F3}) cell ({MathF.Floor( PointOfInterest.X / 10f ):F0},"
				+ $"{MathF.Floor( PointOfInterest.Y / 10f ):F0})" );

		if ( Panning != panned || (Panning && (forward, right) != _panHeld) )
		{
			Log.Info( $"Camera: pan by key {(Panning ? $"forward {forward:F0} right {right:F0}" : $"let go after {_panSeconds:F3} s")}, scroll "
				+ $"{Scroll.Length:F1} a second, point ({PointOfInterest.X:F1},{PointOfInterest.Y:F1}) yaw {Yaw:F2} zoom {Zoom:F0}" );
			_panSeconds = 0f;
		}

		if ( Panning )
			_panSeconds += Time.Delta;

		_panHeld = (forward, right);

		Place( GroundUnderPoi() );
	}

	/// <summary>
	/// Which way the held keys pan the view, forward and right on the screen, each -1, 0 or 1. The keypad's 8, 2, 4 and
	/// 6 are the original's (camera actions <c>0xd</c> to <c>0x10</c> of <c>0x00748158</c>). <b>W, S, A and D are not the
	/// original's</b>: its tables bind W and D to nothing and A and S to the shortcuts' all-items and all-staff rows,
	/// which here are O and E (<c>ParkFrontEnd.ShortcutRow</c>). As each of the original's rows, a key counts only with no
	/// modifier held, so Ctrl+S and Alt+S pan nothing; both ways held cancel, where the original's later row wins.
	/// </summary>
	internal static (float Forward, float Right) PanInput( IReadOnlyCollection<Key> down )
	{
		if ( Input.HeldIn( down ) != Input.Modifiers.None )
			return (0f, 0f);

		var forward = Held( down, Key.W, Key.Keypad8 ) - Held( down, Key.S, Key.Keypad2 );
		var right = Held( down, Key.D, Key.Keypad6 ) - Held( down, Key.A, Key.Keypad4 );

		return (forward, right);
	}

	/// <summary>
	/// Which way the held keys zoom, -1 in or 1 out: Up and keypad 1 in, Down and keypad 3 out (camera actions 0,
	/// 1, <c>0x15</c>, <c>0x16</c>; bits <c>0x20</c> and <c>0x40</c> of <c>[0x00790aac]</c>, <c>0x0042b38c</c>..<c>0x0042b3e8</c>),
	/// with no modifier held. Both held cancel, as the original's two steps do.
	/// </summary>
	internal static float ZoomInput( IReadOnlyCollection<Key> down )
		=> Input.HeldIn( down ) != Input.Modifiers.None
			? 0f
			: Held( down, Key.Down, Key.Keypad3 ) - Held( down, Key.Up, Key.Keypad1 );

	/// <summary>
	/// Whether a reset key is held, with no modifier: keypad 5 and Home, the camera table's rows 19 and 24 (action
	/// <c>0x14</c>, <c>0x18</c>).
	/// </summary>
	internal static bool ResetInput( IReadOnlyCollection<Key> down )
		=> Input.HeldIn( down ) == Input.Modifiers.None && Held( down, Key.Home, Key.Keypad5 ) != 0f;

	private static float Held( IReadOnlyCollection<Key> down, Key a, Key b )
		=> down.Contains( a ) || down.Contains( b ) ? 1f : 0f;

	/// <summary>
	/// The pan a held key settles at, in world units a second on the map's axes: <c>sqrt( zoom ) x 0.01</c> a millisecond,
	/// 104.9 a second at zoom 110 (<c>0x006fddf8</c>, <c>0x006fddfc</c>), turned by the yaw so that forward is forward on
	/// the screen - the same basis the eye is placed from and <see cref="ParkCamcorderCameraMode"/> walks along.
	/// </summary>
	internal static Vector2 PanTarget( float forward, float right, float yaw, float zoom )
	{
		var speed = MathF.Sqrt( zoom ) * 10f;

		return new Vector2(
			((forward * -MathF.Sin( yaw )) + (right * MathF.Cos( yaw ))) * speed,
			((forward * MathF.Cos( yaw )) + (right * MathF.Sin( yaw ))) * speed );
	}

	/// <summary>
	/// The pan's term a frame on: eased up to the target while a key is held, coasting out with none, and nought under
	/// 0.1 a second, the original's 0.0001 a millisecond (<c>0x0074c988</c>).
	/// </summary>
	/// <remarks>
	/// Each axis is noughted on its own. The original noughts its three terms (the two here and the one it keeps for
	/// the height) only when all three are under the floor (<c>0x0042bd69</c>..<c>0x0042bdc3</c>), so an axis under it
	/// stays while the other is over; the difference is below 0.1 a second.
	/// </remarks>
	internal static Vector2 StepScroll( Vector2 term, Vector2 target, bool held )
	{
		var next = held
			? term + ((target - term) * Time.SmoothingFactor( PanRiseRate ))
			: term * (1f - Time.SmoothingFactor( PanCoastRate ));

		return new Vector2( MathF.Abs( next.X ) < 0.1f ? 0f : next.X, MathF.Abs( next.Y ) < 0.1f ? 0f : next.Y );
	}

	/// <summary>
	/// A frame of the pan: the term stepped towards the target, held at the edge by where the point stood before the move
	/// (<see cref="BoundScroll"/>), then the point moved by it, as the original's order (<c>0x0042b668</c> flags,
	/// <c>0x0042c5ed</c> moves); and whether the edge changed the term.
	/// </summary>
	internal static (Vector3 Point, Vector2 Term, bool AtEdge) StepPan( Vector3 point, Vector2 term, Vector2 target, bool held, Vector2? edge )
	{
		var stepped = StepScroll( term, target, held );
		var next = BoundScroll( stepped, term, new Vector2( point.X, point.Y ), edge );

		return (point + new Vector3( next.X * Time.Delta, next.Y * Time.Delta, 0f ), next, next != stepped);
	}

	/// <summary>
	/// The far corner of the point's soft bound: the heightfield's cell counts times its cell sizes (<c>+0x18 x +0x10</c>,
	/// <c>+0x1c x +0x14</c> of <c>[[0x007a0854] + 0x6c]</c>), 960 by 850 in Lost Kingdom, not the 128-cell map; none
	/// without a heightfield, which leaves the point unbounded.
	/// </summary>
	internal static Vector2? EdgeOf( HeightfieldFile? field )
		=> field is null ? null : new Vector2( field.CellsX * field.CellSizeX, field.CellsY * field.CellSizeY );

	/// <summary>
	/// The pan's term held at the heightfield's edge, as the original's soft bound (<c>0x0042b668</c>..<c>0x0042b716</c>,
	/// <c>0x0042bca0</c>..<c>0x0042bd5f</c>; <c>docs/exe/park-engine.md</c>, "The point of interest's edge"): where the point
	/// stood past an edge before this frame's move (x or y under nought, or over <paramref name="edge"/>), an axis whose new
	/// term points further out takes the old term coasted instead. The push is dropped and the coast runs out, so a held
	/// pan stops about a cell past the edge; nothing pulls the point back, and a pan inward is free.
	/// </summary>
	/// <remarks>
	/// <b>Not the original's step:</b> the original divides the old term by <c>1 + 0.01 dt</c> on a frame of <c>dt</c>
	/// milliseconds; this coasts it as <see cref="StepScroll"/> does with no key, on <c>Time.Delta</c> (CLAUDE.md rule 10),
	/// so a held pan at zoom 110 stops about 10.2 units past the edge at 144 frames a second and 11.1 at 30, where the
	/// original's stops 10.5 plus part of its crossing frame's step past it (10.6 to 13.6 measured, Q625): the same cell.
	/// </remarks>
	internal static Vector2 BoundScroll( Vector2 next, Vector2 old, Vector2 point, Vector2? edge )
	{
		if ( edge is not { } far )
			return next;

		var coast = StepScroll( old, Vector2.Zero, false );

		return new Vector2(
			Outward( point.X, next.X, far.X ) ? coast.X : next.X,
			Outward( point.Y, next.Y, far.Y ) ? coast.Y : next.Y );

		static bool Outward( float at, float term, float far )
			=> (at < 0f && term < 0f) || (at > far && term > 0f);
	}

	/// <summary>
	/// Swings the eye around the point of interest and looks back at it.
	/// </summary>
	/// <remarks>
	/// Called from the constructor as well as from <see cref="Update"/>, for the reason
	/// ParkCamcorderCameraMode.Place gives: the camera is swapped and its view matrix built in the
	/// same <see cref="Camera.Update"/>, so a mode that waited for its first update would have a
	/// frame drawn from the world origin.
	/// </remarks>
	private void Place( float ground )
	{
		Position = EyeAt( PointOfInterest, Yaw, Zoom, ground );

		// Only Forward is taken from this - see Rotation.LookAt, whose roll is arbitrary, and the note
		// on it in AudioListener. Camera builds its view from Forward and a fixed world up, so that is
		// all this has to be right about.
		Rotation = Rotation.LookAt( PointOfInterest - Position );
	}

	/// <summary>
	/// Where the eye stands for a point of interest, a spin, a zoom and the eased ground under the point. The original
	/// builds it as the point plus <c>rotateY( yaw, (0, sin(pitch) * zoom, -cos(pitch) * zoom) )</c>, a height and a
	/// horizontal distance swung about the vertical, in its own axes, where Y is up; this engine has Z up, so the height
	/// goes into Z and the swing happens about Z instead. The original's swing puts the eye at the point less
	/// <c>h sin r</c> across, this one at the point plus <c>h sin yaw</c>, so its rotation is this yaw negated: read in the
	/// running original at rotations 0, 90 and 180 (<c>docs/exe/park-engine.md</c>, "The park camera").
	/// </summary>
	internal static Vector3 EyeAt( Vector3 pointOfInterest, float yaw, float zoom, float ground )
	{
		var pitch = PitchAt( zoom ).DegreesToRadians();

		var horizontal = MathF.Cos( pitch ) * zoom;
		var height = (MathF.Sin( pitch ) * zoom) + ground;

		return pointOfInterest + new Vector3(
			horizontal * MathF.Sin( yaw ),
			-horizontal * MathF.Cos( yaw ),
			height );
	}

	/// <summary>
	/// Puts the point of interest on a thing, as a right click on a list's row and a click on an object window's
	/// preview do (<c>FUN_004867b0</c>; <c>docs/exe/park-engine.md</c>, "The camera goes to a thing"): on the corner of
	/// the cell the thing stands in, ten times the cell's column and row, not on the thing's own position. The spin and
	/// the zoom are left as they are. False, and nothing moved, for nought or an id nothing in the park has.
	/// </summary>
	/// <remarks>
	/// The original's write (<c>FUN_0042aab0</c>) also zeroes the camera's three scroll terms
	/// (<c>0x00790a88</c>..<c>0x00790a90</c>); so does this, for the two it keeps (<see cref="Scroll"/>). In first
	/// person the same two words are where the viewer stands, and no screen is open there in the original; here only
	/// the orbit's point moves.
	/// </remarks>
	public static bool GoToThing( int thingId )
	{
		if ( CellOfThing( thingId, ParkPeople.Current, Level.Current?.ParkState ) is not var (x, y) )
			return false;

		PointOfInterest = new Vector3( x * 10f, y * 10f, 0f );
		Scroll = Vector2.Zero;

		Log.Info( $"Camera: to thing {thingId} on cell ({x},{y}), point of interest ({PointOfInterest.X:F0},{PointOfInterest.Y:F0})" );

		return true;
	}

	/// <summary>
	/// The cell a thing stands in - the original's thing bytes <c>+5</c> and <c>+7</c>, the whole part of its
	/// position: a person's from where they walk, a placed object's from its own origin.
	/// </summary>
	internal static (int X, int Y)? CellOfThing( int thingId, ParkPeople? people, ParkState? state )
	{
		if ( thingId == 0 )
			return null;

		if ( people != null )
		{
			if ( people.Guests.TryGetValue( thingId, out var guest ) )
				return guest.Navigator.Position.Cell;

			foreach ( var member in people.Staff )
			{
				if ( member.ThingId == thingId )
					return member.Navigator.Position.Cell;
			}
		}

		if ( state != null && state.TryObject( thingId, out var placed ) )
			return (placed.CellX, placed.CellY);

		return null;
	}

	/// <summary>
	/// Drops where this camera was looking as a scene ends, so the next park does not open wherever
	/// the last one was left - see <see cref="Level.Unload"/>.
	///
	/// <para>
	/// The three pieces of state here are static so they survive <see cref="Camera.SetCameraMode{T}"/>
	/// building a fresh instance, which means they survive the scene too unless something says
	/// otherwise. It matters because a pan, the camera put on a thing and a park's own camera move
	/// <see cref="PointOfInterest"/>, so without this a second park opens looking at a spot that
	/// belonged to the first one.
	/// </para>
	/// </summary>
	public static void Forget()
	{
		PointOfInterest = new Vector3( 475f, 175f, 0f );
		Yaw = 0f;
		Zoom = 110f;
		Scroll = Vector2.Zero;
		Panning = false;
		_atEdge = false;
	}

	/// <summary>
	/// A one-line summary for the debug console. Cell comes back out of the world position by the same
	/// rule that put it in: <c>grid = (world - 5) / 10</c>.
	///
	/// <para>
	/// <c>ground</c> is sampled fresh rather than being the eased value the camera is actually riding,
	/// so it says what the land under the point of interest is, not where the eye has caught up to.
	/// That is the more useful of the two to read back: it is the thing being followed.
	/// </para>
	/// </summary>
	public static string State()
	{
		var ground = ParkGround.Current?.Heightfield?.HeightAtWorld( PointOfInterest.X, PointOfInterest.Y );

		// The cell the point of interest stands in, which is the world position divided by the cell
		// size - not the (world - 5) / 10 above, which recovers the cell a CENTRE belongs to and comes
		// back fractional anywhere else.
		var cellX = (int)MathF.Floor( PointOfInterest.X / 10f );
		var cellY = (int)MathF.Floor( PointOfInterest.Y / 10f );

		var attributes = ParkGround.Current?.Attributes;

		return $"poi=({PointOfInterest.X:F0},{PointOfInterest.Y:F0}) " +
			$"cell=({(PointOfInterest.X - 5f) / 10f:F1},{(PointOfInterest.Y - 5f) / 10f:F1}) " +
			$"yaw={Yaw:F2} zoom={Zoom:F0} pitch={Pitch:F1} " +
			$"ground={(ground.HasValue ? ground.Value.ToString( "F1" ) : "-")} " +
			$"attr={(attributes != null ? attributes.At( cellX, cellY ).ToString() : "-")}";
	}
}
