using System.Numerics;

namespace OpenTPW;

/// <summary>
/// Draws the bumper rides' cars - the Hot Pot's boats, floating in its pot with their riders seated - from the cars
/// <see cref="ParkBumperCars"/> keeps. The original's draw is <c>FUN_00546280</c>, once a frame for each ride
/// (<c>docs/exe/park.md</c>, "Where a bumper ride's cars float").
///
/// <para>
/// <b>Each live car is its ride's supplemental mesh</b> (<see cref="ParkItemCatalogue.Item.SupplementalMeshes"/>, the
/// Hot Pot's <c>b_car.md2</c>), stood where the car floats, turned to its heading and bobbing on its phase, with the
/// clip its car plays: none at rest, role 5 (<c>b_carm</c>) in a go.
/// </para>
/// <para>
/// <b>Two departures, said here.</b> The rocking the original works out from the four corners' heights is not drawn
/// (<c>BUMPER_CAR_ROCK</c>), nor the wake under a boat (<c>BUMPER_CAR_WAKE</c>).
/// </para>
/// </summary>
public sealed class ParkBumperBoats : Entity
{
	/// <summary>The boats being drawn, or null outside a park - read by the guests' drawing to seat a rider.</summary>
	internal static ParkBumperBoats? Current { get; private set; }

	/// <summary>What a car's seat id is looked up under in its model: the seat space, <c>0x80</c> (<c>FUN_00549c60</c>).</summary>
	private const uint SeatSpace = 0x80;

	/// <summary>The bob's scale - <c>DAT_00700ef0</c>, 0.00125 of a sine step, about a third of a unit at the peak.</summary>
	private const float BobScale = 0.00125f;

	private readonly ParkItemCatalogue? _catalogue;
	private readonly string _theme;

	/// <summary>One car's model, the clips it plays and the seat nodes it has, by seat id.</summary>
	private sealed class Boat( LobbyModel model, RideAnimations animations, IReadOnlyDictionary<int, Seat> seats, int ride )
	{
		public LobbyModel Model { get; } = model;

		public RideAnimations Animations { get; } = animations;

		public IReadOnlyDictionary<int, Seat> Seats { get; } = seats;

		public int Ride { get; } = ride;

		public int Animation { get; set; } = RideAnimations.NoRole;

		public AnimationFile? Posed { get; set; }

		/// <summary>The height it was last stood at - the car's <c>+0xa0</c>.</summary>
		public float Height { get; set; }
	}

	/// <summary>A seat node: its name, and its own transform in the car's model space (the original's axes, y up).</summary>
	private readonly record struct Seat( string Node, Matrix4x4 InModel );

	/// <summary>The boat drawn for each pool slot.</summary>
	private readonly Dictionary<int, Boat> _boats = [];

	/// <summary>The slots whose boat would not stand, not tried again until the car leaves the pool.</summary>
	private readonly HashSet<int> _failed = [];

	/// <summary>The scene's height under a boat's corner.</summary>
	private readonly ParkSceneHeight _scene;

	/// <summary>A corner's reach, <c>DAT_00700ee0</c>: a 1024th of the sine's step × 4 × the car's radius.</summary>
	private const float CornerScale = 1f / 1024f;

	public ParkBumperBoats( string themeName, ParkItemCatalogue? catalogue )
	{
		Name = "bumper boats";
		_theme = themeName.ToLowerInvariant();
		_catalogue = catalogue;
		_scene = new ParkSceneHeight( catalogue );
		Current = this;
	}

	/// <summary>The clips of the boat drawn for a pool slot, with its model's node words as they have started; null with none standing.</summary>
	internal RideAnimations? AnimationsOf( int slot ) => _boats.TryGetValue( slot, out var boat ) ? boat.Animations : null;

	/// <summary>The height the boat of a pool slot was last stood at; null with none standing.</summary>
	internal float? HeightOf( int slot ) => _boats.TryGetValue( slot, out var boat ) ? boat.Height : null;

	/// <summary>How many boats are standing - for the census.</summary>
	public int Standing => _boats.Count;

