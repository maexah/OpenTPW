namespace OpenTPW;

/// <summary>
/// The bumper family's half of the track-ride record and its cars - the Hot Pot's boats, and the four other
/// themes' bumper rides (BumperType -1, -3, -6, -11 and -14). <c>docs/exe/park.md</c>, "How a bumper ride ends a go,
/// and lets its riders off" and "Where a bumper ride's cars float".
///
/// <para>
/// <b>One record per slot of <see cref="ParkTrackRideTable"/></b>, the same <c>0xd0</c> entry the original keeps there:
/// its state, wear, duration and the two lists of peeps a <c>BUMP</c> fills and empties; and <b>one pool of 256 cars</b>
/// shared by every ride (<c>DAT_00877b68</c>), each with its riders, its timer and where it floats. The script reaches
/// both through <see cref="RideScript"/>'s <c>BUMP</c>, and the track tick (<see cref="Tick"/>) ends a go.
/// </para>
/// <para>
/// <b>Not built, and counted where it is reached:</b> the cars' motion and their bumping (Q179c), so a car stays
/// where it was launched; the go-karts' and the water ride's arms, whose scripts keep <c>BUMP</c> a counted no-op;
/// the cars' sounds, smoke and splashes; and the performance a ride opens at.
/// </para>
/// </summary>
public sealed class ParkBumperCars
{
	/// <summary>How many cars every ride shares - the pool of <c>0xac00</c> bytes, <c>0xac</c> a car (<c>FUN_00544360</c>).</summary>
	public const int Pool = 256;

	/// <summary>How many list nodes every ride shares - <c>0x5000</c> bytes, <c>0x14</c> a node (<c>FUN_00544360</c>).</summary>
	public const int ListNodes = 1024;

	/// <summary>The most cars any one ride launches, whatever its template says - <c>FUN_00549db0</c>'s <c>0x40</c>.</summary>
	public const int MostCarsAnyRide = 64;

	/// <summary>What <c>BUMP 13</c> scales a duration by: track ticks of 31 ms (<c>0x0055496e</c>).</summary>
	public const int TicksPerDurationUnit = 30;

	/// <summary>One map cell in the record's fixed point - 0xc00, so 307.2 a world unit at ten units a cell.</summary>
	public const int CellUnits = 0xc00;

	/// <summary>A car's flag bits, <c>+0x00</c>.</summary>
	[Flags]
	public enum CarFlags
	{
		None = 0,
		Live = 0x1,
		Unloading = 0x20,
		Active = 0x4000,
		Seating = 0x80000,
		New = 0x100000,
		Lead = 0x400000,
		Bobs = 0x1000000,
		Wake = 0x2000000,
		Launched = 0x4000000
	}

	/// <summary>A ride's state, <c>+0x50</c>.</summary>
	public enum RideState
	{
		Closed = 0,
		Loading = 1,
		Running = 2
	}

	/// <summary>One peep on a list or a car, with the seat <c>FUN_00549c60</c> gave them (0 none yet).</summary>
	public sealed record Rider( int Peep, int Seat );

	/// <summary>A bumper ride's record - the fields of the <c>0xd0</c> entry the bumper family uses.</summary>
	public sealed class Ride
	{
		public int BumperType { get; init; }

		/// <summary><c>+0x04</c>: what a car's timer is set to, in track ticks.</summary>
		public int Duration { get; internal set; }

		/// <summary><c>+0x08</c>: a car's radius, in the record's fixed point.</summary>
		public int CarRadius { get; init; }

		/// <summary><c>+0x14</c> and <c>+0x18</c>: the first supplemental mesh a car is, and how many it cycles through.</summary>
		public int MeshBase { get; init; }

		public int MeshCount { get; init; }

		/// <summary><c>+0x50</c>.</summary>
		public RideState State { get; internal set; }

		/// <summary><c>+0x54</c>: 0 sound, 1 worn, 2 broken.</summary>
		public int Wear { get; internal set; }

