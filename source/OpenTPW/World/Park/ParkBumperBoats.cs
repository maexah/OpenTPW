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
/// <b>Two departures, said here.</b> The height is the top of the ride's own water mesh, where the original asks the
/// scene for the surface under each of four points (not decoded; <c>docs/exe/park.md</c>, the same section): the Hot
/// Pot's <c>water01</c> is flat to 0.2 units, so the two agree there. And the rocking the original works out from the
/// four points' bob is not drawn (<c>BUMPER_CAR_ROCK</c>), nor the wake under a boat (<c>BUMPER_CAR_WAKE</c>).
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
	private sealed class Boat( LobbyModel model, RideAnimations animations, IReadOnlyDictionary<int, string> seats, int ride )
	{
		public LobbyModel Model { get; } = model;

		public RideAnimations Animations { get; } = animations;

		public IReadOnlyDictionary<int, string> Seats { get; } = seats;

		public int Ride { get; } = ride;

		public int Animation { get; set; } = RideAnimations.NoRole;

		public AnimationFile? Posed { get; set; }
	}

	/// <summary>The boat drawn for each pool slot.</summary>
	private readonly Dictionary<int, Boat> _boats = [];

	/// <summary>The slots whose boat would not stand, not tried again until the car leaves the pool.</summary>
	private readonly HashSet<int> _failed = [];

	/// <summary>Each ride's water height above its own origin, by handle, read once.</summary>
	private readonly Dictionary<int, float?> _water = [];

	public ParkBumperBoats( string themeName, ParkItemCatalogue? catalogue )
	{
		Name = "bumper boats";
		_theme = themeName.ToLowerInvariant();
		_catalogue = catalogue;
		Current = this;
	}

	/// <summary>How many boats are standing - for the census.</summary>
	public int Standing => _boats.Count;

	/// <summary>
	/// Where a rider sits in the world and the way their boat points (in a person's 2048ths of a turn), or null when no
	/// boat carries them on a seat its model has.
	/// </summary>
	internal (Vector3 Seat, int Angle)? SeatOf( int peep )
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

				return boat.Seats.TryGetValue( rider.Seat, out var node ) && boat.Model.TryGetPlacedNode( node, out var at )
					? (at, (car.Heading * 4) & 0x7ff)
					: null;
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

			var boat = new Boat( model, animations, SeatsOf( path ), car.Ride );
			_boats[slot] = boat;

			// Counted once a boat: the rocking its corners' bob gives it, and its wake.
			if ( (car.Flags & ParkBumperCars.CarFlags.Bobs) != 0 )
				Unimplemented.Report( "BUMPER_CAR_ROCK" );

			if ( (car.Flags & ParkBumperCars.CarFlags.Wake) != 0 )
				Unimplemented.Report( "BUMPER_CAR_WAKE" );

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

	/// <summary>The seat nodes of a car's model, by id: every node in the seat space, <c>FUN_0044b220( model, 0x80, id )</c>.</summary>
	private IReadOnlyDictionary<int, string> SeatsOf( string path )
	{
		var seats = new Dictionary<int, string>();

		using var stream = FileSystem.OpenRead( path );

		if ( stream is null )
			return seats;

		var file = new ModelFile( stream );

		for ( var id = 1; id < 16; ++id )
		{
			var node = file.FindNode( id, SeatSpace );

			if ( node >= 0 )
				seats[id] = file.Nodes[node].Name;
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
	/// <c>((heading - 0x100) &amp; 0x1ff)</c> 512ths of a turn, and the four corners' bob averaged.
	/// </summary>
	private void Put( Boat boat, ParkBumperCars.Car car, ParkState state )
	{
		var field = ParkGround.Current?.Heightfield;
		var cellX = field?.CellSizeX ?? 10f;
		var cellY = field?.CellSizeY ?? 10f;

		var x = car.X / (float)ParkBumperCars.CellUnits * cellX;
		var y = car.Z / (float)ParkBumperCars.CellUnits * cellY;

		var height = WaterUnder( car.Ride, state ) ?? field?.HeightAtWorld( x, y ) ?? 0f;

		// Each corner bobs on its own multiple of the phase, 6, 4, 3 and 5 (0x0054658c..0x005466c0); their average is
		// the height. The original eases each between this tick's step and the last by the frame's fraction; this
		// takes the tick's own.
		if ( (car.Flags & ParkBumperCars.CarFlags.Bobs) != 0 )
		{
			var bob = ParkBumperCars.Sine( car.Phase * 6 ) + ParkBumperCars.Sine( car.Phase * 4 )
				+ ParkBumperCars.Sine( car.Phase * 3 ) + ParkBumperCars.Sine( car.Phase * 5 );

			height += bob * BobScale * 0.25f;
		}

		// The original passes this as the third of the angles it turns the model by. Which way round it reads on this
		// map is not pinned: a random heading looks the same either way, and Q179c's motion settles it.
		var yaw = ((car.Heading - 0x100) & 0x1ff) * (MathF.Tau / 512f);

		boat.Model.SetTransform( new Vector3( x, y, height ), Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -yaw ) );
	}

	/// <summary>
	/// The height of a ride's water in the world: its object's origin plus the top of the mesh its model names
	/// <c>water</c>, or null with none, which is counted and floats the boat on the ground.
	/// </summary>
	private float? WaterUnder( int handle, ParkState state )
	{
		if ( RideItem( handle, state ) is not { } found )
			return null;

		var (item, placed) = found;

		if ( !_water.TryGetValue( handle, out var top ) )
		{
			top = WaterTop( item );
			_water[handle] = top;

			if ( top is null )
				Unimplemented.Report( "BUMPER_RIDE_NO_WATER_MESH" );
		}

		return top is { } above ? ParkObjects.OriginFor( placed.CellX, placed.CellY, placed.Angle ).Z + above : null;
	}

	private float? WaterTop( ParkItemCatalogue.Item item )
	{
		using var stream = FileSystem.OpenRead( $"{item.Directory}/{item.Stem}.MD2" );

		if ( stream is null )
			return null;

		var file = new ModelFile( stream );
		float? top = null;

		foreach ( var mesh in file.Meshes )
		{
			if ( mesh.Name is null || !mesh.Name.StartsWith( "water", StringComparison.OrdinalIgnoreCase ) || mesh.Vertices is null )
				continue;

			foreach ( var vertex in mesh.Vertices )
			{
				var at = System.Numerics.Vector3.Transform(
					new System.Numerics.Vector3( vertex.Position.X, vertex.Position.Y, vertex.Position.Z ), mesh.WorldTransform );

				top = top is { } high ? MathF.Max( high, at.Y ) : at.Y;
			}
		}

		return top;
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