	/// <summary>
	/// Where a rider sits in the world and which of the 56 head pictures the camera sees them by (<see cref="HeadFrame"/>),
	/// or null when no boat carries them on a seat its model has.
	/// </summary>
	internal (Vector3 Seat, int Frame)? SeatOf( int peep )
	{
		if ( ParkState.Current?.TrackRides.Cars is not { } cars )
			return null;

		for ( var slot = 0; slot < ParkBumperCars.Pool; ++slot )
		{
			var car = cars.All[slot];

			if ( !car.IsLive || !_boats.TryGetValue( slot, out var boat ) )
				continue;

			foreach ( var rider in car.Riders )
			{
				if ( rider.Peep != peep )
					continue;

				if ( !boat.Seats.TryGetValue( rider.Seat, out var seat ) || !boat.Model.TryGetPlacedNode( seat.Node, out var at ) )
					return null;

				return (at, HeadFrame( TowardsCamera( Camera.Position - at, boat, seat ) ));
			}
		}

		return null;
	}

	/// <summary>
	/// Stands, moves and lets go of the boats to match the cars - after the frame's entity pass (<see cref="Level.Update"/>),
	/// because a boat stood is a model's entities added, which the pass's walk of <see cref="Entity.All"/> cannot take.
	/// </summary>
	internal void Sync()
	{
		if ( ParkState.Current is not { } state )
			return;

		var cars = state.TrackRides.Cars;
		var now = (int)(GameClock.Ticks * GameClock.TickSeconds * 1000f);

		for ( var slot = 0; slot < ParkBumperCars.Pool; ++slot )
		{
			var car = cars.All[slot];

			if ( _boats.TryGetValue( slot, out var boat ) && (!car.IsLive || boat.Ride != car.Ride) )
			{
				Drop( slot, boat );
				boat = null;
			}

			if ( !car.IsLive )
			{
				_failed.Remove( slot );
				continue;
			}

			if ( boat is null && _failed.Contains( slot ) )
				continue;

			boat ??= Stand( slot, car, state );

			if ( boat is null )
				_failed.Add( slot );

			if ( boat is null )
				continue;

			Pose( boat, car, now );
			Put( boat, car, state );
			ParkBumperCars.Landed( car );
		}
	}

	/// <summary>A car's model loaded from its ride's folder, or null where it cannot be, said once.</summary>
	private Boat? Stand( int slot, ParkBumperCars.Car car, ParkState state )
	{
		if ( RideItem( car.Ride, state ) is not { } found )
			return null;

		var (item, _) = found;

		if ( item.SupplementalMeshes is not { } meshes || car.Mesh < 0 || car.Mesh >= meshes.Count || meshes[car.Mesh] is not { } file )
		{
			Unimplemented.Report( "BUMPER_CAR_NO_MESH" );
			return null;
		}

		var stem = Path.GetFileNameWithoutExtension( file );
		var path = $"{item.Directory}/{stem}.MD2";

		try
		{
			var animations = RideAnimations.Load( item.Directory, stem, FileSystem );
			var model = new LobbyModel( path, $"{item.Directory}/textures", Vector3.Zero,
				sharedTextureDirectory: $"levels/{_theme}/sharetex",
				clips: animations.AllClips );

			var boat = new Boat( model, animations, SeatsOf( path, out var read ), car.Ride );

			// The model's node words, kept as its clips start, for the park file's record of it.
			if ( read != null )
				animations.Nodes = new ParkModelTables.Running( read, 1 );

			_boats[slot] = boat;

			// Counted once a boat: the rocking its corners' bob gives it, and its wake.
			if ( (car.Flags & ParkBumperCars.CarFlags.Bobs) != 0 )
				Unimplemented.Report( "BUMPER_CAR_ROCK" );

			if ( (car.Flags & ParkBumperCars.CarFlags.Wake) != 0 )
				Unimplemented.Report( "BUMPER_CAR_WAKE" );

			// The roll the original gives each rider's head quad to match its seat node (ParkGuestSprites.DrawHead).
			if ( boat.Seats.Count > 0 )
				Unimplemented.Report( "RIDER_HEAD_ROLL" );

			Log.Info( $"Bumper boats: car {slot} of ride 0x{car.Ride:x} stands as {path}, seats {string.Join( ",", boat.Seats.Keys )}" );

			return boat;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Bumper boats: {path} would not load, so car {slot} is not drawn - {e.Message}" );
			Unimplemented.Report( "BUMPER_CAR_NO_MESH" );
			return null;
		}
	}