		/// <summary><c>+0x58</c> not nought: one of its cars leads.</summary>
		public bool HasLead { get; internal set; }

		/// <summary><c>+0x5c</c>: how many cars it has.</summary>
		public int Cars { get; internal set; }

		/// <summary><c>+0x60</c>: how many riders sit in its cars.</summary>
		public int Seated { get; internal set; }

		/// <summary><c>+0x64</c>: how many cars its template allows.</summary>
		public int MostCars { get; init; }

		/// <summary><c>+0x74</c>, <c>+0x78</c>: the arena's centre, in the record's fixed point.</summary>
		public int CentreX { get; internal set; }

		public int CentreZ { get; internal set; }

		/// <summary>The radius of the arena a new car is put down in - the collision object at <c>+0xc0</c>, its <c>+0x0c</c>.</summary>
		public int ArenaRadius { get; init; }

		/// <summary><c>+0xc4</c>: who boards the next car, head first.</summary>
		public List<int> Boarding { get; } = [];

		/// <summary><c>+0xc8</c>: who has come off and waits for <c>BUMP 2</c>, head first.</summary>
		public List<int> Leaving { get; } = [];
	}

	/// <summary>One car of the pool.</summary>
	public sealed class Car
	{
		public CarFlags Flags { get; internal set; }

		/// <summary>The handle of the ride it belongs to (its record, <c>+0x9c</c>).</summary>
		public int Ride { get; internal set; }

		/// <summary><c>+0x04</c>: which supplemental mesh of its ride it is drawn as.</summary>
		public int Mesh { get; internal set; }

		/// <summary><c>+0x10</c>: the clip its model plays - role 5 in a go, <see cref="RideAnimations.NoRole"/> at rest.</summary>
		public int Animation { get; internal set; } = RideAnimations.NoRole;

		/// <summary><c>+0x30</c>: its riders, head first.</summary>
		public List<Rider> Riders { get; } = [];

		/// <summary><c>+0x34</c>, <c>+0x38</c>: where it floats, in the record's fixed point.</summary>
		public int X { get; internal set; }

		public int Z { get; internal set; }

		/// <summary><c>+0x54</c>: which way it points, in 512ths of a turn.</summary>
		public int Heading { get; internal set; }

		/// <summary><c>+0x64</c>: its radius, copied from its ride.</summary>
		public int Radius { get; internal set; }

		/// <summary><c>+0x88</c>: the ticks left in its go; below nought it never counts again.</summary>
		public int Timer { get; internal set; }

		/// <summary><c>+0x90</c>: counted up every track tick, the phase its bob is drawn at.</summary>
		public int Phase { get; internal set; }

		/// <summary><c>+0x2c</c> not -1: smoke rising from its emitter while the ride is broken.</summary>
		public bool Smoking { get; internal set; }

		public bool IsLive => (Flags & CarFlags.Live) != 0;
	}

	private readonly ParkTrackRideTable _table;
	private readonly Ride?[] _rides = new Ride?[ParkTrackRideTable.Slots];
	private readonly Car[] _cars = new Car[Pool];

	/// <summary>How many nodes are free. A freed ride's lists are not given back, as the original's are not.</summary>
	private int _freeNodes = ListNodes;

	/// <summary>
	/// The draws a new car's spot and heading take. The original draws the world's one generator
	/// (<c>FUN_00516330</c>); this keeps its own state, the departure <see cref="ParkGenerator"/> describes.
	/// </summary>
	private uint _random;

	/// <summary>Counted up every track tick - <c>DAT_00877b5c</c>.</summary>
	public int Ticks { get; private set; }

	internal ParkBumperCars( ParkTrackRideTable table, uint seed = 0x1234567 )
	{
		_table = table;
		_random = seed;

		for ( var i = 0; i < Pool; ++i )
			_cars[i] = new Car();
	}

	/// <summary>Whether the BumperType is one of the family this builds.</summary>
	public static bool IsBumperFamily( int bumperType ) => bumperType is -1 or -3 or -6 or -11 or -14;

