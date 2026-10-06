namespace OpenTPW;

/// <summary>
/// The people a park may hire - the pool the hire screen lists, and the half of staffing that exists
/// before anybody is employed.
///
/// <para>
/// The five staff a park ships with are read from the save; everybody else who can be hired waits here.
/// </para>
///
/// <para>
/// <b>Hiring is a PLACEMENT verb in the original, not a transaction.</b> <c>FUN_00507bf0</c> does not
/// create a person and does not touch money: it builds a "place staff" interaction mode carrying the
/// kind, grade, costume and name, and the worker is constructed on the next click at the cursor cell.
/// So this pool hands out a candidate and nothing more - see <see cref="Take"/>.
/// </para>
///
/// <para>
/// <b>There is no hiring fee.</b> <c>StaffPoolInfo.BaseCostPerStaff</c> (2000) and
/// <c>CostPerQualityLevel</c> (100) sit in the balance file and <b>are never read by the
/// executable</b>. The only money staff cost is a monthly wage, and dismissal charges one more month.
/// </para>
///
/// <para>
/// <b>Two numberings, and they are different permutations.</b> A candidate's KIND is 0-4 in the order
/// the hire screen's five tabs run - cleaner, mechanic, entertainer, guard, scientist - while the
/// thing a hire becomes has a MODEL of 5, 4, 6, 7, 8 respectively. <see cref="ModelFor"/> is the only
/// place that conversion is written.
/// </para>
/// </summary>
public sealed class ParkStaffPool
{
	/// <summary>The pool of the park currently loaded, or null outside one - see <see cref="ForgetCurrent"/>.</summary>
	public static ParkStaffPool? Current { get; private set; }

	/// <summary>
	/// Lets go of <see cref="Current"/> as the park ends, with the running park, as the original's pool goes with the
	/// world it is the first member of (<c>0x004091aa</c>) - see <see cref="Level.ForgetRunningPark"/>.
	/// </summary>
	internal static void ForgetCurrent() => Current = null;

	/// <summary>
	/// The candidate on the cursor, or nought.
	///
	/// <para>
	/// <b>Choosing somebody on the hire screen does not hire them.</b> The original puts them into a
	/// place-staff mode - interaction type 5 - and constructs the worker on the next click at a cell,
	/// which is why a cancelled hire costs nothing and takes nobody out of the pool. The same shape as
	/// <see cref="ParkBuilding.Carrying"/>, and for the same reason.
	/// </para>
	/// </summary>
	public static int Carrying { get; private set; }

	/// <summary>Takes a candidate onto the cursor. They stay in the pool until they are put down.</summary>
	public static string Carry( int candidateId )
	{
		if ( Current is not { } pool )
			return "carry: a park has to be loaded";

		foreach ( var person in pool.Candidates )
		{
			if ( person.Id != candidateId )
				continue;

			// Hiring is a placement verb with its own mode, installed over whatever was current: a tool is
			// put away and anything in the hand let go of first, a candidate already carried among them.
			if ( ParkHand.LetGo() is { } letGo )
				Log.Info( $"Hand: {letGo}" );

			Carrying = candidateId;

			// The mode's install sets the carry cursor and hangs a sprite of the candidate under the
			// pointer, which marks a cell it would refuse in red (0x0046c730, 0x0046c480). Nothing
			// here shows a carried candidate.
			Unimplemented.Report( "STAFF_CARRY_PREVIEW" );

			return $"carrying {person.Name}, a grade {person.Grade} " +
				$"{NameOfKind( person.Kind ).ToLowerInvariant()} at {person.Wage} a month - " +
				"click the park to put them down";
		}

		return $"carry: no candidate {candidateId}";
	}

	/// <summary>Puts the carried candidate back. Nothing was taken, so there is nothing to give back.</summary>
	public static string Drop()
	{
		if ( Carrying == 0 )
			return "drop: nobody is on the cursor";

		var was = Carrying;

		Carrying = 0;

		return $"drop: put candidate {was} back - they were never taken out of the pool";
	}

