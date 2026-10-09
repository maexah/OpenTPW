namespace OpenTPW;

/// <summary>
/// What an object keeps of its days - its six day rings and two lifetime counts, seeded from its record and moved
/// by the settle-up, the door and the charge (<c>docs/exe/ride-operation.md</c>, "The settle-up's bookkeeping" and
/// "The object's six day rings").
/// </summary>
/// <remarks>
/// <see cref="ParkWorld"/> describes the file and cannot change, so the running figures live here, one per object,
/// kept by <see cref="ParkState"/>. A thing built in the park starts as the original's constructor starts one
/// (<c>FUN_004db090</c>): both counts nought, and each ring empty with its entry at −1.
/// </remarks>
public sealed class ParkObjectRings
{
	/// <summary>Today's cost of goods, <c>+0xf8</c>.</summary>
	public DayRing Costs { get; }

	/// <summary>Today's takings, <c>+0x70</c>, which the charge credits.</summary>
	public DayRing Takings { get; }

	/// <summary>Today's customers, <c>+0x1a8</c>: every settle-up, before the win roll.</summary>
	public DayRing Customers { get; }

	/// <summary>Today's walk-aways, <c>+0x230</c>: every guest the price turned away at the door.</summary>
	public DayRing WalkAways { get; }

	/// <summary>Today's served, <c>+0x2b8</c>: the settle-ups past the win roll.</summary>
	public DayRing Served { get; }

	/// <summary>Today's satisfaction, <c>+0x340</c>, which <see cref="Satisfy"/> averages into.</summary>
	public DayRing Satisfaction { get; }

	/// <summary><c>mNumCustomers</c>, <c>+0x1a0</c>: every settle-up since it was built. Never rolled.</summary>
	public int NumCustomers { get; private set; }

	/// <summary><c>mNumWalkAways</c>, <c>+0x1a4</c>: every refusal at its door since it was built. Never rolled.</summary>
	public int NumWalkAways { get; private set; }

	/// <summary>A thing just built: nothing counted, every ring empty.</summary>
	public ParkObjectRings()
	{
		Costs = new();
		Takings = new();
		Customers = new();
		WalkAways = new();
		Served = new();
		Satisfaction = new();
	}

	/// <summary>A thing as its record left it.</summary>
	public ParkObjectRings( ParkWorld.ObjectRings saved )
	{
		Costs = new( saved.Costs );
		Takings = new( saved.Takings );
		Customers = new( saved.Customers );
		WalkAways = new( saved.WalkAways );
		Served = new( saved.Served );
		Satisfaction = new( saved.Satisfaction );
		NumCustomers = saved.NumCustomers;
		NumWalkAways = saved.NumWalkAways;
	}

	/// <summary>The rings and the two counts as a park file's record holds them.</summary>
	public ParkWorld.ObjectRings Written()
		=> new( Costs.Written(), Takings.Written(), NumCustomers, Customers.Written(), NumWalkAways,
			WalkAways.Written(), Served.Written(), Satisfaction.Written() );

	/// <summary>A visit counted - <c>FUN_004e1690</c>: <c>mNumCustomers</c> and today's customers, one each.</summary>
	public void CountCustomer()
	{
		NumCustomers++;
		Customers.Today++;
	}

	/// <summary>A guest turned away by the price at the door - <c>FUN_004e1670</c>: <c>mNumWalkAways</c> and today's
	/// walk-aways, one each.</summary>
	public void CountWalkAway()
	{
		NumWalkAways++;
		WalkAways.Today++;
	}

	/// <summary>
	/// Averages one visit's change in happiness into today's satisfaction - <c>FUN_004e1e00</c>: taken whole while
	/// today's is nought, otherwise the two halved, truncated toward nought (<c>CDQ</c>, <c>SUB</c>, <c>SAR</c>).
	/// </summary>
	/// <remarks>
	/// No count and no clamp, so a day whose average comes to nought is overwritten by the next visit, as the
	/// original's is.
	/// </remarks>
	public void Satisfy( int change )
		=> Satisfaction.Today = Satisfaction.Today == 0 ? change : (Satisfaction.Today + change) / 2;

	/// <summary>
	/// The day's change - message <c>0xb</c> in the object's handler, <c>FUN_004dd320</c>: every ring rolled, in its
	/// order, and nothing else touched.
	/// </summary>
	public void Roll()
	{
		Customers.Roll();
		WalkAways.Roll();
		Served.Roll();
		Takings.Roll();
		Costs.Roll();
		Satisfaction.Roll();
	}