	/// <summary>
	/// The camera's direction seen from a seat node, in that node's own frame and the model's axes (y up): the world
	/// direction turned back by the boat's heading, swapped from this map's z-up into the model's y-up, and turned back by
	/// the node's own rotation - the inverse of the node, then the camera, that <c>FUN_0044b510</c> composes.
	/// </summary>
	private static System.Numerics.Vector3 TowardsCamera( Vector3 world, Boat boat, Seat seat )
	{
		var unturned = System.Numerics.Vector3.Transform( world.GetSystemVector3(), Quaternion.Inverse( boat.Model.PlacedRotation ) );
		var model = new System.Numerics.Vector3( unturned.X, unturned.Z, unturned.Y );

		var rotation = seat.InModel with { M41 = 0, M42 = 0, M43 = 0 };

		return Matrix4x4.Invert( rotation, out var inverse ) ? System.Numerics.Vector3.TransformNormal( model, inverse ) : model;
	}

	/// <summary>
	/// Which head picture a node shows the camera - <c>FUN_0044b510</c>: of the 56 directions it walks, rows 0 to 6 tilting
	/// 30° about x from the node's own up (<c>0x006fe2c4</c>) and columns 0 to 7 turning −45° about up (<c>0x006fe2bc</c>),
	/// the first that lies closest to the camera's direction (strictly, from nought); straight above or below keeps column
	/// 0. The picture is column + 8 × row: a head bank's 56 run eight headings to a row, from above to below.
	/// </summary>
	internal static int HeadFrame( System.Numerics.Vector3 towardsCamera )
	{
		var length = towardsCamera.Length();

		if ( length <= 0f )
			return 0;

		var d = towardsCamera / length;
		float best = 0f;
		int row = 0, column = 0;

		for ( var r = 0; r < 7; ++r )
		{
			var tilt = r * (MathF.PI / 6f);

			for ( var c = 0; c < 8; ++c )
			{
				var turn = c * -(MathF.PI / 4f);
				var candidate = new System.Numerics.Vector3( MathF.Sin( tilt ) * MathF.Sin( turn ), MathF.Cos( tilt ), MathF.Sin( tilt ) * MathF.Cos( turn ) );
				var dot = System.Numerics.Vector3.Dot( d, candidate );

				if ( dot > best )
				{
					best = dot;
					row = r;
					column = c;
				}
			}
		}

		if ( row is 0 or 6 )
			column = 0;

		return column + (8 * row);
	}

	/// <summary>The seat nodes of a car's model, by id: every node in the seat space, <c>FUN_0044b220( model, 0x80, id )</c>.</summary>
	private IReadOnlyDictionary<int, Seat> SeatsOf( string path, out ModelFile? file )
	{
		var seats = new Dictionary<int, Seat>();

		file = null;

		using var stream = FileSystem.OpenRead( path );

		if ( stream is null )
			return seats;

		file = new ModelFile( stream );

		for ( var id = 1; id < 16; ++id )
		{
			var node = file.FindNode( id, SeatSpace );

			if ( node >= 0 )
				seats[id] = new Seat( file.Nodes[node].Name, file.Nodes[node].WorldTransform );
		}

		return seats;
	}

	/// <summary>The clip a car plays, as its <c>+0x10</c> says: role 5 looped (<c>FUN_004732a0( 5, 0, 1 )</c>), or stopped.</summary>
	private static void Pose( Boat boat, ParkBumperCars.Car car, int now )
	{
		if ( car.Animation != boat.Animation )
		{
			boat.Animation = car.Animation;

			if ( car.Animation == RideAnimations.NoRole )
			{
				boat.Animations.Trigger( RideAnimations.NoRole, 0, AnimTimeControl.StartAtOnceFlag, 1f, now );

				if ( boat.Posed is { } posed )
					boat.Model.Rest( posed );

				boat.Posed = null;
			}
			else
			{
				boat.Animations.Trigger( car.Animation, 0, AnimTimeControl.LoopFlag | AnimTimeControl.StartAtOnceFlag, 1f, now );
			}
		}

		boat.Animations.Advance( now );

		if ( boat.Animations.Channel( 0 ) is not { IsIdle: false } channel
			|| boat.Animations.Clip( channel.AnimID, channel.SubAnim ) is not { } clip )
			return;

		boat.Posed = clip;
		boat.Model.Pose( clip, channel.AnimFrame );
	}