	/// <summary>The ride a handle names, or null for a stale handle or one of another family.</summary>
	public Ride? RideOf( int handle ) => _table.Holds( handle ) ? _rides[handle & 0xff] : null;

	/// <summary>Every car in the pool, live or not, in pool order.</summary>
	public IReadOnlyList<Car> All => _cars;

	/// <summary>The live cars of one ride, in pool order.</summary>
	public IEnumerable<Car> CarsOf( int handle )
	{
		foreach ( var car in _cars )
		{
			if ( car.IsLive && car.Ride == handle )
				yield return car;
		}
	}

	/// <summary>
	/// A new record from its BumperType's template - <c>FUN_00545890</c>'s copy of <c>0x764178</c> and its bumper arm.
	/// Only the Hot Pot's template is read here; another bumper type takes a slot with no record, counted.
	/// </summary>
	internal void Open( int handle, int bumperType )
	{
		var slot = handle & 0xff;
		_rides[slot] = null;

		if ( !IsBumperFamily( bumperType ) )
			return;

		if ( bumperType != -1 )
		{
			Unimplemented.Report( $"BUMPER_TEMPLATE_{-bumperType}" );
			return;
		}

		// The Hot Pot's template at 0x764178: duration 4350, car radius 768, mesh 1 of 1, eight cars at most;
		// its bumper arm lays the arena 0x1200 in radius (FUN_00545890, 0x00545a0b) and sets state 0.
		_rides[slot] = new Ride
		{
			BumperType = bumperType,
			Duration = 4350,
			CarRadius = 768,
			MeshBase = 1,
			MeshCount = 1,
			MostCars = 8,
			ArenaRadius = 0x1200,
			State = RideState.Closed
		};
	}

	/// <summary>
	/// Where a placed bumper ride's arena is centred, in the record's fixed point - <c>FUN_00529e10</c>: the anchor cell,
	/// the item's adjust for the turn, the placer's own offset for it, and half a cell (<c>FUN_00545890</c>).
	/// </summary>
	public static (int X, int Z) ArenaCentre( ParkItemCatalogue.Item item, int cellX, int cellY, int angle )
	{
		var (adjustX, adjustY) = item.BumperAdjustAt( angle );

		// The placer's second switch on the turn (0x00529edf..0x00529f2b).
		var (fixedX, fixedY) = angle switch
		{
			0 => (4, 1),
			90 => (1, -5),
			180 => (-5, -2),
			270 => (-2, 4),
			_ => (0, 0)
		};

		return ((cellX + adjustX + fixedX) * CellUnits + CellUnits / 2, (cellY + adjustY + fixedY) * CellUnits + CellUnits / 2);
	}

