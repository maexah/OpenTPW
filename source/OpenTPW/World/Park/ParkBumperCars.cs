namespace OpenTPW;

/// <summary>
/// The bumper family's half of the track-ride record and its cars - the Hot Pot's boats, and the four other
/// themes' bumper rides (BumperType -1, -3, -6, -11 and -14). <c>docs/exe/park.md</c>, "How a bumper ride ends a go,
/// and lets its riders off", "Where a bumper ride's cars float" and "How a bumper ride's cars move".
///
/// <para>
/// <b>One record per slot of <see cref="ParkTrackRideTable"/></b>, the same <c>0xd0</c> entry the original keeps there:
/// its state, wear, duration and the two lists of peeps a <c>BUMP</c> fills and empties; and <b>one pool of 256 cars</b>
/// shared by every ride (<c>DAT_00877b68</c>), each with its riders, its timer and where it floats. The script reaches
/// both through <see cref="RideScript"/>'s <c>BUMP</c>, and the track tick (<see cref="Tick"/>) ends a go.
/// </para>
/// <para>
/// <b>The cars move</b> in the track tick's two passes: each steers at a buoy of its arena's ring or chases another
/// car, is thrust, slowed and stepped, then every pair that overlaps is bumped apart and a car past the rim is put back
/// on it, reflected. The performance the thrust, friction, turn and restitution are read from is the script's speed word.
/// </para>
/// <para>
/// <b>Not built, and counted where it is reached:</b> the go-karts' and the water ride's arms, whose scripts keep
/// <c>BUMP</c> a counted no-op; the other bumper types' templates; the cars' sounds, smoke and splashes.
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

		/// <summary>It chases another car (<c>+0x80</c>).</summary>
		Chasing = 0x4,

		/// <summary>It steers at a buoy of its ring (<c>+0x7c</c>).</summary>
		AtBuoy = 0x8,
		Unloading = 0x20,
		Active = 0x4000,

		/// <summary>Set by every target, cleared only by a launch: a bumped car with it chooses again.</summary>
		Targeted = 0x8000,

		/// <summary>The point steered at is taken again from the target and the car's offset.</summary>
		Resteer = 0x10000,

		/// <summary>Struck by another car this tick, as the second of a closing pair.</summary>
		Bumped = 0x40000,
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

		/// <summary>Its handle, <c>slot | BumperType &lt;&lt; 8</c>.</summary>
		public int Handle { get; init; }

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

		/// <summary>The template's <c>+0x30</c>..<c>+0x4c</c>: thrust, friction, turn and restitution at performance 0 and 100.</summary>
		public (int Thrust, int Friction, int Turn, int Restitution) Least { get; init; }

		public (int Thrust, int Friction, int Turn, int Restitution) Most { get; init; }

		/// <summary><c>+0x1c</c>: the performance, 0-100, and what <c>FUN_00545180</c> lerps from it.</summary>
		public int Performance { get; internal set; }

		/// <summary><c>+0x20</c>: what a car's velocity gains a tick, along its steering heading.</summary>
		public int Thrust { get; internal set; }

		/// <summary><c>+0x24</c>: what a car keeps of its velocity a tick, in 1024ths.</summary>
		public int Friction { get; internal set; }

		/// <summary><c>+0x28</c>: how far a car's steering heading turns a tick, in 512ths.</summary>
		public int TurnRate { get; internal set; }

		/// <summary><c>+0x2c</c>: what a bump gives the struck car, in 1024ths.</summary>
		public int Restitution { get; internal set; }

		/// <summary><c>+0x68</c>: how near a buoy a car has reached it.</summary>
		public int ArrivalRadius { get; init; }

		/// <summary>
		/// The script's speed word, pushed into the performance at each of its scheduler visits (<c>0x00551844</c>);
		/// null for a ride no script is bound to.
		/// </summary>
		public int? SpeedWord { get; internal set; }

		/// <summary>The ring the arena's buoys make, as offsets from its centre, first to last (<c>+0xa8</c>, <c>+0xac</c>).</summary>
		public IReadOnlyList<(int X, int Z)> Buoys { get; init; } = [];

		/// <summary><c>+0xc4</c>: who boards the next car, head first.</summary>
		public List<int> Boarding { get; } = [];

		/// <summary><c>+0xc8</c>: who has come off and waits for <c>BUMP 2</c>, head first.</summary>
		public List<int> Leaving { get; } = [];
	}

	/// <summary>One car of the pool.</summary>
	public sealed class Car
	{
		/// <summary>Its place in the pool, which a car chasing it keeps (<c>+0x80</c>).</summary>
		public int Index { get; init; }

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

		/// <summary><c>+0x3c</c>, <c>+0x40</c>: its velocity, in the record's fixed point a tick.</summary>
		public int VelocityX { get; internal set; }

		public int VelocityZ { get; internal set; }

		/// <summary><c>+0x44</c>, <c>+0x48</c>: the velocity after this tick's friction, before any bump - what a bump reads.</summary>
		public int SteppedX { get; internal set; }

		public int SteppedZ { get; internal set; }

		/// <summary><c>+0x4c</c>: its speed this tick.</summary>
		public int Speed { get; internal set; }

		/// <summary><c>+0x50</c>: the heading it steers and is thrust along, in 512ths of a turn.</summary>
		public int Steering { get; internal set; }

		/// <summary><c>+0x54</c>: which way it is drawn pointing, in 512ths of a turn.</summary>
		public int Heading { get; internal set; }

		/// <summary><c>+0x5c</c>: how far <see cref="Heading"/> turned this tick.</summary>
		public int Turn { get; internal set; }

		/// <summary><c>+0x6c</c>, <c>+0x70</c>: the point it steers at.</summary>
		public int SteerX { get; internal set; }

		public int SteerZ { get; internal set; }

		/// <summary><c>+0x74</c>, <c>+0x78</c>: its offset from its target, so no two cars aim at one point.</summary>
		public int OffsetX { get; internal set; }

		public int OffsetZ { get; internal set; }

		/// <summary><c>+0x7c</c>: the buoy of its ride's ring it steers at, -1 none yet.</summary>
		public int Buoy { get; internal set; } = -1;

		/// <summary><c>+0x80</c>: the pool index of the car it chases, -1 none.</summary>
		public int Chased { get; internal set; } = -1;

		/// <summary><c>+0x8c</c>: buoys, or ticks of a chase, left before it chooses again.</summary>
		public int Patience { get; internal set; }

		/// <summary><c>+0x98</c>: the ride whose arena it floats in - the collision object it was launched in.</summary>
		public int Arena { get; internal set; }

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
			_cars[i] = new Car { Index = i };
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

		// The Hot Pot's template at 0x764178: duration 4350, car radius 768, mesh 1 of 1, performance 50, the four
		// ranges, eight cars at most, arrival 1024; its bumper arm lays the arena 0x1200 in radius with its ring of
		// buoys at 0xc00 (FUN_00545890, 0x00545a0b) and sets state 0.
		var ride = new Ride
		{
			BumperType = bumperType,
			Handle = handle,
			Duration = 4350,
			CarRadius = 768,
			MeshBase = 1,
			MeshCount = 1,
			MostCars = 8,
			ArenaRadius = 0x1200,
			Least = (8, 960, 8, 1000),
			Most = (12, 1010, 14, 1100),
			ArrivalRadius = 1024,
			Buoys = Ring( 0xc00 ),
			State = RideState.Closed
		};

		_rides[slot] = ride;
		SetPerformance( handle, 50 );
	}

	/// <summary>
	/// The eight buoys <c>FUN_00545890</c> lays round an arena: the first at (0, <paramref name="radius"/>), each next
	/// the last turned an eighth by the sine table's 181s, divided toward nought.
	/// </summary>
	private static (int X, int Z)[] Ring( int radius )
	{
		var buoys = new (int X, int Z)[8];
		var (x, z) = (0, radius);
		var s = Sine( 0x40 );
		var c = Sine( 0xc0 );

		for ( var i = 0; i < buoys.Length; ++i )
		{
			buoys[i] = (x, z);
			(x, z) = ((c * x + s * z) / 256, (c * z - s * x) / 256);
		}

		return buoys;
	}

	/// <summary>
	/// <c>FUN_00545180</c>: the performance, clamped to 0-100, and the four values lerped from the template by it.
	/// </summary>
	public void SetPerformance( int handle, int performance )
	{
		if ( RideOf( handle ) is not { } ride )
			return;

		var p = Math.Clamp( performance, 0, 100 );

		if ( p != ride.Performance || ride.Thrust == 0 )
			Log.Info( $"Bumper: Set ride performance to {p}" );

		ride.Performance = p;
		ride.Thrust = ride.Least.Thrust + (ride.Most.Thrust - ride.Least.Thrust) * p / 100;
		ride.Friction = ride.Least.Friction + (ride.Most.Friction - ride.Least.Friction) * p / 100;
		ride.TurnRate = ride.Least.Turn + (ride.Most.Turn - ride.Least.Turn) * p / 100;
		ride.Restitution = ride.Least.Restitution + (ride.Most.Restitution - ride.Least.Restitution) * p / 100;
	}

	/// <summary>
	/// The scheduler's push of each bound script's speed word into its ride (<c>0x00551844</c>, every visit, whether
	/// or not a turn ran) - called after each tick's scripts. <see cref="RideScript"/> keeps no speed word (Q155), so the
	/// word is the one the object constructor would have pushed, kept on the record when the script is bound.
	/// </summary>
	public void PushSpeedWords()
	{
		foreach ( var ride in _rides )
		{
			if ( ride?.SpeedWord is { } word && word != ride.Performance )
				SetPerformance( ride.Handle, word );
		}
	}

	/// <summary>Binds a ride's speed word - the constructor's push (<c>0x004db534</c>): the item's starting speed, or the loader's 50.</summary>
	internal void BindSpeedWord( int handle, int word )
	{
		if ( RideOf( handle ) is { } ride )
			ride.SpeedWord = word;
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
		car.VelocityX = car.VelocityZ = car.SteppedX = car.SteppedZ = car.Speed = 0;
		car.Steering = car.Turn = car.SteerX = car.SteerZ = car.OffsetX = car.OffsetZ = car.Patience = 0;
		car.Buoy = -1;
		car.Chased = -1;
		car.Arena = ArenaHolding( ride.CentreX, ride.CentreZ )?.Handle ?? 0;

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

		SetPerformance( handle, ride.Performance );
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
	/// The track tick - <c>TrackRides_Tick</c>, <c>FUN_00546c80</c>, once per 31 ms step before the scripts. The first
	/// pass gives each live car its turn (<see cref="CarTick"/>) and, still live, its step (<see cref="Step"/>); the
	/// second bumps every overlapping pair in the pool and keeps each car in its arena.
	/// </summary>
	public void Tick()
	{
		++Ticks;

		foreach ( var car in _cars )
		{
			if ( !car.IsLive )
				continue;

			car.Flags &= ~(CarFlags)0x200000;
			CarTick( car );

			if ( car.IsLive )
				Step( car );
		}

		// Every live car against every other, so each overlapping pair meets twice, once from each side, both times
		// from the velocities the first pass left. No bumper arena is flagged 0x80 and no bumper car 0x200.
		foreach ( var car in _cars )
		{
			if ( !car.IsLive )
				continue;

			foreach ( var other in _cars )
			{
				if ( other.IsLive && other != car )
					Bump( car, other );
			}

			// The stuck count +0xa4 is -1 for every bumper car, and only the unused type -2 lays an obstacle
			// (Bumper_PushOffObstacles); the Hot Pot's boats bump silently (Bumper_PlayCarSound).
			KeepInArena( car );
		}
	}

	/// <summary>
	/// <c>Bumper_CarTick</c>, <c>FUN_005474b0</c>, the bumper arms: the phase counted, the timer counted down while the
	/// ride runs unbroken, and at nought the car unloads; then a chase, an unload or a buoy. An unloading car hands its
	/// riders to the leaving list, and only when no rider is seated anywhere on the ride does it go back to loading.
	/// </summary>
	private void CarTick( Car car )
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

		if ( (car.Flags & CarFlags.Chasing) != 0 )
		{
			Chase( ride, car );
			return;
		}

		if ( (car.Flags & CarFlags.Unloading) == 0 )
		{
			if ( (car.Flags & (CarFlags)0x58) != 0 && ride.Buoys.Count > 0 )
				RoundBuoy( ride, car );

			return;
		}

		if ( Unload( ride, car, particles: true ) != 0 )
			return;

		ride.State = RideState.Loading;
		car.Flags &= ~(CarFlags)0x7e;
		Log.Info( $"Bumper: ride 0x{car.Ride:x} empty and loading again at track tick {Ticks}" );
		car.Animation = RideAnimations.NoRole;
	}

	/// <summary>
	/// <c>Bumper_CarTick</c>'s chase (<c>0x0054760f</c>..): patience down a tick, and at nought, or with the chased car
	/// gone, unloading or just bumped, a new target. Otherwise it leads the chased car - its position plus its velocity
	/// times the ticks it would take to reach it at this car's speed, at most 24 - pulled inside the arena when outside.
	/// </summary>
	private void Chase( Ride ride, Car car )
	{
		var chased = car.Chased is >= 0 and < Pool ? _cars[car.Chased] : null;

		if ( --car.Patience < 1 || chased is null || (chased.Flags & (CarFlags.Live | CarFlags.Unloading | CarFlags.Bumped)) != CarFlags.Live )
		{
			Retarget( ride, car );
			return;
		}

		car.Flags |= CarFlags.Resteer;

		var speed = Math.Max( Distance( car.VelocityX, car.VelocityZ ), 1 );
		var ticks = Math.Min( Distance( car.X - chased.X, car.Z - chased.Z ) / speed, 24 );

		var x = chased.VelocityX * ticks + chased.X;
		var z = chased.VelocityZ * ticks + chased.Z;

		// Bumper_NearestJoinedObject: a bumper arena joins only itself, so the nearest is the chased car's own.
		if ( !InArena( x, z, RideOf( chased.Arena ) ) && ArenaHolding( x, z ) is null && RideOf( chased.Arena ) is { } arena )
		{
			var ox = (x - arena.CentreX) / 8;
			var oz = (z - arena.CentreZ) / 8;
			var d = Distance( ox, oz ) + 32;

			x = ox * arena.ArenaRadius / d + arena.CentreX;
			z = oz * arena.ArenaRadius / d + arena.CentreZ;
		}

		Steer( ride, car, x, z );
	}

	/// <summary>
	/// <c>Bumper_CarTick</c>'s buoy arm: steer at it; bumped, choose again; within the arrival radius, patience down by
	/// one, and at nought choose again, else on to the ring's next buoy with a fresh offset, the old x offset becoming
	/// the z. A bumper ring's buoys are flagged 1 alone and hold no object (<c>FUN_00545890</c>), so the finish and
	/// the object check of the go-karts' and water ride's are not reached.
	/// </summary>
	private void RoundBuoy( Ride ride, Car car )
	{
		var buoy = ride.Buoys[Math.Clamp( car.Buoy, 0, ride.Buoys.Count - 1 )];
		var distance = Steer( ride, car, ride.CentreX + buoy.X, ride.CentreZ + buoy.Z );

		if ( (car.Flags & (CarFlags.Targeted | CarFlags.Bumped)) == (CarFlags.Targeted | CarFlags.Bumped) )
		{
			Retarget( ride, car );
			car.Flags |= CarFlags.Resteer;
			return;
		}

		if ( distance > ride.ArrivalRadius )
			return;

		if ( --car.Patience == 0 )
		{
			Retarget( ride, car );
			car.Flags |= CarFlags.Resteer;
			return;
		}

		if ( (car.Flags & (CarFlags.AtBuoy | (CarFlags)0x40)) == CarFlags.AtBuoy )
		{
			car.Buoy = (car.Buoy + 1) % ride.Buoys.Count;
			car.OffsetZ = car.OffsetX;
			car.OffsetX = ride.ArrivalRadius / 2 - (int)(Draw() % (uint)ride.ArrivalRadius);
		}
		else
		{
			// 0x10 (a buoy of any ride) and 0x40 are set only by the go-karts' and water ride's targets.
			Unimplemented.Report( "BUMPER_CAR_OTHER_TARGET" );
		}

		car.Flags |= CarFlags.Resteer;
	}

	/// <summary>
	/// <c>Bumper_SteerToward</c>, <c>FUN_00547c60</c>: the point steered at taken again when asked, the steering heading
	/// turned the record's whole turn toward it when more than half a turn off, and thrust along that heading when
	/// nearly on it; both need <see cref="CarFlags.Active"/> and the ride not broken, and the thrust no
	/// <see cref="CarFlags.Seating"/>, so only a car with riders, in a go, drives. Answers the distance to the point.
	/// </summary>
	private int Steer( Ride ride, Car car, int targetX, int targetZ )
	{
		if ( (car.Flags & CarFlags.Resteer) != 0 && car.Arena != 0 )
		{
			if ( ArenaHolding( targetX, targetZ ) is not { } holder )
			{
				car.SteerX = targetX;
				car.SteerZ = targetZ;
				return -1;
			}

			if ( holder.Handle != car.Arena )
			{
				// Another ride's arena: Bumper_ObjectsJoin links only the karts' and water ride's pieces.
				Unimplemented.Report( "BUMPER_CAR_UNJOINED_TARGET" );
				return -1;
			}

			car.SteerX = car.OffsetX + targetX;
			car.SteerZ = car.OffsetZ + targetZ;
			car.Flags &= ~CarFlags.Resteer;
		}

		var dx = car.SteerX - car.X;
		var dz = car.SteerZ - car.Z;
		var bearing = (HeadingOf( dx, dz ) - car.Steering) & 0x1ff;
		var distance = Distance( dx, dz );

		var turn = ride.TurnRate;

		if ( bearing > 0x100 )
		{
			turn = -turn;
			bearing = 0x200 - bearing;
		}

		if ( ride.TurnRate / 2 < bearing && ride.Wear != 2 && (car.Flags & CarFlags.Active) != 0 )
			car.Steering = (car.Steering + turn) & 0x1ff;

		// A bumper arena is laid whole (flags 0x1f), so the three quarters of the thrust in a part object is not reached.
		if ( bearing < Math.Clamp( distance / 512, 22, 48 ) && (car.Flags & (CarFlags.Active | CarFlags.Seating)) == CarFlags.Active
			&& ride.Wear != 2 )
		{
			car.VelocityX += Sine( car.Steering ) * ride.Thrust * 4 / 1024;
			car.VelocityZ += Sine( car.Steering + 0x80 ) * ride.Thrust * 4 / 1024;
		}

		return distance;
	}

	/// <summary>
	/// <c>Bumper_StepCar</c>, <c>FUN_00547f50</c>, the Hot Pot's arm: friction (seven tenths of it while seating, none for
	/// a broken ride), the stepped velocity kept for the bumps, the speed, the position; and while the ride runs the drawn
	/// heading eased toward the steering heading by the speed over 1000. Another bumper type turns toward its velocity
	/// instead, and has no template here (<c>BUMPER_TEMPLATE_n</c>).
	/// </summary>
	private void Step( Car car )
	{
		if ( RideOf( car.Ride ) is not { } ride )
			return;

		var friction = ride.Friction;

		if ( (car.Flags & CarFlags.Seating) != 0 )
			friction = friction * 7 / 10;
		else if ( ride.Wear == 2 )
			friction = 1000;

		car.VelocityX = friction * car.VelocityX / 1024;
		car.VelocityZ = friction * car.VelocityZ / 1024;
		car.SteppedX = car.VelocityX;
		car.SteppedZ = car.VelocityZ;
		car.Speed = (int)Math.Sqrt( (double)car.VelocityX * car.VelocityX + (double)car.VelocityZ * car.VelocityZ );
		car.X += car.VelocityX;
		car.Z += car.VelocityZ;

		if ( ride.State == RideState.Running )
		{
			var off = (car.Steering - car.Heading) & 0x1ff;

			if ( off > 0x100 )
				off -= 0x200;

			car.Turn = off * car.Speed / 1000;
		}
		else
		{
			car.Turn = 0;
		}

		car.Heading = (car.Heading + car.Turn) & 0x1ff;
	}

	/// <summary>
	/// A bump, in <c>TrackRides_Tick</c>'s second pass: <paramref name="a"/> the outer loop's car, <paramref name="b"/>
	/// the inner's, overlapping by their radii. Closing, <paramref name="b"/> is kicked along the line between them by
	/// the closing speed times the struck ride's restitution, and <paramref name="a"/> back by the rest; parting, their
	/// positions are pushed apart.
	/// </summary>
	private void Bump( Car a, Car b )
	{
		var dx = b.X - a.X;
		var dz = b.Z - a.Z;
		var reach = a.Radius + b.Radius;
		var d = Distance( dx, dz );

		if ( d == 0 || d >= reach || RideOf( b.Ride ) is not { } struck )
			return;

		var h = HeadingOf( dx, dz );
		var closing = (HeadingOf( a.SteppedX - b.SteppedX, a.SteppedZ - b.SteppedZ ) - h) & 0x1ff;
		var e = struck.Restitution;

		if ( closing < 0x80 || closing > 0x180 )
		{
			var rx = a.SteppedX - b.SteppedX;
			var rz = a.SteppedZ - b.SteppedZ;
			var impulse = (int)Math.Sqrt( (double)(rx * rx + rz * rz) ) * Sine( closing + 0x80 ) * 4 / 1024;
			var kick = e * impulse / 1024;
			var back = impulse - kick;

			b.VelocityX += kick * Sine( h ) * 4 / 1024;
			b.VelocityZ += kick * Sine( h + 0x80 ) * 4 / 1024;
			b.Flags |= CarFlags.Bumped | CarFlags.Resteer;

			a.VelocityX -= back * Sine( h ) * 4 / 1024;
			a.VelocityZ -= back * Sine( h + 0x80 ) * 4 / 1024;

			// The crunch is asked only for the karts and the -3, -6, -11 and -14 arenas; none of them has a template here.
			return;
		}

		var ox = dx - reach * dx / d;
		var oz = dz - reach * dz / d;
		var bx = ox * e / 1024;
		var bz = oz * e / 1024;

		b.X -= bx;
		b.Z -= bz;
		a.X += ox - bx;
		a.Z += oz - bz;
	}

	/// <summary>
	/// <c>Bumper_KeepInObject</c>, <c>FUN_005497b0</c>: a car out of its arena takes another that holds it, or is put back
	/// on the rim, its velocity cut by the restitution and reflected about the line from the centre.
	/// </summary>
	private void KeepInArena( Car car )
	{
		var arena = RideOf( car.Arena );

		if ( InArena( car.X, car.Z, arena ) )
			return;

		if ( ArenaHolding( car.X, car.Z ) is { } other )
		{
			car.Arena = other.Handle;
			car.Flags |= CarFlags.Resteer;
			return;
		}

		if ( arena is null || RideOf( car.Ride ) is not { } ride )
			return;

		var ox = car.X - arena.CentreX;
		var oz = car.Z - arena.CentreZ;
		var stepped = HeadingOf( car.SteppedX, car.SteppedZ );

		// The disassembly's own arithmetic (0x0054997b..0x005499ab): 2 × (out − stepped) − 256, a mirror about the normal.
		var outward = -HeadingOf( ox, oz );
		var mirror = ((((-0x80 - outward) & 0x1ff) * 2 - 0x100) - ((stepped - 0x100) & 0x1ff) - stepped) & 0x1ff;

		var vz = ride.Restitution * car.VelocityZ / 1024;
		var vx = ride.Restitution * car.VelocityX / 1024;

		car.VelocityX = (vz * Sine( mirror ) + vx * Sine( mirror + 0x80 )) * 4 / 1024;
		car.VelocityZ = (vz * Sine( mirror + 0x80 ) - vx * Sine( mirror )) * 4 / 1024;

		var d = Distance( ox, oz );

		car.X = ox * arena.ArenaRadius / d + arena.CentreX;
		car.Z = oz * arena.ArenaRadius / d + arena.CentreZ;
	}

	/// <summary>
	/// <c>Bumper_PointInObject</c>, <c>FUN_005493f0</c>, for a whole round arena: on its centre, or no farther from it
	/// than its radius.
	/// </summary>
	private static bool InArena( int x, int z, Ride? arena )
	{
		if ( arena is null )
			return false;

		var dx = arena.CentreX - x;
		var dz = arena.CentreZ - z;

		return (dx == 0 && dz == 0) || Distance( dx, dz ) <= arena.ArenaRadius;
	}

	/// <summary>
	/// The first arena that holds a point - the walk of the collision objects (<c>DAT_00877b70</c>) for one flagged live
	/// and not <c>0x40</c>. Here in slot order, where the original's is the order the objects were laid in.
	/// </summary>
	private Ride? ArenaHolding( int x, int z ) => Array.Find( _rides, ride => InArena( x, z, ride ) );

	/// <summary>
	/// <c>Bumper_Distance</c>, <c>FUN_00549020</c>: <c>ftol( sqrt )</c> of the offsets, halved together until x is at
	/// most <c>0x8000</c> and z at most <c>0x7fff</c>, shifted back.
	/// </summary>
	public static int Distance( int x, int z )
	{
		x = Math.Abs( x );
		z = Math.Abs( z );

		var shift = 0;

		while ( x > 0x8000 || z > 0x7fff )
		{
			++shift;
			x >>= 1;
			z >>= 1;
		}

		return (int)Math.Sqrt( (double)(x * x + z * z) ) << shift;
	}

	/// <summary>
	/// The heading of an offset, in 512ths of a turn whose direction is (sin, cos) in (x, z):
	/// <c>ftol( 256 − atan2( x, −z ) × c × 256 )</c>, <c>c</c> the double at <c>0x700eb8</c>. The integer is negated before
	/// it is loaded, so a z of nought is +0 and an offset of nought heads 256.
	/// </summary>
	public static int HeadingOf( int x, int z ) => (int)(256.0 - Math.Atan2( x, (double)-z ) * HeadingStep * 256.0);

	/// <summary>The double at <c>0x700eb8</c>, just over 1/π.</summary>
	private static readonly double HeadingStep = BitConverter.Int64BitsToDouble( 0x3FD45F318E7ADAF5 );

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
	/// <c>Bumper_Retarget</c>, <c>FUN_0054a040</c>'s bumper arm. A new car is put down at a random point of the arena, up
	/// to a hundred tries clear of every other live car, given a random heading and made to bob and trail its wake. Then
	/// its next target: unless it chases or was just bumped, 3 draws in 16 chase another car of the ride (with two cars
	/// or more), else a buoy - the next after its own 4 in 16 when it was at one, else one stepped on from the first.
	/// </summary>
	private void Retarget( Ride ride, Car car )
	{
		if ( (car.Flags & CarFlags.New) != 0 )
		{
			for ( var tries = 100; ; )
			{
				var radius = (int)(Draw() % (uint)ride.ArenaRadius);
				var angle = (int)(Draw() & 0x1ff);

				car.X = Sine( angle + 0x80 ) * radius / 256 + ride.CentreX;
				car.Z = ride.CentreZ - Sine( angle ) * radius / 256;

				if ( --tries == 0 || !Clashes( car ) )
					break;
			}

			car.Flags &= ~CarFlags.Active;
			car.Heading = (int)(Draw() & 0x1ff);
			car.Steering = car.Heading;

			if ( ride.BumperType == -1 )
				car.Flags |= CarFlags.Bobs | CarFlags.Wake;
		}

		// The lead car of a running ride, active and not unloading, starts its looped sound here; the unload fades it.
		if ( ride.State == RideState.Running && (car.Flags & (CarFlags.Lead | CarFlags.Active | CarFlags.Unloading))
			== (CarFlags.Lead | CarFlags.Active) )
			Unimplemented.Report( "BUMPER_CAR_SOUND" );

		if ( (car.Flags & (CarFlags.Chasing | CarFlags.Bumped)) == 0 && (Draw() & 0xf) <= 2 && ride.Cars >= 2 )
		{
			// The candidates are listed head first from the end of the pool, and the draw counts from the head.
			var candidates = _cars.Where( other => other.IsLive && other != car && (other.Flags & CarFlags.Unloading) == 0
				&& other.Ride == car.Ride && other.Arena == car.Arena ).ToList();

			if ( candidates.Count > 0 )
			{
				var pick = (int)(Draw() % (uint)candidates.Count);

				car.Chased = candidates[candidates.Count - 1 - pick].Index;
				car.Patience = 90;
				car.Flags = (car.Flags & ~(CarFlags)0x14007a) | CarFlags.Chasing | CarFlags.Targeted;
				return;
			}
		}

		var steps = 1;
		var from = car.Buoy;

		if ( (car.Flags & CarFlags.AtBuoy) == 0 || (Draw() & 0xf) > 3 )
		{
			steps = (int)(Draw() % (uint)ride.Buoys.Count);
			from = 0;
		}

		car.Flags = (car.Flags & ~((CarFlags)0x40076 | CarFlags.New)) | CarFlags.AtBuoy | CarFlags.Resteer | CarFlags.Targeted;
		car.Buoy = (from + steps) % ride.Buoys.Count;
		car.Patience = 3;
	}

	/// <summary>Whether a live car other than this one is closer than their two radii (<c>0x0054a0e0</c>..<c>0x0054a185</c>).</summary>
	private bool Clashes( Car car )
	{
		foreach ( var other in _cars )
		{
			if ( other.IsLive && other != car && Distance( other.X - car.X, other.Z - car.Z ) < other.Radius + car.Radius )
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