	/// <summary>
	/// Hires whoever is on the cursor onto a cell, through <see cref="Hire"/>. <b>A refused cell leaves
	/// them on the cursor and in the pool</b>, so the next click tries again: the original's place-staff
	/// click (<c>FUN_0046c8e0</c>) answers a refusal with an inert log line and nothing else, and returns
	/// with its mode, its preview and its candidate as they were.
	/// </summary>
	/// <remarks>
	/// <b>The refusals here are not the original's.</b> It refuses only by the cell rule, which is not
	/// built (see <see cref="Hire"/>), and its picker never hands the mode a cell off the map. This park
	/// refuses a cell off the map and a kind it packs no picture for, and treats each as the original treats a
	/// refused cell.
	/// </remarks>
	public static string PlaceCarried( int cellX, int cellY )
	{
		if ( Carrying == 0 )
			return "put: nobody is on the cursor";

		if ( Current is not { } pool || ParkPeople.Current is not { } people )
			return "put: a park has to be loaded";

		if ( pool.Find( Carrying ) is not { } candidate )
			return $"put: candidate {Carrying} is no longer in the pool";

		var thingId = pool.Hire( people, candidate, cellX, cellY );

		if ( thingId == 0 )
			return $"put: {candidate.Name} cannot be put down at ({cellX},{cellY}) - still on the cursor, still in the pool";

		Carrying = 0;

		return $"put: hired {candidate.Name} as thing {thingId} at ({cellX},{cellY})";
	}

	/// <summary>
	/// Puts one candidate into the park at a cell, and only then takes them out of the pool. Answers
	/// their thing id, or nought where the park refused them, in which case they are still waiting.
	/// </summary>
	/// <remarks>
	/// <b>One body for the place-staff click and the console's <c>hire</c></b>, so the two cannot drift
	/// apart in the order that matters: the worker goes up first and the pool loses them second.
	/// </remarks>
	internal int Hire( ParkPeople people, Candidate candidate, int cellX, int cellY )
	{
		// The original takes a worker only on a cell of kind 0, 1, 3 or 9 that is not flagged 0x40 and
		// whose track record passes two tests (FUN_0046c8e0; docs/exe/park-engine.md, "Putting a candidate
		// down"). Here any cell on the map takes one.
		Unimplemented.Report( "STAFF_PLACEMENT_CELL_RULE" );

		var thingId = people.Hire( candidate, cellX, cellY );

		if ( thingId != 0 )
			Take( candidate.Id, out _ );

		return thingId;
	}

	/// <summary>The waiting candidate with this id, or null.</summary>
	public Candidate? Find( int candidateId )
	{
		foreach ( var person in _candidates )
		{
			if ( person.Id == candidateId )
				return person;
		}

		return null;
	}

	/// <summary>How many candidates the pool can hold at once - the original's fixed array.</summary>
	public const int Slots = 32;

	/// <summary>How many kinds of staff there are, which is also how many tabs the hire screen has.</summary>
	public const int Kinds = 5;

	/// <summary>The highest pay grade, and the one a candidate's meter reads 80% at - see <see cref="Candidate"/>.</summary>
	public const int TopGrade = 4;

	/// <summary>
	/// One person waiting to be hired.
	/// </summary>
	/// <param name="Kind">0 cleaner, 1 mechanic, 2 entertainer, 3 guard, 4 scientist - the tab order.</param>
	/// <param name="Grade">
	/// 0 to 4. <b>The hire screen draws it as <c>(grade &lt;&lt; 10) / 5</c></b>, so even a
	/// top-grade candidate fills only four fifths of the meter - that is the original's own
	/// arithmetic, not a bug to correct.
	/// </param>
	/// <param name="Costume">Which of that kind's sprite costumes they wear.</param>
	/// <param name="Mark">The park's <c>mGameTick</c> as they joined the pool - the record's <c>+0xc</c>.</param>
	/// <param name="Lifetime">
	/// How long they wait to be hired, in fours of sweeps - the record's <c>+0x10</c>: <c>StaffTimeoutTime</c> plus a
	/// draw of up to half as much again.
	/// </param>
	public readonly record struct Candidate( int Id, int Kind, string Name, int Grade, int Costume, int Wage,
		int Mark = 0, int Lifetime = 0 );