	/// <summary>Puts a ride's arena where its object stands.</summary>
	internal void Place( int handle, int x, int z )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		ride.CentreX = x;
		ride.CentreZ = z;
	}

	/// <summary>
	/// A ride let go of - <c>FUN_00545610</c>: every car removed, the ride closed, the record gone. The nodes left on
	/// its lists are not given back, as the original's are not.
	/// </summary>
	internal void Close( int handle )
	{
		if ( RideOf( handle ) is null )
			return;

		foreach ( var car in _cars )
		{
			if ( car.IsLive && car.Ride == handle )
				Remove( car );
		}

		Shut( handle );

		_rides[handle & 0xff] = null;
	}

	/// <summary><c>BUMP 1</c> - <c>FUN_0054aa80</c>: the peep onto the head of the boarding list, unless closed or out of nodes.</summary>
	public bool Board( int handle, int peep )
	{
		if ( RideOf( handle ) is not { } ride || ride.State == RideState.Closed || _freeNodes == 0 )
			return false;

		--_freeNodes;
		ride.Boarding.Insert( 0, peep );

		Log.Info( $"Bumper: peep {peep} added to next car to be loaded on ride 0x{handle:x}" );

		return true;
	}

	/// <summary><c>BUMP 2</c> - <c>FUN_0054ab40</c>: the head of the leaving list, or nought.</summary>
	public int TakeLeaving( int handle )
	{
		if ( RideOf( handle ) is not { } ride || ride.Leaving.Count == 0 )
			return 0;

		var peep = ride.Leaving[0];
		ride.Leaving.RemoveAt( 0 );
		++_freeNodes;

		return peep;
	}

	/// <summary>
	/// <c>BUMP 3</c> - <c>FUN_00544f90</c>: from loading, the go: every car timed and retargeted, and a Hot Pot car
	/// with riders set rocking (role 5).
	/// </summary>
	public void Start( int handle )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		if ( ride.State == RideState.Loading )
		{
			ride.State = RideState.Running;

			foreach ( var car in _cars )
			{
				if ( !car.IsLive || car.Ride != handle )
					continue;

				car.Flags &= ~(CarFlags.Seating | (CarFlags)0x40);
				car.Timer = ride.Duration;

				Retarget( ride, car );

				if ( ride.BumperType == -1 && (car.Flags & CarFlags.Active) != 0 )
					car.Animation = 5;
			}
		}

		Log.Info( $"Bumper: Start Bump Ride 0x{handle:x}, {ride.Duration} ticks, at track tick {Ticks}" );
	}

	/// <summary>
	/// <c>BUMP 4</c> - <c>FUN_00549db0( h, 0 )</c>: a car from the pool, taking the whole boarding list. Null when the
	/// ride is closed or full, or the pool is empty.
	/// </summary>
	public Car? Launch( int handle )
	{
		if ( RideOf( handle ) is not { } ride || ride.Cars >= ride.MostCars || ride.Cars >= MostCarsAnyRide
			|| ride.State == RideState.Closed )
			return null;

		var car = Array.Find( _cars, candidate => !candidate.IsLive );

		if ( car is null )
			return null;

		car.Riders.Clear();
		car.Flags = CarFlags.Live | CarFlags.Active;
		car.Ride = handle;
		car.X = ride.CentreX;
		car.Z = ride.CentreZ;
		car.Radius = ride.CarRadius;
		car.Heading = 0;
		car.Phase = 0;
		car.Smoking = false;

		++ride.Cars;
		car.Timer = ride.Duration;

		car.Riders.AddRange( ride.Boarding.Select( peep => new Rider( peep, 0 ) ) );
		ride.Boarding.Clear();

		car.Flags |= CarFlags.New;
		car.Mesh = ride.Cars % Math.Max( 1, ride.MeshCount ) + ride.MeshBase;
		car.Animation = RideAnimations.NoRole;

		if ( !ride.HasLead )
		{
			ride.HasLead = true;
			car.Flags |= CarFlags.Lead;
		}

		Seat( ride, car, particles: false );
		Retarget( ride, car );

		// The launcher's last word: a splash where it lands, spawned at the next draw (0x0054a01d, FUN_00546280).
		car.Flags |= CarFlags.Launched;
		Unimplemented.Report( "BUMPER_CAR_SPLASH" );

		return car;
	}

	/// <summary><c>BUMP 6</c> - <c>FUN_00544a10</c>: unless closed, closed; the boarding list onto the leaving list's tail, every car to unload.</summary>
	public void Shut( int handle )
	{
		if ( RideOf( handle ) is not { } ride || ride.State == RideState.Closed )
			return;

		ride.State = RideState.Closed;
		Log.Info( $"Bumper: Close ride 0x{handle:x}" );

		ride.Leaving.AddRange( ride.Boarding );
		ride.Boarding.Clear();

		foreach ( var car in _cars )
		{
			if ( !car.IsLive || car.Ride != handle )
				continue;

			car.Timer = 0;
			car.Flags = (car.Flags & ~(CarFlags)0x405e) | CarFlags.Unloading | (CarFlags)0x10000;
		}
	}

	/// <summary><c>BUMP 7</c> - <c>FUN_00544840</c>: the bumper family loading, whatever it was.</summary>
	public void OpenForLoading( int handle )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		Log.Info( $"Bumper: Open ride 0x{handle:x}" );

		ride.State = RideState.Loading;

		// FUN_00545180 sets +0x1c from the template's performance; nothing here reads it.
		Unimplemented.Report( "BUMPER_RIDE_PERFORMANCE" );
	}

	/// <summary><c>BUMP 8</c> with a value - <c>FUN_00544c80</c>: broken, smoke at each running car, a Hot Pot car at rest.</summary>
	public void Break( int handle )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		ride.Wear = 2;
		Log.Info( "Bumper: Ride broken" );

		foreach ( var car in _cars )
		{
			if ( !car.IsLive || car.Ride != handle || (car.Flags & CarFlags.Active) == 0 || car.Smoking )
				continue;

			// The b_car's emitter, 0x100 id 2, is Head03: there is always one to smoke from.
			car.Smoking = true;
			Unimplemented.Report( "BUMPER_CAR_SMOKE" );

			if ( ride.BumperType == -1 )
				car.Animation = RideAnimations.NoRole;
		}
	}

	/// <summary><c>BUMP 9</c> with a value - <c>FUN_00544c00</c>: worn.</summary>
	public void Wear( int handle )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		ride.Wear = 1;
		Log.Info( "Bumper: Ride worn out" );
	}

	/// <summary><c>BUMP 8</c> or <c>9</c> with nought - <c>FUN_00544e50</c>: sound, and only from broken the smoke out and the cars rocking again.</summary>
	public void Fix( int handle )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		var was = ride.Wear;
		ride.Wear = 0;

		if ( was != 2 )
			return;

		Log.Info( "Bumper: Ride Fixed" );

		foreach ( var car in _cars )
		{
			if ( !car.IsLive || car.Ride != handle || !car.Smoking )
				continue;

			car.Smoking = false;

			if ( ride.BumperType == -1 && (car.Flags & CarFlags.Active) != 0 )
				car.Animation = 5;
		}
	}

	/// <summary><c>BUMP 10</c> - <c>FUN_00544b50</c>: every car removed, then closed.</summary>
	public void Empty( int handle )
	{
		if ( RideOf( handle ) is null )
			return;

		foreach ( var car in _cars )
		{
			if ( car.IsLive && car.Ride == handle )
				Remove( car );
		}

		Shut( handle );
	}

	/// <summary><c>BUMP 11</c> - <c>FUN_005452a0</c>: how many cars.</summary>
	public int CarCount( int handle ) => RideOf( handle )?.Cars ?? 0;

	/// <summary>
	/// <c>BUMP 12</c> - <c>FUN_00549b80</c>: unless closed, the first car with no riders takes the boarding list, timed
	/// and seated. A car still unloading is not skipped, and its retarget clears the unload.
	/// </summary>
	public bool Fill( int handle )
	{
		if ( RideOf( handle ) is not { } ride || ride.State == RideState.Closed )
			return false;

		var car = Array.Find( _cars, candidate => candidate.IsLive && candidate.Ride == handle && candidate.Riders.Count == 0 );

		if ( car is null )
			return false;

		car.Riders.AddRange( ride.Boarding.Select( peep => new Rider( peep, 0 ) ) );
		ride.Boarding.Clear();

		car.Flags |= CarFlags.Seating | CarFlags.Active;
		car.Timer = ride.Duration;

		Seat( ride, car, particles: true );
		Retarget( ride, car );

		return true;
	}

	/// <summary><c>BUMP 13</c> and <c>14</c> - <c>FUN_00545100</c>: the duration, as given.</summary>
	public void SetDuration( int handle, int ticks )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		ride.Duration = ticks;
		Log.Info( $"Bumper: Set Ride Duration to {ticks}" );
	}

	/// <summary>
	/// <c>BUMP 16</c> - <c>FUN_0054ad90( h, 1 )</c>: the first car with no riders removed, answering 1; with none, the
	/// first car whatever it carries, answering <b>nought</b>, as the original's does.
	/// </summary>
	public int RemoveACar( int handle )
	{
		if ( RideOf( handle ) is null )
			return 0;

		if ( Array.Find( _cars, car => car.IsLive && car.Ride == handle && car.Riders.Count == 0 ) is { } empty )
		{
			Remove( empty );
			return 1;
		}

		if ( Array.Find( _cars, car => car.IsLive && car.Ride == handle ) is { } any )
			Remove( any );

		return 0;
	}

	/// <summary>
	/// The track tick - <c>FUN_00546c80</c>, once per 31 ms step before the scripts: each live car's timer, then its
	/// step, counted.
	/// </summary>
	public void Tick()
	{
		++Ticks;

		foreach ( var car in _cars )
		{
			if ( !car.IsLive )
				continue;

			Count( car );

			if ( car.IsLive && RideOf( car.Ride ) is { State: RideState.Running } )
				Unimplemented.Report( "BUMPER_CAR_MOTION" );
		}

		// The pairwise pass that bumps cars apart is Q179c's.
	}

	/// <summary>
	/// <c>FUN_005474b0</c>'s bumper arms: the phase counted, the timer counted down while the ride runs unbroken, and at
	/// nought the car unloads; an unloading car hands its riders to the leaving list, and only when no rider is seated
	/// anywhere on the ride does it go back to loading.
	/// </summary>
	private void Count( Car car )
	{
		++car.Phase;

		if ( RideOf( car.Ride ) is not { } ride )
			return;

		if ( car.Timer >= 0 && ride.State == RideState.Running && ride.Wear != 2 )
		{
			--car.Timer;

			if ( car.Timer == 0 )
			{
				car.Flags &= ~CarFlags.Active;
				car.Flags = (car.Flags & ~(CarFlags)0x5e) | CarFlags.Unloading | (CarFlags)0x10000;
			}
		}

		if ( (car.Flags & CarFlags.Unloading) == 0 )
			return;

		if ( Unload( ride, car, particles: true ) != 0 )
			return;

		ride.State = RideState.Loading;
		car.Flags &= ~(CarFlags)0x7e;
		Log.Info( $"Bumper: ride 0x{car.Ride:x} empty and loading again at track tick {Ticks}" );
		car.Animation = RideAnimations.NoRole;
	}

	/// <summary>
	/// <c>FUN_0054ac70</c>: each rider of the car onto the head of the leaving list, so their order reverses, the seated
	/// count down one each; answers the ride-wide seated count.
	/// </summary>
	private static int Unload( Ride ride, Car car, bool particles )
	{
		foreach ( var rider in car.Riders )
		{
			ride.Leaving.Insert( 0, rider.Peep );
			--ride.Seated;

			if ( particles && rider.Seat != 0 )
				Unimplemented.Report( "BUMPER_RIDER_PARTICLE" );

			Log.Info( $"Bumper: peep {rider.Peep} added to ride 0x{car.Ride:x} leaving list" );
		}

		car.Riders.Clear();

		return ride.Seated;
	}

	/// <summary>
	/// <c>FUN_00549c60</c>: each rider of the car given a seat, 1 upwards from the head, and counted seated. Whether the
	/// car's model has a seat of that id is the drawing's question.
	/// </summary>
	private static void Seat( Ride ride, Car car, bool particles )
	{
		for ( var i = 0; i < car.Riders.Count; ++i )
		{
			car.Riders[i] = car.Riders[i] with { Seat = i + 1 };
			++ride.Seated;

			if ( particles )
				Unimplemented.Report( "BUMPER_RIDER_PARTICLE" );
		}
	}

	/// <summary>
	/// <c>FUN_0054ae50</c>: the car's riders to the leaving list, its lead and smoke let go, the car back in the pool;
	/// the last car of a ride not of the water family sets it loading ("Ride Over - reset to loading").
	/// </summary>
	private void Remove( Car car )
	{
		if ( RideOf( car.Ride ) is not { } ride )
		{
			car.Flags = CarFlags.None;
			return;
		}

		Unload( ride, car, particles: false );

		if ( (car.Flags & CarFlags.Lead) != 0 )
			ride.HasLead = false;

		car.Flags = CarFlags.None;
		car.Smoking = false;

		// A puff where it was; its models let go of are the drawing's (ParkBumperBoats).
		Unimplemented.Report( "BUMPER_CAR_REMOVED_PARTICLE" );

		if ( --ride.Cars == 0 )
		{
			Log.Info( "Bumper: Ride Over - reset to loading" );
			ride.State = RideState.Loading;
		}
	}

	/// <summary>
	/// <c>FUN_0054a040</c>'s bumper arm. A new car is put down at a random point of the arena, up to a hundred tries
	/// clear of every other live car, given a random heading and made to bob and trail its wake; its next target, a
	/// buoy or another car, is Q179c's and counted.
	/// </summary>
	private void Retarget( Ride ride, Car car )
	{
		if ( (car.Flags & CarFlags.New) != 0 )
		{
			for ( var tries = 100; ; )
			{
				var radius = (int)(Draw() % (uint)ride.ArenaRadius);
				var angle = (int)(Draw() & 0x1ff);

				// Divided toward nought, as the original's (v + (v >> 31 & 0xff)) >> 8 does.
				car.X = Sine( angle + 0x80 ) * radius / 256 + ride.CentreX;
				car.Z = ride.CentreZ - Sine( angle ) * radius / 256;

				if ( --tries == 0 || !Clashes( car ) )
					break;
			}

			car.Flags &= ~CarFlags.Active;
			car.Heading = (int)(Draw() & 0x1ff);

			if ( ride.BumperType == -1 )
				car.Flags |= CarFlags.Bobs | CarFlags.Wake;
		}

		// The lead car of a running ride, active and not unloading, starts its looped sound here; the unload fades it.
		if ( ride.State == RideState.Running && (car.Flags & (CarFlags.Lead | CarFlags.Active | CarFlags.Unloading))
			== (CarFlags.Lead | CarFlags.Active) )
			Unimplemented.Report( "BUMPER_CAR_SOUND" );

		// The next target, a buoy or another car, is the motion's. Either way the target step clears the unload, the
		// new flag and four of the motion's bits (0x0054a4b5, 0x0054a54e); the bits it sets are the motion's too.
		Unimplemented.Report( "BUMPER_CAR_TARGET" );

		car.Flags &= ~((CarFlags)0x40052 | CarFlags.Unloading | CarFlags.New);
	}

	/// <summary>Whether a live car other than this one is closer than their two radii (<c>0x0054a0e0</c>..<c>0x0054a185</c>).</summary>
	private bool Clashes( Car car )
	{
		foreach ( var other in _cars )
		{
			if ( !other.IsLive || other == car )
				continue;

			var dx = (double)(other.X - car.X);
			var dz = (double)(other.Z - car.Z);

			if ( Math.Sqrt( dx * dx + dz * dz ) < other.Radius + car.Radius )
				return true;
		}

		return false;
	}

	/// <summary>
	/// The draw's answer to a launched car - <c>FUN_00546280</c> clears <c>0x4000000</c> as it spawns the splash, which
	/// is counted at the launch (<c>BUMPER_CAR_SPLASH</c>).
	/// </summary>
	internal static void Landed( Car car ) => car.Flags &= ~CarFlags.Launched;

	private uint Draw() => ParkGenerator.Draw( ref _random );

	/// <summary>
	/// The track system's sine table, <c>DAT_00877358</c>, filled by <c>FUN_00544360</c>: 512 steps a turn,
	/// <c>ftol( sin( 2k × c ) × 256 )</c> with its rounded <c>c</c> (<c>0x700ec8</c>, just under π/512), so the peaks
	/// truncate to 255 and -255.
	/// </summary>
	public static int Sine( int step ) => (int)(Math.Sin( (step & 0x1ff) * 2 * SineStep ) * 256.0);

	/// <summary>The double at <c>0x700ec8</c>.</summary>
	private static readonly double SineStep = BitConverter.Int64BitsToDouble( 0x3F7921F9F01B866E );
}