	/// <summary>The debug console's line for <c>rings</c>: both counts, then each ring in the roll's order.</summary>
	public string Census()
		=> $"customers {NumCustomers} walk-aways {NumWalkAways} | customers {Customers.Census()} "
			+ $"| walk-aways {WalkAways.Census()} | served {Served.Census()} | takings {Takings.Census()} "
			+ $"| costs {Costs.Census()} | satisfaction {Satisfaction.Census()}";

	/// <summary>
	/// One figure's thirty days - <c>mTemp</c>, <c>mData[30]</c>, <c>mCurrentEntry</c>, <c>mNumEntries</c> and
	/// <c>mWrappedAround</c>.
	/// </summary>
	public sealed class DayRing
	{
		private readonly int[] _days = new int[ParkWorld.DayRing.Length];

		/// <summary><c>mTemp</c>, today's figure so far.</summary>
		public int Today { get; set; }

		/// <summary><c>mCurrentEntry</c>, the last finished day's slot; −1 before any day has ended.</summary>
		public int CurrentEntry { get; private set; } = -1;

		/// <summary><c>mWrappedAround</c>: every slot has been filled once.</summary>
		public bool WrappedAround { get; private set; }

		/// <summary>An empty ring, as the constructor leaves one (the original leaves its days unwritten; none is
		/// read before it is filled).</summary>
		public DayRing()
		{
		}

		/// <summary>A ring as the record left it.</summary>
		public DayRing( ParkWorld.DayRing saved )
		{
			Today = saved.Today;
			CurrentEntry = saved.CurrentEntry;
			WrappedAround = saved.WrappedAround;

			saved.Days.Take( _days.Length ).ToArray().CopyTo( _days, 0 );
		}

		/// <summary>The ring as a park file's record holds it, every slot as it stands.</summary>
		public ParkWorld.DayRing Written()
			=> new( CurrentEntry, ParkWorld.DayRing.Length, WrappedAround, Today, [.. _days] );

		/// <summary>How many finished days it holds - <c>FUN_00495d40</c>: all of them once wrapped, else the entry
		/// plus one.</summary>
		public int Filled => WrappedAround ? _days.Length : CurrentEntry + 1;

		/// <summary>
		/// Ends the day - <c>FUN_004dd320</c>'s step for each ring: the entry on, back to nought and marked wrapped
		/// once it reaches the end, today's figure stored there and today started at nought.
		/// </summary>
		public void Roll()
		{
			if ( ++CurrentEntry >= _days.Length )
			{
				CurrentEntry = 0;
				WrappedAround = true;
			}

			_days[CurrentEntry] = Today;
			Today = 0;
		}

		/// <summary>
		/// The figure <paramref name="daysAgo"/> finished days back, nought for the last finished day -
		/// <c>FUN_00495cf0</c>; nought where the ring holds nothing that far back.
		/// </summary>
		public int DaysAgo( int daysAgo )
		{
			if ( daysAgo < 0 || daysAgo >= _days.Length )
				return 0;

			if ( daysAgo <= CurrentEntry )
				return _days[CurrentEntry - daysAgo];

			return WrappedAround ? _days[CurrentEntry - daysAgo + _days.Length] : 0;
		}

		/// <summary>Today, the entry, the wrap, the last thirty added up and every finished day, newest first.</summary>
		public string Census()
			=> $"today {Today} entry {CurrentEntry} wrapped {(WrappedAround ? 1 : 0)} last30 {LastDays( _days.Length )} "
				+ $"[{string.Join( ",", Enumerable.Range( 0, Filled ).Select( DaysAgo ) )}]";

		/// <summary>
		/// The last <paramref name="days"/> finished days added up, today's excluded - what the ride window's Users
		/// last month shows of the customers ring (<c>FUN_004ade40</c>, <c>0x004ade7d</c>). Fewer are added while
		/// the ring holds fewer (<c>0x004adeb7</c>, <c>FUN_00495d40</c>).
		/// </summary>
		/// <remarks>
		/// Each day is added as an unsigned 32-bit figure and the total truncated by <c>__ftol</c>, whose low half
		/// is kept (<c>0x004adefd</c>..<c>0x004adf20</c>).
		/// </remarks>
		public int LastDays( int days )
		{
			var total = 0L;

			for ( var i = 0; i < Math.Min( days, Filled ); ++i )
				total += (uint)DaysAgo( i );

			return unchecked((int)total);
		}
	}
}