	/// <summary>A candidate joined the pool while it was running - the hire list's row is added (<c>FUN_00481550</c>).</summary>
	internal event Action<Candidate>? Joined;

	/// <summary>A candidate's time ran out, by their id - the hire list's row goes.</summary>
	internal event Action<int>? Left;

	/// <summary>How many turns the pool has taken, one a sweep the park ran.</summary>
	internal int Turns { get; private set; }

	/// <summary>The <c>mGameTick</c> the pool was last topped up on, or made - world <c>+0x294</c>.</summary>
	internal int Mark { get; private set; }

	private readonly List<Candidate> _candidates = [];
	private readonly ParkBalance? _balance;
	private readonly Random _random;
	private int _nextId = 1;

	/// <summary>Everybody currently available to hire, in the order they joined the pool.</summary>
	public IReadOnlyList<Candidate> Candidates => _candidates;

	/// <summary>Everybody of one kind - what a hire screen tab lists.</summary>
	public IEnumerable<Candidate> OfKind( int kind ) => _candidates.Where( person => person.Kind == kind );

	/// <summary>
	/// The name tables, one per kind, each of 35 entries. <b>The file names are the decode's and are
	/// confirmed on disk</b>; note the first is HANDYMAN where every other name for that kind - the
	/// tab, the UITEXT row, the game's own word - is "cleaner".
	/// </summary>
	private static readonly string[] NameTables =
		["HANDYMAN_NAMES", "MECHANIC_NAMES", "ENTERTAINER_NAMES", "GUARD_NAMES", "RESEARCHER_NAMES"];

	/// <summary>
	/// The balance file's own word for each kind, which is <b>irregular</b> and has to be spelled
	/// exactly: <c>StaffPoolInfo.BeginningNumberOfHandymen</c> but <c>...OfMechanics</c>,
	/// <c>MaxResearchersInPark</c> but <c>MaxGuardsInPark</c>. A key that does not match returns the
	/// fallback silently, which is the quiet way to get a park full of the wrong people.
	/// </summary>
	private static readonly string[] BalanceNames =
		["Handymen", "Mechanics", "Entertainers", "Guards", "Researchers"];

	/// <summary>
	/// The thing model a hire of each kind becomes - <b>a different permutation from the kind order</b>.
	/// Cleaner 5, mechanic 4, entertainer 6, guard 7, scientist 8.
	/// </summary>
	public static int ModelFor( int kind ) => kind switch
	{
		0 => 5,
		1 => 4,
		2 => 6,
		3 => 7,
		4 => 8,
		_ => 0
	};

	/// <summary>The sprite kind the native staff constructors use; docs/exe/park-engine.md, hiring.</summary>
	internal static int SpriteKindFor( int kind ) => kind switch
	{
		0 => 5,
		1 => 6,
		2 => 4,
		3 => 7,
		4 => 8,
		_ => -1
	};

	/// <summary>
	/// The kind a thing model belongs to - the inverse of <see cref="ModelFor"/>, and <b>-1</b> for a
	/// model that is not staff at all.
	/// </summary>
	/// <remarks>
	/// Written as its own switch rather than a search of the other one, because it is the direction a
	/// wage lookup goes: a <see cref="Staff"/> carries its MODEL, and what it is paid is indexed by
	/// KIND. <see cref="ParkWorld.StaffState.PayTypeOf"/> is the same mapping from the save's side and
	/// the two must agree.
	/// </remarks>
	public static int KindFor( int model ) => model switch
	{
		5 => 0,
		4 => 1,
		6 => 2,
		7 => 3,
		8 => 4,
		_ => -1
	};