	/// <summary>
	/// Stands a boat where its car floats - <c>FUN_00546280</c>: the position at 1/307.2 of a unit, the heading as
	/// <c>((heading - 0x100) &amp; 0x1ff)</c> 512ths of a turn, and the height the average of its four corners', each
	/// the scene's under it (<see cref="ParkSceneHeight"/>) plus its bob, eased back from this tick's toward the last by the part
	/// of a tick not yet come: the park passes <c>(now - stepped) / 31</c>, between -1 and 0, since its catch-up loop
	/// steps past now (<c>0x0054fa0d</c>).
	/// </summary>
	private void Put( Boat boat, ParkBumperCars.Car car, ParkState state )
	{
		var field = ParkGround.Current?.Heightfield;
		var cellX = field?.CellSizeX ?? 10f;
		var cellY = field?.CellSizeY ?? 10f;
		var ease = Math.Clamp( GameClock.PartialTick, 0f, 1f ) - 1f;

		var x = (car.X + car.VelocityX * ease) / ParkBumperCars.CellUnits * cellX;
		var y = (car.Z + car.VelocityZ * ease) / ParkBumperCars.CellUnits * cellY;

		// The original passes this as the third of the angles it turns the model by, its heading eased back by its turn,
		// truncated to a whole 512th (0x00546409..0x00546439).
		var heading = car.Heading - (int)(car.Turn * -ease);

		var radius = state.TrackRides.Cars.RideOf( car.Ride )?.CarRadius ?? car.Radius;
		var last = boat.Height;

		var height = Float( x, y, heading, radius, cellX, cellY, ( cornerX, cornerY ) => _scene.Under( cornerX, cornerY, state, field, last ) );

		// Each corner bobs on its own multiple of the phase, 6, 4, 3 and 5 (0x0054658c..0x005466c0); the height is
		// the four's average, so a quarter of their bobs.
		if ( (car.Flags & ParkBumperCars.CarFlags.Bobs) != 0 )
			height += (Bob( car.Phase, 6, ease ) + Bob( car.Phase, 4, ease ) + Bob( car.Phase, 3, ease ) + Bob( car.Phase, 5, ease )) * BobScale * 0.25f;

		var yaw = ((heading - 0x100) & 0x1ff) * (MathF.Tau / 512f);

		boat.Height = height;
		boat.Model.SetTransform( new Vector3( x, y, height ), Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -yaw ) );
	}

	/// <summary>
	/// The height a car stands at before its bob: the average of the scene's under its four corners, which stand the
	/// ride's car radius from its middle, ahead, behind and to each side (<c>0x0054643f</c>..<c>0x005464e6</c>).
	/// </summary>
	internal static float Float( float x, float y, int heading, int radius, float cellX, float cellY, Func<float, float, float> under )
	{
		var ahead = (ParkBumperCars.Sine( heading ) << 2) * (float)radius * CornerScale / ParkBumperCars.CellUnits;
		var aside = (ParkBumperCars.Sine( heading + 0x80 ) << 2) * (float)radius * CornerScale / ParkBumperCars.CellUnits;

		return (under( x + ahead * cellX, y + aside * cellY )
			+ under( x - ahead * cellX, y - aside * cellY )
			+ under( x - aside * cellX, y + ahead * cellY )
			+ under( x + aside * cellX, y - ahead * cellY )) * 0.25f;
	}

	/// <summary>One corner's bob, eased back toward the last tick's: <c>s[p×m] + (s[p×m] - s[p×m - m]) × ease</c>.</summary>
	private static float Bob( int phase, int multiple, float ease )
	{
		var now = ParkBumperCars.Sine( phase * multiple );

		return now + (now - ParkBumperCars.Sine( phase * multiple - multiple )) * ease;
	}

	/// <summary>The catalogue item and the placed thing whose track handle is this one.</summary>
	private (ParkItemCatalogue.Item Item, ParkWorld.CatalogueObject Placed)? RideItem( int handle, ParkState state )
	{
		if ( _catalogue is null )
			return null;

		foreach ( var placed in state.Objects )
		{
			if ( placed.TrackRide == handle && _catalogue.TryGet( placed.CatalogueId, out var item ) )
				return (item, placed);
		}

		return null;
	}

	private void Drop( int slot, Boat boat )
	{
		foreach ( var entity in boat.Model.Entities )
			entity.Delete();

		_boats.Remove( slot );
	}

	protected override void OnDelete()
	{
		foreach ( var (slot, boat) in _boats.ToArray() )
			Drop( slot, boat );

		if ( Current == this )
			Current = null;
	}
}
