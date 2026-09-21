namespace OpenTPW;

/// <summary>
/// The people a park may hire - the pool the hire screen lists, and the half of staffing that exists
/// before anybody is employed.
///
/// <para>
/// <b>Nothing in this project had one.</b> The five staff a park ships with are read from the save and
/// that was the entire population; there was no such thing as a candidate.
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
	/// <summary>The pool of the park currently loaded, or null outside one.</summary>
	public static ParkStaffPool? Current { get; private set; }

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
	public readonly record struct Candidate( int Id, int Kind, string Name, int Grade, int Costume, int Wage );

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
	public ParkStaffPool( ParkBalance? balance, int seed = 20260920 )
	{
		_balance = balance;
		_random = new Random( seed );
		Current = this;

		// The opening pool, which the original generates once as a park opens.
		for ( var kind = 0; kind < Kinds; ++kind )
		{
			var wanted = balance?.Int( $"StaffPoolInfo.BeginningNumberOf{BalanceNames[kind]}", 5 ) ?? 5;

			for ( var i = 0; i < wanted && _candidates.Count < Slots; ++i )
				_candidates.Add( Roll( kind ) );
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
	private Candidate Roll( int kind )
	{
		var average = _balance?.Int( $"StaffPoolInfo.AvgGradeOf{BalanceNames[kind]}", 2 ) ?? 2;
		var grade = Math.Clamp( average + _random.Next( 3 ) - 1, 0, TopGrade );

		return new Candidate(
			Id: _nextId++,
			Kind: kind,
			Name: RollName( kind ),
			Grade: grade,
			Costume: 0,
			Wage: WageFor( kind, grade ) );
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
	public int WageFor( int kind, int grade )
	{
		var wage = _balance?.Int( $"PerGradeStaffConsts[{grade}].BaseWage", 4 ) ?? 4;
		var multiplier = _balance?.Int( $"PerTypeStaffConsts[{kind}].PayMultiplier", 10 ) ?? 10;

		return wage * multiplier;
	}

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
				$"grade {person.Grade}  {person.Wage} a month";
		}
	}
}