	/// <summary>
	/// What one kind is called, from the game's own <c>STAFF_TYPES.str</c>.
	/// </summary>
	/// <remarks>
	/// <b>That table holds TEN entries, not five</b> - singular and plural in pairs, so kind k is at
	/// k*2 and its plural at k*2+1. Reading it as five would name a mechanic "Cleaners".
	/// </remarks>
	public static string NameOfKind( int kind, bool plural = false )
	{
		try
		{
			var types = new StringFile( "Language/English/STAFF_TYPES.str" );
			var row = (kind * 2) + (plural ? 1 : 0);

			return types[row] ?? $"kind {kind}";
		}
		catch ( Exception )
		{
			return $"kind {kind}";
		}
	}

	/// <param name="seed">
	/// Fixed so a run is repeatable. <b>A declared deviation:</b> the original rolls from one global
	/// LCG on the world object, seeded from play, so its pool differs every game. Reproducing that is
	/// pointless until something depends on the sequence, and a repeatable pool is what lets a harness
	/// predict a wage before reading it.
	/// </param>
	/// <summary>
	/// The pool's turn, once a thing sweep - the original's <c>FUN_005084f0</c>, which <c>FUN_004d7b20</c> calls on
	/// every sweep (<c>0x004d7b30</c>; <c>docs/exe/park-engine.md</c>, "The staff pool's refresh").
	/// <list type="number">
	/// <item>Each candidate who is not in the hand and is more than four times their lifetime old, in sweeps, is
	/// dropped (<see cref="TimedOut"/>).</item>
	/// <item>When the pool's mark is more than <c>TimeBetweenStaffUpdates * 4</c> sweeps old it is topped up
	/// (<see cref="TopUp"/>) and marked with this sweep.</item>
	/// </list>
	/// </summary>
	/// <param name="gameTick">The park's own clock, one a sweep.</param>
	/// <param name="inPark">How many of a kind the park employs now - what <c>FUN_00508000</c> counts.</param>
	internal void Sweep( int gameTick, Func<int, int> inPark )
	{
		++Turns;

		for ( var at = _candidates.Count - 1; at >= 0; --at )
		{
			var person = _candidates[at];

			if ( person.Id == Carrying || !TimedOut( gameTick, person.Mark, person.Lifetime ) )
				continue;

			_candidates.RemoveAt( at );
			Left?.Invoke( person.Id );

			Log.Info( $"Staff pool: {person.Name}, a {NameOfKind( person.Kind ).ToLowerInvariant()}, timed out on mGameTick "
				+ $"{gameTick}, {gameTick - person.Mark} sweeps after {person.Mark} (lifetime {person.Lifetime})" );
		}

		if ( !UpdateIsDue( gameTick, Mark, Key( "TimeBetweenStaffUpdates", 90 ) ) )
			return;

		var before = _candidates.Count;

		TopUp( gameTick, inPark );
		Mark = gameTick;

		Log.Info( $"Staff pool: topped up on mGameTick {gameTick} by {_candidates.Count - before} to {_candidates.Count} - "
			+ string.Join( ", ", Enumerable.Range( 0, Kinds ).Select( kind => $"{OfKind( kind ).Count()} {NameOfKind( kind, plural: true ).ToLowerInvariant()}" ) ) );
	}

	/// <summary>Whether a candidate's time is up: more than four times their lifetime in sweeps, unsigned (<c>FUN_005084f0</c>'s first loop).</summary>
	internal static bool TimedOut( int gameTick, int mark, int lifetime )
		=> unchecked((uint)(gameTick - mark)) > (uint)(lifetime * 4);

	/// <summary>Whether the pool is due its top-up: its mark more than <c>TimeBetweenStaffUpdates * 4</c> sweeps old, unsigned.</summary>
	internal static bool UpdateIsDue( int gameTick, int mark, int timeBetween )
		=> unchecked((uint)(gameTick - mark)) > (uint)(timeBetween * 4);

	/// <summary>
	/// The top-up. The budget is <c>MaxNumberOfStaffPerUpdate</c>, or the free slots where they are fewer. A kind the
	/// park employs <c>Max&lt;Kind&gt;InPark</c> of is full and wants none; any other wants <c>Max&lt;Kind&gt;</c> less
	/// its candidates in the pool. While the budget and the total wanted last, one draw modulo the total picks a kind
	/// by those weights and a candidate of it is made. Then the minimums are made up (<see cref="MakeUpTheMinimums"/>).
	/// </summary>
	/// <remarks>
	/// The original draws on the world's generator; this project keeps no shared one, so the pool's own is drawn. A
	/// kind with more candidates than its <c>Max</c> wants a negative number, there as here. A kind is full only once
	/// a member of it has been met in the park (<c>FUN_00508000</c> raises the flag as it counts one), so a limit of
	/// nought with nobody employed is not full.
	/// </remarks>
	private void TopUp( int gameTick, Func<int, int> inPark )
	{
		var budget = Math.Min( Key( "MaxNumberOfStaffPerUpdate", 12 ), Slots - _candidates.Count );
		var full = Enumerable.Range( 0, Kinds ).Select( kind => inPark( kind ) is var employed and > 0 && employed >= MostInPark( kind ) ).ToArray();
		var want = Enumerable.Range( 0, Kinds )
			.Select( kind => full[kind] ? 0 : Key( $"Max{BalanceNames[kind]}", 5 ) - OfKind( kind ).Count() ).ToArray();

		for ( var total = want.Sum(); budget > 0 && total > 0; --total )
		{
			// The draw is under the total, which is the wants' own sum, so the walk always lands on a kind, and on
			// one that wants somebody.
			var kind = PickByWeight( want, (uint)_random.Next() % (uint)total );

			if ( full[kind] || _candidates.Count >= Slots )
				break;

			Join( kind, gameTick );
			--budget;
			--want[kind];
		}

		MakeUpTheMinimums( gameTick, inPark, full );
	}

	/// <summary>
	/// The kind a draw lands on: the weights are walked in order, the draw less each one passed, to the first it is
	/// under (signed, as the original compares it). The draw must be under the weights' sum.
	/// </summary>
	internal static int PickByWeight( int[] want, uint draw )
	{
		for ( var kind = 0; kind < want.Length - 1; ++kind )
		{
			if ( (int)draw < want[kind] )
				return kind;

			draw -= (uint)want[kind];
		}

		return want.Length - 1;
	}

	/// <summary>
	/// <c>FUN_00508170</c>: for the mechanics, the handymen, the entertainers, the guards and the researchers in that
	/// order, round and round, a kind whose staff in the park plus candidates in the pool is under
	/// <c>Min&lt;Kind&gt;InPool</c> gets one more, unless it is full or the pool is.
	/// </summary>
	/// <remarks>
	/// The original goes round again whenever a kind was short, whether or not it could be given one, which never
	/// ends for a kind that is short and cannot be: here a round that adds nobody is the last.
	/// </remarks>
	private void MakeUpTheMinimums( int gameTick, Func<int, int> inPark, bool[] full )
	{
		var minimum = Enumerable.Range( 0, Kinds ).Select( kind => Key( $"Min{BalanceNames[kind]}InPool", 1 ) ).ToArray();

		for ( var added = true; added; )
		{
			added = false;

			foreach ( var kind in ShortOfTheirMinimum( kind => inPark( kind ) + OfKind( kind ).Count(), minimum, full ) )
			{
				if ( _candidates.Count >= Slots )
					return;

				Join( kind, gameTick );
				added = true;
			}
		}
	}

	/// <summary>
	/// One round of <c>FUN_00508170</c>: the kinds that are under their minimum and not full, in the order it takes
	/// them. <paramref name="have"/> is asked as each kind is reached, so a candidate one kind was just given counts.
	/// </summary>
	internal static IEnumerable<int> ShortOfTheirMinimum( Func<int, int> have, int[] minimum, bool[] full )
	{
		foreach ( var kind in MinimumsOrder )
		{
			if ( have( kind ) < minimum[kind] && !full[kind] )
				yield return kind;
		}
	}

	/// <summary>The order <c>FUN_00508170</c> takes the kinds in: mechanics first.</summary>
	private static readonly int[] MinimumsOrder = [1, 0, 2, 3, 4];

	/// <summary>Makes a candidate of a kind on this sweep and tells whoever shows the pool.</summary>
	private void Join( int kind, int gameTick )
	{
		var person = Roll( kind, gameTick );

		_candidates.Add( person );
		Joined?.Invoke( person );
	}

	/// <summary>A key of the balance file's <c>StaffPoolInfo</c> block.</summary>
	private int Key( string name, int fallback ) => _balance?.Int( $"StaffPoolInfo.{name}", fallback ) ?? fallback;

	/// <summary>
	/// A candidate's lifetime, in fours of sweeps - <c>FUN_00507600</c>'s last draw: <c>StaffTimeoutTime</c> plus a draw
	/// modulo half of it, so 120 to 179 for the shipped 120.
	/// </summary>
	internal static int LifetimeFrom( int timeout, uint draw ) => timeout + (int)(draw % (uint)Math.Max( 1, timeout >> 1 ));

	public ParkStaffPool( ParkBalance? balance, int seed = 20260920, int gameTick = 0 )
	{
		_balance = balance;
		_random = new Random( seed );
		Current = this;
		Mark = gameTick;

		// The opening pool, which the original generates once as a park opens.
		for ( var kind = 0; kind < Kinds; ++kind )
		{
			var wanted = balance?.Int( $"StaffPoolInfo.BeginningNumberOf{BalanceNames[kind]}", 5 ) ?? 5;

			for ( var i = 0; i < wanted && _candidates.Count < Slots; ++i )
				_candidates.Add( Roll( kind, gameTick ) );
		}

		Log.Info( $"Staff: {_candidates.Count} candidates waiting - " + string.Join( ", ",
			Enumerable.Range( 0, Kinds ).Select( k => $"{OfKind( k ).Count()} {NameOfKind( k, plural: true ).ToLowerInvariant()}" ) ) );
	}

	/// <summary>
	/// Makes one candidate of a kind.
	/// </summary>
	/// <remarks>
	/// The grade is <c>AvgGradeOf&lt;kind&gt; + rand % 3 - 1</c>, clamped to 0..4 - the original's own
	/// roll. <b>It computes that in a BYTE</b>, so an average grade of nought underflows to 0xFF and
	/// the clamp then yields the TOP grade; no shipped balance file sets one, so the fault is latent
	/// rather than live. Clamping in an int here cannot reproduce it, which is a deviation in this
	/// direction rather than the dangerous one.
	/// </remarks>
	private Candidate Roll( int kind, int gameTick )
	{
		var average = _balance?.Int( $"StaffPoolInfo.AvgGradeOf{BalanceNames[kind]}", 2 ) ?? 2;
		var grade = Math.Clamp( average + _random.Next( 3 ) - 1, 0, TopGrade );

		return new Candidate(
			Id: _nextId++,
			Kind: kind,
			Name: RollName( kind ),
			Grade: grade,
			Costume: 0,
			Wage: WageFor( kind, grade ),
			Mark: gameTick,
			Lifetime: LifetimeFrom( Key( "StaffTimeoutTime", 120 ), (uint)_random.Next() ) );
	}

	/// <summary>One of the kind's 35 names, or a plain one where the table will not read.</summary>
	private string RollName( int kind )
	{
		try
		{
			var names = new StringFile( $"Language/English/{NameTables[kind]}.str" );

			return names[_random.Next( 35 )] ?? $"{NameOfKind( kind )} {_nextId}";
		}
		catch ( Exception )
		{
			return $"{NameOfKind( kind )} {_nextId}";
		}
	}

	/// <summary>
	/// What one of these is paid a month: <c>PerGradeStaffConsts[grade].BaseWage</c> times
	/// <c>PerTypeStaffConsts[kind].PayMultiplier</c>.
	/// </summary>
	/// <remarks>
	/// <b>A running jungle park uses the EASY numbers, not the ones in the global file</b> -
	/// <see cref="Level"/> builds its balance with easy mode on, and <c>Easy_Standard.sam</c> overrides
	/// both halves: base wages 3, 4, 5, 7, 9 against 4, 5, 6, 8, 12, and multipliers 9, 23, 12, 15, 25
	/// against 10, 30, 15, 20, 35. So a grade-2 mechanic is 5 x 23 = 115 there and 6 x 30 = 180 by the
	/// global file, and reading the wrong layer is not a rounding difference.
	/// </remarks>
	public int WageFor( int kind, int grade ) => WageFrom( _balance, kind, grade );

	/// <inheritdoc cref="WageFor"/>
	public static int WageFrom( ParkBalance? balance, int kind, int grade )
	{
		var wage = balance?.Int( $"PerGradeStaffConsts[{grade}].BaseWage", 4 ) ?? 4;
		var multiplier = balance?.Int( $"PerTypeStaffConsts[{kind}].PayMultiplier", 10 ) ?? 10;

		return wage * multiplier;
	}

	/// <summary>
	/// What one training point costs a kind at a grade: <c>&lt;Kind&gt;ConstsPerGrade[grade].PoundsPerTrainingPoint</c>,
	/// the table <c>CStaff::TrainMe</c> reads by the member's model (<c>FUN_00505a10</c>). Nought where the balance
	/// has no such key, and at grade 4 in every shipped file.
	/// </summary>
	/// <remarks>
	/// The global <c>Standard.sam</c> is the only file that sets these (5, 8, 12, 15 and nought for grades 0 to 4;
	/// the researcher's 8, 12, 15, 18 and nought): no theme or easy file overrides them.
	/// </remarks>
	public static int PoundsPerTrainingPoint( ParkBalance? balance, int kind, int grade )
		=> kind is >= 0 and < Kinds
			? balance?.Int( $"{TrainingNames[kind]}ConstsPerGrade[{grade}].PoundsPerTrainingPoint", 0 ) ?? 0
			: 0;

	/// <summary>The per-grade tables' prefixes, by kind - the balance file names them in the singular.</summary>
	private static readonly string[] TrainingNames = ["Handyman", "Mechanic", "Entertainer", "Guard", "Researcher"];

	/// <summary>
	/// Takes a candidate out of the pool - what hiring one does to it. Answers false where no such
	/// candidate is waiting.
	/// </summary>
	/// <remarks>
	/// <b>It charges nothing</b>, because hiring does not: the pool hands over a person and the money
	/// only ever moves as a monthly wage. See the class remarks.
	/// </remarks>
	public bool Take( int candidateId, out Candidate taken )
	{
		var at = _candidates.FindIndex( person => person.Id == candidateId );

		if ( at < 0 )
		{
			taken = default;
			return false;
		}

		taken = _candidates[at];
		_candidates.RemoveAt( at );

		return true;
	}

	/// <summary>How many of a kind the park may employ at once - <c>StaffPoolInfo.Max&lt;Kind&gt;InPark</c>.</summary>
	public int MostInPark( int kind )
		=> _balance?.Int( $"StaffPoolInfo.Max{BalanceNames[kind]}InPark", 15 ) ?? 15;

	/// <summary>The pool as lines, for the debug console.</summary>
	public IEnumerable<string> Census()
	{
		foreach ( var person in _candidates )
		{
			yield return $"#{person.Id,-3} {NameOfKind( person.Kind ),-12} {person.Name,-20} " +
				$"grade {person.Grade}  {person.Wage} a month  made {person.Mark} lifetime {person.Lifetime}";
		}
	}
}
