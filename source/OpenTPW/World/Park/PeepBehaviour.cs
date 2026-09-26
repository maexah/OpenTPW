using System;

namespace OpenTPW;

/// <summary>
/// One turn of what a guest is <i>doing</i> - the original's <c>FUN_005019f0</c>, the switch on state that
/// runs after the needs and decides what walking comes to.
///
/// <para>
/// <b>This moves a guest from state to state on their own turn</b>, through <see cref="Peep.SetState"/>,
/// which queues the animation <see cref="Peep.AnimationFor"/> answers for the state entered.
/// </para>
/// <para>
/// <b>Eight of the twenty-two cases are answered in the switch's own body; the other fourteen jump to a
/// handler.</b> The inline cases are 0, 7, 8, 12, 14, 16, 17 and 20.
/// </para>
/// <para>
/// The save's thirteen are in <see cref="PeepState.HeadingForGate"/>, <see cref="PeepState.Entering"/>
/// and <see cref="PeepState.WaitingForOpening"/>, all three of which delegate - but a guest who is
/// admitted to a ride is put into <see cref="PeepState.Riding"/>, which is inline, and one who finishes
/// deciding is put into <see cref="PeepState.Wandering"/>, which is inline too. <b>What decides whether a
/// state matters is whether anything SETS it, not where the park starts.</b>
/// </para>
/// <para>
/// <b>What decides every one of those three is whether the park is open</b> - <c>FUN_0051a280</c>, which
/// returns <c>world + 0x1da710</c>, which the executable's own field-name table pairs with
/// <c>mParkClosed</c>.
/// </para>
/// <para>
/// <b>The admission sequence, for whoever carries it on.</b> HeadingForGate arrives and judges the fee;
/// a fee it accepts sets a "paid" flag and sends the guest back to wait for the gate; waiting with that
/// flag set and standing on the right cell becomes Entering; Entering arrives, takes a visitor number and
/// goes on to decide what to do. <b>The whole of that loop is here</b>, judging, waiting and the paid
/// arm included - see <see cref="Step"/> for each remaining deferral and the reason it is deferred.
/// </para>
/// </summary>
public sealed class PeepBehaviour
{
	private readonly Random _random;

	/// <param name="park">
	/// The park these guests are in, for the facts the behaviours ask about. Null for a test that is only
	/// interested in a transition, which then sees an open park and no visitors.
	/// </param>
	/// <param name="random">
	/// The rolls a state entry makes - only <see cref="PeepState.WaitingForOpening"/> makes one. Taken so
	/// that a test can seed it; the game does not, because the original rolls from a global generator.
	/// </param>
	/// <param name="state">
	/// The park's own running state, or null to make one from <paramref name="park"/>. A park that is
	/// being played hands in the one the level owns, so that what is taken at the gate lands on the
	/// balance everything else reads - see <see cref="ParkState"/>.
	/// </param>
	/// <param name="catalogue">
	/// What the things in this park actually are, for the ride arm to score them by. Null leaves a guest
	/// choosing on distance and queue alone - see <see cref="ParkRideChooser"/>.
	/// </param>
	/// <param name="admit">
	/// Asks a ride to take this guest aboard, answering whether it did - the guest's side of
	/// <c>FUN_004e0900</c>.
	///
	/// <para>
	/// <b>A delegate for the reason <paramref name="gateStatus"/> is one</b>, and for one more: the
	/// admission needs the ride's SCRIPT and the park's guests by id, and this type has neither. Taking
	/// <see cref="ParkRideOperation"/> here would tie every guest's turn to the whole of ride operation
	/// for a single yes-or-no. Null refuses every guest at the door: they walk back to their place in the
	/// queue (<see cref="FindQueueDestination"/>), or are put out when they cannot get there.
	/// </para>
	/// </param>
	public PeepBehaviour( ParkWorld? park, Random? random = null,
		ParkAdmission? admission = null, Func<int>? gateStatus = null, ParkState? state = null,
		ParkItemCatalogue? catalogue = null,
		Func<ParkWorld.CatalogueObject, int, bool>? admit = null,
		Func<ParkWorld.CatalogueObject, int, bool>? finishAdmission = null,
		Action<ParkWorld.CatalogueObject, int>? tellTheScript = null,
		Action<ParkWorld.CatalogueObject, int>? walkAway = null,
		Action<ParkWorld.CatalogueObject, int>? leaveQueue = null,
		Func<int, bool>? stillQueueing = null )
	{
		_admit = admit;
		_finishAdmission = finishAdmission;
		_tellTheScript = tellTheScript;
		_walkAway = walkAway;
		_leaveQueue = leaveQueue;
		_stillQueueing = stillQueueing;

		// Zero is open, which is the way round the name is not - see ParkWorld.ParkClosed. ParkState
		// applies that rule itself, so it is not repeated here.
		State = state ?? new ParkState( park );
		Admission = admission;
		_gateStatus = gateStatus;
		_random = random ?? new Random();
		// The state goes in so the chooser walks the RUNNING park's object chain: something bought this
		// session is in that one and in no other, and a guest is never offered what the walk cannot reach.
		_chooser = new ParkRideChooser( park, catalogue, state: State );
		_park = park;
		_catalogue = catalogue;
	}

	/// <summary>
	/// The park these guests are in, as it is being played rather than as it was saved.
	///
	/// <para>
	/// <see cref="Takings"/> and <see cref="VisitorsToDate"/> read through this, because
	/// <see cref="ParkWorld"/> describes a file and cannot be moved.
	/// </para>
	/// </summary>
	public ParkState State { get; }

	/// <summary>
	/// The same, from the two facts themselves rather than from a park.
	///
	/// <para>
	/// <b>This lets a test start a park shut.</b> The only park that can be loaded is the one the game ships,
	/// and it is saved open; taking the facts directly reaches both arms of every branch that turns on the
	/// gates being closed - including the one that decides whether a guest arriving at the gate judges the
	/// fee or settles down to wait - without a park to load.
	/// </para>
	/// </summary>
	/// <param name="admission">
	/// What the park charges and what a guest makes of it. <b>Null leaves the fee unjudged</b> rather than
	/// guessed at: a guest who reaches the ticket booths with nothing able to price the park stands there.
	/// </param>
	/// <param name="gateStatus">
	/// What the park's gate says it is doing - <c>ParkRides.GateStatus</c>, which reads the gate script's
	/// own <c>VAR_STATUS</c>. See <see cref="GateWillAdmit"/> for what null means and why.
	/// </param>
	public PeepBehaviour( bool parkIsClosed, int visitorsToDate, Random? random = null,
		ParkAdmission? admission = null, Func<int>? gateStatus = null )
	{
		State = new ParkState( parkIsClosed, visitorsToDate );
		Admission = admission;
		_gateStatus = gateStatus;
		_random = random ?? new Random();

		// No park, so nothing to choose from - which is the right answer for a guest built out of two
		// facts rather than out of a save.
		_chooser = new ParkRideChooser( null );
		_park = null;
		_catalogue = null;
	}

	private readonly Func<int>? _gateStatus;

	/// <summary>Asks a ride to take a guest aboard - see the constructor's remarks.</summary>
	private readonly Func<ParkWorld.CatalogueObject, int, bool>? _admit;

	/// <summary>
	/// Finishes an admission the script has taken up, given the ride and the tick - the guest's half of
	/// <c>FUN_00500870</c>. A delegate for the same reason <see cref="_admit"/> is one.
	/// </summary>
	private readonly Func<ParkWorld.CatalogueObject, int, bool>? _finishAdmission;

	/// <summary>
	/// Hands a thing's script the outcome of the roll a guest entering it makes -
	/// <see cref="ParkRideOperation.OutcomeVariable"/>. A delegate for the reason <see cref="_admit"/> is
	/// one: the write needs the thing's SCRIPT, which this type has no way to reach.
	/// </summary>
	private readonly Action<ParkWorld.CatalogueObject, int>? _tellTheScript;

	/// <summary>
	/// The ride's side of a guest walking away from its door - <c>FUN_004e0ac0</c>, then <c>FUN_004ddd20</c>:
	/// the nominee let go of and the queue left, <c>VAR_LETMEON</c> emptied wherever it names them. A delegate
	/// for the reason <see cref="_admit"/> is one. Null leaves the ride holding them, which only a test does.
	/// </summary>
	private readonly Action<ParkWorld.CatalogueObject, int>? _walkAway;

	/// <summary>
	/// The ride's side of a guest put out of its queue by their own turn, or by a failed walk to their place after
	/// joining or after a refused door - <c>FUN_004ddd20</c> alone: <c>VAR_LETMEON</c> emptied if it names them, and
	/// the queue spliced by their own links (<see cref="ParkRideOperation.LeaveQueue"/>).
	/// A delegate for the reason <see cref="_admit"/> is one. Null leaves the queue holding them, which only a
	/// test does.
	/// </summary>
	private readonly Action<ParkWorld.CatalogueObject, int>? _leaveQueue;

	/// <summary>
	/// Whether a guest, by thing id, is still in a queue's states - <see cref="ParkRideOperation.IsQueueing"/>,
	/// which the queue walk asks of everybody it steps past (<see cref="ParkState.PositionInQueue"/>). This type
	/// keeps no guest by id, so it is handed the test. Null follows every link.
	/// </summary>
	private readonly Func<int, bool>? _stillQueueing;

	/// <summary>
	/// What a guest deciding what to do picks from - <c>FUN_004fcb10</c>. Always present, because a
	/// chooser with no park behind it simply chooses nothing, which is what the ride arm should do then.
	/// </summary>
	private readonly ParkRideChooser _chooser;

	/// <summary>
	/// The park these guests are in, for the two questions joining a queue asks of it: which object they
	/// chose, and where its queue ends. Null leaves a guest unable to join one, which is the same answer
	/// a null park gives everywhere else here.
	/// </summary>
	private readonly ParkWorld? _park;

	/// <summary>What the chosen thing actually is, for the excitement a guest turns away from.</summary>
	private readonly ParkItemCatalogue? _catalogue;

	/// <summary>
	/// The park and its catalogue, for a RIDE's turn rather than a guest's - see
	/// <see cref="ParkRideOperation"/>.
	///
	/// <para>
	/// <b>Exposed rather than duplicated.</b> A ride's turn needs the objects to walk and each item's track
	/// type, and this already holds both for the guest side; <see cref="ParkPeople"/> owns one of these and
	/// would otherwise have to keep a second reference to the same two things. Neither is stored anywhere
	/// else in the park's people, which is why they are reached through here.
	/// </para>
	/// </summary>
	internal ParkWorld? Park => _park;

	/// <inheritdoc cref="Park"/>
	internal ParkItemCatalogue? Catalogue => _catalogue;

	/// <summary>What the park charges and how a guest feels about it, or null where nothing can say.</summary>
	public ParkAdmission? Admission { get; }

	/// <summary>
	/// What this park has taken in admissions since it opened, counting from nought rather than from the
	/// balance the save recorded.
	///
	/// <para>
	/// <b>It is <see cref="ParkState.Takings"/>, read through <see cref="State"/></b>: taking a fee moves
	/// <c>mBalance</c> and <c>mProfitThisYear</c> by the same amount (<c>FUN_004d0600</c>, which adds it to
	/// both and to two running totals on the world), and <see cref="ParkState.Take"/> moves the balance and
	/// this together. The park's money on screen is <see cref="ParkState.Balance"/>.
	/// </para>
	/// </summary>
	public int Takings => State.Takings;

	/// <summary>
	/// What the park's rides are worth to a guest deciding whether the price is fair - the sum
	/// <c>FUN_004c8240</c> makes.
	///
	/// <para>
	/// <b>Nought, because nothing computes it.</b> <c>FUN_004c8240</c> is not decoded (<c>docs/exe/park.md</c>,
	/// "What the balance file supplies, and the one score that is not decoded"), so every guest judges the
	/// fee against a park worth nothing. It is settable so that the term is visible and testable rather
	/// than a zero nobody can see.
	/// </para>
	/// </summary>
	public int ParkExcitement { get; set; }

	/// <summary>
	/// Whether the gate will let anybody through - the pair of questions at the top of
	/// <c>FUN_004ff7f0</c>, which wants <c>mParkClosed</c> nought <b>and</b> the gate reporting 1.
	///
	/// <para>
	/// <b>A null <c>gateStatus</c> reads as open, and that is a choice with a precedent.</b>
	/// The constructor above already treats a null park as an open one, for the same reason: a park with
	/// no script runtime bound is not a park whose gates are shut, and answering "shut" would strand every
	/// guest at the bus stop on the strength of missing plumbing rather than of anything in the file.
	/// </para>
	/// </summary>
	public bool GateWillAdmit
		=> !ParkIsClosed && (_gateStatus == null || _gateStatus() == ParkRides.GateIsOpen);

	/// <summary>
	/// Whether the park is shut to visitors - <see cref="ParkState.ParkIsClosed"/>, seeded from the save and
	/// moved by the door on the entry-price screen (<see cref="ParkState.SetParkClosed"/>, <c>FUN_00519ef0</c>).
	///
	/// <para>
	/// The two key bindings that would also reach it (<c>InputButton.OpenPark</c> and <c>ClosePark</c>) are
	/// among the bindings nothing consumes.
	/// </para>
	/// </summary>
	public bool ParkIsClosed => State.ParkIsClosed;

	/// <summary>
	/// How many guests this park has ever admitted, counting on from what the save recorded.
	///
	/// <para>
	/// The original keeps it on the world at <c>0x1da714</c> and moves it in exactly one place -
	/// <c>FUN_0051aaf0</c>, which adds one and announces "Your park has received its %dth visitor". Its only
	/// caller is a guest finishing <see cref="PeepState.Entering"/>, so this counts admissions and not
	/// arrivals. It is held here rather than written back because <see cref="ParkWorld"/> describes a file
	/// and is deliberately immutable.
	/// </para>
	/// </summary>
	public int VisitorsToDate => State.VisitorsToDate;

	/// <summary>
	/// What a guest heading for the gate does <b>not</b> hurry at. The original writes one of three speeds
	/// into the person's <c>+0xc2</c> before it walks them: <see cref="Peep.UnhurriedSpeed"/>,
	/// <see cref="Peep.HurryingSpeed"/>, and a third of <b>50</b> that is reached only when a bus is due.
	/// </summary>
	/// <remarks>
	/// <b>The 50 is deliberately not reproduced and not declared as a constant.</b> Reaching it needs the
	/// arrival vehicle's <i>script</i> state - <c>FUN_0051a690</c> looks the bus thing up and asks its
	/// script what it is doing - and this turn does not ask, although the bus runs its script
	/// (<see cref="ParkRides"/> binds it). Whether the branch is reached is not measured.
	/// </remarks>
	public const int GateHurryShare = 4;

	/// <summary>
	/// Which way a guest ends up facing when they arrive somewhere the original turns them - an eleven-bit
	/// turn, written straight onto the thing at <c>+0x1c</c>.
	/// </summary>
	public const int ArrivalHeading = 0x400;

	/// <summary>
	/// The last state <see cref="Step"/> was handed that its switch has <b>no case for at all</b>, or null
	/// if every state it has been given was answered by something.
	///
	/// <para>
	/// <b>This exists because an unanswered state is invisible.</b> A guest in one is never walked, never
	/// re-stated, never logged; on screen they stand still - which is also what several perfectly faithful
	/// states do, so the two cannot be told apart by looking. Recording the fall-through is what lets a test
	/// tell a deliberate stillness from a hole in the machine.
	/// </para>
	/// <para>
	/// <b>It is not a diagnostic switch and it is not tooling.</b> It is one field the behaviour keeps
	/// about itself, written on the one path that should never be taken; there is nothing to turn on and
	/// nothing to turn off.
	/// </para>
	/// </summary>
	public PeepState? UnansweredState { get; private set; }

	/// <summary>
	/// Whether a thing has hold of this guest - a queue they are in, or a ride that has them. The
	/// original's own condition for refusing to delete somebody: <c>Leaving</c> sweeps the thing list
	/// for one whose <c>+0x212</c> names this person and leaves them alone if it finds one. Removing
	/// somebody a thing still names would leave the thing pointing at nobody.
	/// </summary>
	internal static bool HeldByAThing( PeepState state )
		=> state is PeepState.InQueue or PeepState.SteppingUpQueue or PeepState.BeingAdmitted
			or PeepState.EnteringRide or PeepState.Riding or PeepState.LeavingRide;

	/// <summary>
	/// One turn of one guest's behaviour.
	///
	/// <para>
	/// <b>The switch comes first and the walk second, which is the original's order and not a detail.</b>
	/// <c>FUN_005019f0</c> decides what state a guest is in and only then asks whether they got anywhere, so
	/// a guest in a state that does not walk never reaches the walk at all.
	/// </para>
	/// <para>
	/// <see cref="PeepState.WaitingForOpening"/> is <see cref="Wait"/>,
	/// <see cref="PeepState.JudgingTheFee"/> is <see cref="Judge"/>, and
	/// <see cref="PeepState.Deciding"/> - the hub a guest returns to whenever they finish anything - is
	/// <see cref="Decide"/>. What is absent sits inside those - Decide's leave test and the arms before its
	/// split (Q109, Q111) - and each is recorded where it happens rather than here.
	/// </para>
	/// <para>
	/// <b>And the give-up path does nothing on purpose.</b> Where the walk reports it cannot get through,
	/// the original's answer in four of these five cases is to print "Big problem - no way this should
	/// happen" through its diagnostic call and leave the guest where they are. There is nothing to
	/// reproduce but the leaving-alone.
	/// </para>
	/// </summary>
	/// <param name="tick">
	/// The thing tick, the same counter <see cref="Peep.Tick"/> is spread across, and the clock the states that
	/// record a time compare against: the original's <c>mGameTick</c> goes up by one per thing sweep
	/// (<c>0x00516394</c>). It is <see cref="GameClock.Ticks"/> over eight, which runs from the program's start
	/// and is not reset on entering a park, so a park's first sweep carries the lobby's; the original zeroes
	/// <c>mGameTick</c> at level start (<c>0x00515865</c>) and loads the save's (<c>0x00517bec</c>).
	/// </param>
	public void Step( Peep peep, PeepWalk walk, SpriteScript? playing, int tick )
	{
		ArgumentNullException.ThrowIfNull( peep );
		ArgumentNullException.ThrowIfNull( walk );

		// <b>Their day running out, a deviation Q109 holds.</b> ExitLevel counts down on every needs tick -
		// once per guest's turn in four. The original tests it for exactly nought in the Deciding turn alone
		// and aims the guest at CrossingParkSide (docs/exe/ride-operation.md, "The state-6 turn, in order");
		// this sends any guest a thing is not holding home from any state at nought or below, to a bus stop,
		// docking nothing. A guest a thing is holding is left alone, for the reason Leaving is.
		if ( peep.ExitLevel <= 0
			&& peep.State is not (PeepState.HeadingForExit or PeepState.Leaving)
			&& !HeldByAThing( peep.State )
			&& Admission is { } goingHome )
		{
			SendTo( peep, walk, EitherOf( goingHome.BusStopA, goingHome.BusStopB ) );
			peep.SetState( PeepState.HeadingForExit, tick, _random );

			return;
		}

		switch ( peep.State )
		{
			// Walking somewhere chosen. Arriving turns the guest to a fixed heading and puts them at the
			// gate; the original writes the heading before the state, and the state queues the standing
			// animation on its way in.
			case PeepState.Walking:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					walk.Heading = ArrivalHeading;
					peep.SetState( PeepState.AtGate, tick, _random );
				}

				break;

			// Heading for the gate - five of Lost Kingdom's thirteen. The speed is decided every turn,
			// before the walk, and then the fee is judged if the park will let them in.
			case PeepState.HeadingForGate:
				peep.PurposeSpeed = HurriesToTheGate( peep ) ? Peep.HurryingSpeed : Peep.UnhurriedSpeed;

				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					peep.SetState( ParkIsClosed ? PeepState.WaitingForOpening : PeepState.JudgingTheFee,
						tick, _random );
				}

				break;

			// Standing at a ticket booth making their mind up about the price - FUN_004ff9d0.
			//
			// The countdown comes first and nothing else happens on a turn that decrements it. It is the
			// guest's own mParkOpeningWaitingTime, shared with waiting for the gate - see Peep.ParkOpeningWait.
			case PeepState.JudgingTheFee:
				if ( peep.ParkOpeningWait != 0 )
				{
					--peep.ParkOpeningWait;
					break;
				}

				if ( Admission is { } admission )
					Judge( peep, walk, admission, tick );

				break;

			// Waiting outside for the gate - FUN_004ff7f0, whose three arms are two questions deep.
			case PeepState.WaitingForOpening:
				Wait( peep, walk, tick );

				break;

			// Coming through the gate - seven of the thirteen. Arriving is what makes somebody a visitor.
			//
			// The guard this does NOT have is the one at the top of FUN_004ffb20: a guest whose park has
			// shut under them, or whose gate is not open, is sent back to head for the gate again. Both
			// halves can be asked (GateWillAdmit), and the entry-price door shuts a running park, so the
			// guard is a gap rather than an unreachable arm.
			case PeepState.Entering:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					peep.VisitorNumber = State.Admit();
					peep.SetState( PeepState.Deciding, tick, _random );
				}

				break;

			// Walking to something they chose - FUN_004ffbc0. The ride arm of Deciding puts a guest into
			// this state, IsAWalkingState lists it, and AnimationFor gives it the walk.
			case PeepState.GoingToRide:
				switch ( Walked( peep, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						JoinTheQueue( peep, walk, tick );
						break;

					// "The person has become stuck on the way to the ride" - they give up on it and think
					// again, which is what the original does rather than leaving them standing.
					case WalkVerdict.CannotReach:
						peep.MajorDest = 0;
						peep.SetState( PeepState.Deciding, tick, _random );
						break;

					default:
						break;
				}

				break;

			// Standing in a queue - FUN_004ffff0, its arms in its own order: see QueueTurn.
			case PeepState.InQueue:
				QueueTurn( peep, walk, tick );

				break;

			// Walking to the ride that called them forward, and asking it to take them - FUN_005006b0.
			//
			// <b>THE ADMISSION IS THE GUEST'S, NOT THE RIDE'S.</b> This is what sets PeepState.EnteringRide,
			// which CompleteAdmission waits on.
			//
			// Arriving and getting STUCK are one path, which is the original's own shape: it logs "Person
			// %d: Got stuck in middle o[f]..." and then carries on into the same test rather than treating
			// it as a failure.
			//
			// First the guest asks whether the thing is worth its price to them (FUN_004fde50, 0x00500715;
			// see PeepPriceOpinion), and walks away from the door if it is not - WalkAwayFromTheDoor.
			// ParkAdmission judges the GATE fee, a different question.
			//
			// Refused, the guest walks back to their place - still linked at the head, so the front
			// (FindQueueDestination, 0x00500826) - to wait in the queue for a new call forward, and is put out
			// if they cannot get there (0x00500857), without the ride forgetting them (no FUN_004e0ac0).
			case PeepState.BeingAdmitted:
				if ( Walked( peep, walk, playing ) != WalkVerdict.Walking && Chosen( peep ) is { } arriving )
				{
					if ( ThinksTooExpensive( peep, arriving ) )
					{
						WalkAwayFromTheDoor( peep, arriving, tick );

						break;
					}

					if ( _admit?.Invoke( arriving, peep.ThingId ) == true )
					{
						Log.Info( $"Person {peep.ThingId} been AdmitPerson'd to ride {arriving.ThingId}, "
							+ "now waiting for script to admit me" );

						peep.SetState( PeepState.EnteringRide, tick, _random );

						RollForTheVisit( peep, arriving );
					}
					else if ( !FindQueueDestination( peep, walk, arriving, tick ) )
					{
						Log.Info( $"Person {peep.ThingId}: Couldn't rejoin FOQ even!" );
						PutOutOfTheQueue( peep, arriving, tick, "the door" );
					}
				}

				break;

			// Waiting for the script to take them up, and coming off the queue when it has -
			// FUN_005019f0's case 0xe, which is FUN_00500870 inlined.
			//
			// <b>THE COMPLETION IS THE GUEST'S TOO.</b> ParkPeople's ride turn calls CompleteAdmission only
			// for a ride that is closed (mCanLoad nought, in Invite's place, 0x004e13fc) or broken, waiting
			// for an upgrade or condemned (states 1, 2 and 4), through CompleteOrTurnAway - which is
			// faithful: the original does not call it from an open, healthy ride's turn either, and its only
			// other caller there is SetState. So in an open park this arm is what finishes an admission.
			//
			// The gate is FUN_004e0a70, four lines: script[VAR_LETMEON] != mFirstInQ. That is exactly
			// what CompleteAdmission already tests, so nothing new is decided here - this arm only calls
			// it from the side that calls it in the original. It checks the head, the state, removes them
			// from the queue and sets Riding.
			case PeepState.EnteringRide:
				if ( Chosen( peep ) is { } taking )
					_finishAdmission?.Invoke( taking, tick );

				break;

			// Shuffling up a queue, which ends the same way whether they got there or gave up - the
			// original writes the same state from both arms of the test, and that is not a mistake to
			// tidy: a guest who cannot shuffle forward is still in the queue.
			case PeepState.SteppingUpQueue:
				if ( Walked( peep, walk, playing ) != WalkVerdict.Walking )
					peep.SetState( PeepState.InQueue, tick, _random );

				break;

			// Choosing what to do next - FUN_004fec90, the hub a guest is CONSTRUCTED in and returns to
			// whenever they finish anything. The seven guests standing in Lost Kingdom's archway are here.
			case PeepState.Deciding:
				Decide( peep, walk, tick );

				break;

			// Wandering about the park, and this one is answered inline in the original's own switch rather
			// than by a handler - case 7 of FUN_005019f0, and it is five lines long.
			//
			// Arriving and giving up are treated alike, as they are for SteppingUpQueue: either way a guest
			// takes a ONE IN FOUR chance of picking somewhere else to wander to and staying here, and
			// otherwise drops back to Deciding. That loop is what keeps a park in motion.
			case PeepState.Wandering:
				if ( Walked( peep, walk, playing ) != WalkVerdict.Walking )
				{
					if ( (_random.Next() & (KeepWanderingShare - 1)) == 0 && SetRandomDest( peep, walk ) )
						peep.SetState( PeepState.Wandering, tick, _random );
					else
						peep.SetState( PeepState.Deciding, tick, _random );
				}

				break;

			// Walking about outside the park, which ends at the bus stop.
			//
			// The original picks one of two headings here depending on whether a bus is due, and takes the
			// same 0x400 this does when none is. The other heading needs the arrival vehicle's state, which
			// this turn does not ask, so the no-bus reading is what is reproduced.
			case PeepState.WalkingOutside:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					walk.Heading = ArrivalHeading;
					peep.SetState( PeepState.AtTheBusStop, tick, _random );
				}

				break;

			// Standing at the gate having walked to it - FUN_004ff520. They pick one of the two ticket
			// booths and set off to be charged, which is how a guest who arrives from outside joins the
			// admission sequence at its head.
			//
			// The original gates this on FUN_0051a760, which asks the arrival vehicle's script what it is
			// doing, and returns 1 at its first test when no bus thing stands. A bus stands in the park and
			// its script runs - ParkFixedItems stands it and ParkRides binds it - so whether the branch is
			// reached is not measured. The gate is left open here.
			case PeepState.AtGate:
				if ( Admission is { } atTheGate )
				{
					SendTo( peep, walk, EitherOf( atTheGate.TicketBoothA, atTheGate.TicketBoothB ) );
					peep.SetState( PeepState.HeadingForGate, tick, _random );
				}

				break;

			// Walking away from a ride that has just let them off - FUN_00500900, and THE STATE THAT CLOSES
			// THE PARK'S LOOP. Arriving at the ride's exit drops them back into Deciding, which is what lets
			// a guest who has had one go go and have another.
			//
			// ParkRideOperation.Dismiss sets this state from the ride's own turn.
			//
			// <b>The destination is cleared on the stuck arm only, and that asymmetry is the original's.</b>
			// FUN_00500900 zeroes +0x1dc when the walk reports it cannot get through, and on arrival keeps
			// it while it logs "Person %d: successfully left rid[e]". Deciding's ride arm clears MajorDest
			// before it chooses, so the kept id lasts until then - and until then a sale of that ride still
			// puts them off, as it does in the original.
			//
			// <b>The pending-second-destination arm is absent because nothing writes the field.</b> Before
			// dropping to Deciding the original reads the person's +0x1de - somewhere they had chosen while
			// they were on the ride - and resumes it as GoingToRide if that thing still exists. Nothing in
			// this tree ever writes +0x1de, so the arm is unreachable, not unbuilt.
			case PeepState.LeavingRide:
				switch ( Walked( peep, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						peep.SetState( PeepState.Deciding, tick, _random );
						break;

					case WalkVerdict.CannotReach:
						peep.MajorDest = 0;
						peep.SetState( PeepState.Deciding, tick, _random );
						break;

					default:
						break;
				}

				break;

			// On the ride, and doing nothing is the whole of it: case 0x10 of FUN_005019f0 is a single call
			// that resolves the ride's thing pointer and returns.
			//
			// <b>A guest does not take themselves off a ride - the ride takes them off.</b>
			// ParkRideOperation.Dismiss sets LeavingRide when the script says the go is over, so a guest
			// with nothing to do on their own turn is faithful rather than stalled. This case exists so
			// that the state is ANSWERED: see UnansweredState for why a deliberate stillness and a hole in
			// the switch are indistinguishable on screen, and why the difference is recorded rather than
			// left to a comment.
			case PeepState.Riding:
				break;

			// Heading for the exit, having decided not to stay - FUN_00500a50. They walk to whichever bus
			// stop the arm that sent them here chose, and on arriving go on to pick a cell outside.
			//
			// <b>The change-of-mind arm is absent.</b> The original turns a guest back to Deciding - "Make
			// up your mind!" - when mExitLevel (+0x1bc) is positive AND a float conversion of something is
			// non-zero AND the park is open AND FUN_004fa990 agrees.
			//
			// Getting stuck prints "I'm stuck in the park, even though it's closed!!" and leaves them where
			// they are, which is the give-up path this switch takes everywhere.
			case PeepState.HeadingForExit:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
					peep.SetState( PeepState.PickingACellOutside, tick, _random );

				break;

			// Their day is done and nothing is holding them - ParkPeople.Depart takes them out of the
			// park on the next sweep. It is terminal rather than a no-op: this class owns what a guest
			// wants and never owns the list they are in, so the removal belongs to whoever does.
			case PeepState.Leaving:
				break;

			// <b>Four states answered by standing still, each saying what it waits on.</b> Nothing here sets
			// 8 or 9; HeadingForExit and WalkingOutside set 19 and 21, and ParkPeople takes a guest in 19 out
			// of the park. A case that breaks looks exactly like a missing case on screen - the guest stands
			// still either way - so the difference has to be written down, and UnansweredState is what lets
			// the program itself tell them apart.
			//
			// PlayingSpotAnimation (8) returns to SavedState once ten ticks have passed; FUN_004fc890 is
			// one line and both halves of it exist here. What does not exist is anything that PLAYS a spot
			// animation, so the state is never entered.
			//
			// <b>GoingToMinorDestination (9) is a LITTER-BIN ERRAND.</b> FUN_004fec90 sets it at 004fedf6, when
			// the guest's litter (+0x1b4) reaches 90 and FUN_00500dc0 (at 004feddd) finds the nearest thing
			// carrying flag +0x32 & 0x40 within squared distance under 9; its turn, FUN_004fff20 (case 9 of
			// FUN_005019f0), walks there, writes that thing's script variable 0 to one, ZEROES the guest's
			// litter and returns them to Deciding. It never queues, never charges and never touches +0x1de. In
			// Lost Kingdom there is exactly one such target: thing 17, the Litter Bin at (44,29).
			//
			// <b>It is reachable content.</b> The Drinks Shop's LitterEffect is 50, so two drinks put a guest
			// over the threshold, and with the errand unbuilt (Q111, arm (c)) the guest carries the litter.
			// The separate "Minor Decision" (FUN_004fd570) stays in state 10.
			//
			// PickingACellOutside (19) and AtTheBusStop (21) walk to cells from FUN_004d8650, which reads
			// FixedItemInfo.BusStopA/B (docs/exe/park.md, "Arrivals"), and consult the bus - FUN_0051a690
			// for its script state, FUN_0051aad0 for whether one is here. The bus runs its script
			// (ParkFixedItems stands it, ParkRides binds it). Both stay unbuilt until their decode is checked
			// whole (Q128).
			case PeepState.PlayingSpotAnimation:
			case PeepState.GoingToMinorDestination:
			case PeepState.PickingACellOutside:
			case PeepState.AtTheBusStop:
				break;

			// <b>And a guard for a state with no case.</b> A guest put into one would stand still for ever
			// with nothing in the program able to say so. The original needs no default because its switch
			// answers all twenty-two; this one records rather than throws, because crashing a park is worse
			// than a guest standing still, and because a test can read a record.
			default:
				UnansweredState = peep.State;

				// And say so, so that more than a test reads it.
				Unimplemented.Report( $"guest state {peep.State}" );

				break;
		}

		// <b>And the cell they are standing on is told, which is what makes the gate work at all.</b>
		// The original keeps a list of the things on each cell - head at the cell's +0x24, links on the
		// things themselves - and maintains it when a thing moves cell (FUN_0050b6a0), is created
		// (FUN_0050afe0) or destroyed. ParkState.StandOn answers the first two together.
		//
		// <b>This is called every turn, unconditionally, and that matters:</b> a guest the shipped park
		// leaves STANDING STILL is entered into their cell's list on their first turn, where a from/to move
		// guarded by "did the cell change" would never enter them and the gate could not see them.
		// StandOn decides for itself that the cell is unchanged, exactly as FUN_0050b6a0 does.
		//
		// <b>It lives here rather than in ParkPeople on purpose.</b> Putting it in the driver would leave
		// it unreachable from every test that calls Step directly. Every caller goes through Step, so
		// every caller keeps the list honest.
		var (standingX, standingY) = walk.Position.Cell;

		State.StandOn( peep.ThingId, standingX, standingY );
	}

	/// <summary>
	/// Whether this guest hurries to the gate - <b>a quarter of them do, by their thing id</b>.
	///
	/// <para>
	/// <c>FUN_004ff730</c> copies the person's own id out of the front of their block and tests
	/// <c>id &amp; 3</c>, taking the hurried speed when it is zero and none at all otherwise. It is not a
	/// mood or a need: the same guest hurries every time, and which quarter of the park hurries is fixed
	/// when the park is built.
	/// </para>
	/// <para>
	/// <b>No guest in Lost Kingdom exercises it.</b> The five heading for the gate are things 42, 39, 35, 31
	/// and 29, whose remainders are 2, 3, 3, 3 and 1 - so every one of them walks there unhurried, and a
	/// test that wants the other arm has to build a guest for it.
	/// </para>
	/// </summary>
	public static bool HurriesToTheGate( Peep peep )
	{
		ArgumentNullException.ThrowIfNull( peep );

		return (peep.ThingId & (GateHurryShare - 1)) == 0;
	}

	/// <summary>
	/// One turn of walking, and the animation that goes with it - the part of <c>FUN_004fa2a0</c> that runs
	/// whatever state asked for it.
	///
	/// <para>
	/// <b>Giving a guest a route here is a departure and it is still named.</b> The original sets a route on
	/// the way <i>into</i> a walking state, through <c>FUN_00510100</c>, as <see cref="SendTo"/> does here;
	/// but a guest restored from a file carries a destination and no route at all - the route being the one
	/// part of it the save deliberately does not keep. So the first turn of walking is what asks for one.
	/// </para>
	/// <para>
	/// <b>A guest who arrives enters a state</b>, and the state queues its own animation through
	/// <see cref="Peep.AnimationFor"/>, exactly as the original does.
	/// </para>
	/// </summary>
	private static WalkVerdict Walked( Peep peep, PeepWalk walk, SpriteScript? playing )
	{
		// A guest who cannot be given a route has given up, which is what the navigator itself records when
		// a search fails - so it is reported as such rather than as a turn that quietly did nothing.
		if ( !walk.HasRoute && (peep.Navigator.CannotReach || !walk.PlanRoute()) )
			return WalkVerdict.CannotReach;

		var verdict = walk.Step();

		if ( verdict != WalkVerdict.Walking )
			return verdict;

		var hurrying = peep.PurposeSpeed > Peep.UnhurriedSpeed;

		// FUN_004fa2a0 asks whether the sprite is ALREADY on the walk before asking for it, which is the
		// whole reason a jump must not change a script's identity: without that test a walking guest would
		// be restarted at the first picture on every single tick.
		var wanted = hurrying ? (int)PeepAnimation.HurriedWalk : (int)PeepAnimation.Walk;

		if ( playing != null && !playing.IsOn( wanted ) )
			peep.NextAnimation = wanted;

		peep.NextInterval = SpriteScript.IntervalFor( walk.LastStep.X, walk.LastStep.Y, hurrying );

		return verdict;
	}

	/// <summary>How long a guest who finds the price merely expensive sulks before judging it again.</summary>
	/// <remarks>
	/// <c>rand % 0x32 + 0x32</c>, written into the same field that waiting for the gate rolls 200 to 349
	/// into. Two states, one countdown - see <see cref="Peep.ParkOpeningWait"/>.
	/// </remarks>
	public const int SulkAtLeast = 50;

	/// <inheritdoc cref="SulkAtLeast"/>
	public const int SulkSpread = 50;

	/// <summary>
	/// What a guest makes of the admission fee, and what it makes them do - the switch in
	/// <c>FUN_004ff9d0</c>.
	///
	/// <para>
	/// <b>The cheap arm falls through into the about-right arm in the original</b>, which is why the two
	/// share their body here: being pleasantly surprised gains a guest some happiness and then they pay
	/// exactly as somebody who found it fair would.
	/// </para>
	/// <para>
	/// <b>Two fields the original touches on the leaving arms are deliberately not reproduced.</b> It
	/// writes 1 to <c>+0x188</c> and nought to <c>mExitLevel</c> (<c>+0x1bc</c>, <see cref="Peep.ExitLevel"/>).
	/// Nothing here reads <c>+0x188</c>, and a guest this sends home is past the one test here that reads
	/// the exit level.
	/// </para>
	/// </summary>
	private void Judge( Peep peep, PeepWalk walk, ParkAdmission admission, int tick )
	{
		var opinion = admission.OpinionAt( ParkExcitement, _random );

		switch ( opinion )
		{
			// "Person: park far too expensive" - they set off for the bus stop and give up on the park.
			case ParkAdmission.Opinion.FarTooExpensive:
				SendTo( peep, walk, EitherOf( admission.BusStopA, admission.BusStopB ) );
				peep.SetState( PeepState.HeadingForExit, tick, _random );

				break;

			// "Person: park on the expensive side" - they sulk, lose heart, and try again later. Only a
			// guest with no happiness left gives up, and they do so WITHOUT a destination: the original's
			// expensive arm reaches the shared tail without ever calling SetDest, unlike the arm above.
			case ParkAdmission.Opinion.OnTheExpensiveSide:
				peep.ParkOpeningWait = (_random.Next() % SulkSpread) + SulkAtLeast;
				peep.Happiness = Peep.Change( peep.Happiness, -admission.MediumHappinessChange );

				// The original tests the low byte of the truncated happiness, which cannot mislead here
				// because Peep.Change clamps it to 0..100 and so it never reaches 256.
				if ( (int)peep.Happiness == 0 )
					peep.SetState( PeepState.HeadingForExit, tick, _random );

				break;

			// "Person: park on the cheap side", then straight on into paying.
			case ParkAdmission.Opinion.OnTheCheapSide:
			case ParkAdmission.Opinion.AboutRight:
				if ( opinion == ParkAdmission.Opinion.OnTheCheapSide )
					peep.Happiness = Peep.Change( peep.Happiness, admission.MediumHappinessChange );

				// FUN_004d0600 - the fee goes on the balance and on the year's profit alike, which is
				// what ParkState.Take does: one call moving both.
				State.Take( admission.Fee );

				peep.PaidAdmission = true;
				peep.SetState( PeepState.WaitingForOpening, tick, _random );

				break;
		}
	}

	/// <summary>
	/// Waiting outside for the gate - <c>FUN_004ff7f0</c>.
	///
	/// <para>
	/// <b>A guest who has paid waits until the cell they are standing on names <i>them</i></b>: the original
	/// reads the short at <c>+0x24</c> of that cell's runtime record - the head of the cell's thing list,
	/// which <c>FUN_004d91f0</c> writes (<see cref="ParkState.EnterCell"/>) - and compares it against the
	/// guest's own thing id, which <c>FUN_0050b350</c> copies out of the front of the thing. That is the gate
	/// admitting one guest at a time; until the cell names them, a guest who has paid stands and waits.
	/// </para>
	/// </summary>
	private void Wait( Peep peep, PeepWalk walk, int tick )
	{
		if ( Admission is not { } admission )
			return;

		if ( !GateWillAdmit )
		{
			// Still shut. They put up with it for as long as they rolled and then head home.
			if ( peep.ParkOpeningWait != 0 )
			{
				--peep.ParkOpeningWait;
				return;
			}

			SendTo( peep, walk, EitherOf( admission.BusStopA, admission.BusStopB ) );
			peep.SetState( PeepState.HeadingForExit, tick, _random );

			return;
		}

		if ( !peep.PaidAdmission )
		{
			// Back to the booths to be charged. A guest already standing on one of the two keeps it,
			// which is the original's own order - it tests each booth against where they are before it
			// rolls for one.
			SendTo( peep, walk, BoothFor( peep, walk, admission ) );

			peep.ParkOpeningWait = 0;
			peep.SetState( PeepState.HeadingForGate, tick, _random );
		}

		// <b>And the paid arm.</b> A guest who has paid goes through when the cell they are standing on
		// NAMES THEM - the original reads a short at the cell's +0x24 and compares it with the guest's own
		// thing id.
		//
		// <b>That short is the head of the cell's thing list, not a reservation</b>: FUN_004d91f0 ends
		// `cell[0x24] = thing`, and FUN_004d9280 repairs it. So the test reads "am I the FIRST thing
		// standing here?", and THAT is the gate letting one guest through at a time - as each is admitted
		// and steps off the cell, whoever is behind them becomes the head. ParkState keeps the list; Step
		// maintains it as guests move.
		else if ( StandingOnTheirOwnCell( peep, walk ) )
		{
			SendTo( peep, walk, EitherOf( admission.EntranceA, admission.EntranceB ) );
			peep.SetState( PeepState.Entering, tick, _random );
		}
	}

	/// <summary>
	/// Whether the cell this guest is standing on names <b>them</b> as the first thing on it.
	/// </summary>
	/// <remarks>
	/// <b>The map check is asked rather than caught.</b> <see cref="ParkState.CellAt"/> throws off the map
	/// on purpose - a default cell would be silently writable and the write would go nowhere - and a guest
	/// who has wandered to the edge should simply not be admitted, not bring the park down.
	/// </remarks>
	private bool StandingOnTheirOwnCell( Peep peep, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

		return ParkState.OnMap( x, y ) && State.CellAt( x, y ).Occupant == peep.ThingId;
	}

	/// <summary>
	/// Which ticket booth a waiting guest heads for: the one they are standing on if it is either of them,
	/// and otherwise one of the two at random.
	/// </summary>
	private (int X, int Y) BoothFor( Peep peep, PeepWalk walk, ParkAdmission admission )
	{
		var standing = walk.Position.Cell;

		if ( standing == admission.TicketBoothA )
			return admission.TicketBoothA;

		if ( standing == admission.TicketBoothB )
			return admission.TicketBoothB;

		return EitherOf( admission.TicketBoothA, admission.TicketBoothB );
	}

	/// <summary>One of two cells, by the coin the original flips - <c>rand &amp; 1</c>.</summary>
	private (int X, int Y) EitherOf( (int X, int Y) first, (int X, int Y) second )
		=> (_random.Next() & 1) == 0 ? first : second;

	/// <summary>
	/// Sends a guest to the centre of a cell - the original's <c>FUN_004fa530</c>.
	///
	/// <para>
	/// <b>The centre, not the corner.</b> A route's waypoints are cell centres
	/// (<see cref="PeepNavigator.WaypointCentre"/>), which is why the shipped park's entering guests are
	/// saved walking to (47.5,17.5) rather than (47,17) - so a destination set at the corner would be half
	/// a cell away from where the pathfinder would ever put them.
	/// </para>
	/// <para>
	/// It answers whether a route was found. <see cref="ChooseSomewhereToGo"/>, the boarding arm of
	/// <see cref="QueueTurn"/>, the arrival's re-aim in <see cref="JoinTheQueue"/>,
	/// <see cref="FindQueueDestination"/> and <see cref="WanderFromNowhere"/> act on a failure at once; every
	/// other caller leaves it to <see cref="Walked"/>, which reports a guest who cannot get through as
	/// having given up on their next turn.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b><c>internal</c> rather than <c>private</c> so that a ride can use it too.</b>
	/// <see cref="ParkRideOperation.Dismiss"/> puts a guest down at the ride's exit, and the original does
	/// that through the same pair of steps this does - a destination, then a route. Widening one method is
	/// cheaper than a second way of moving a peep, which is how the two would drift apart.
	/// </remarks>
	internal static bool SendTo( Peep peep, PeepWalk walk, (int X, int Y) cell )
		=> SendTo( peep, walk, new FixedVector(
			PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) ) );

	/// <summary>
	/// Sends a guest to an exact point - the original's <c>FUN_004fa5f0</c>, which takes an 8.8 point where
	/// <c>FUN_004fa530</c> takes a cell's centre. The route runs to the point's cell and its last leg closes on
	/// the point itself (<see cref="PeepNavigator.NavigateTo"/>).
	/// </summary>
	private static bool SendTo( Peep peep, PeepWalk walk, FixedVector point )
	{
		peep.Navigator.Target = point;

		return walk.PlanRoute();
	}

	/// <summary>
	/// One turn in four is how often a guest who has finished wandering wanders again rather than stopping
	/// to think - <c>rand &amp; 3</c> in case 7 of <c>FUN_005019f0</c>.
	/// </summary>
	public const int KeepWanderingShare = 4;

	/// <summary>How many turns the original waits between a guest's decisions before it offers them a ride.</summary>
	public const int ThinkingGap = 30;

	/// <summary>
	/// The four sides in the order the original tests them, which is <b>not</b> compass order.
	/// <c>FUN_004f9490</c> reads the cell's connection bits as <c>0x10, 0x04, 0x01, 0x40</c>, and
	/// <see cref="CellEdge.BitFor"/> - derived separately, from the map - gives those to North, West, South
	/// and East. So slot and opposite slot differ by two, which is what makes the original's
	/// "do not turn back" test <c>(slot + 2) &amp; 3</c> correct.
	/// </summary>
	private static readonly StepDirection[] SlotOrder =
		[StepDirection.North, StepDirection.West, StepDirection.South, StepDirection.East];

	/// <summary>
	/// What a guest does when they finish anything - <c>FUN_004fec90</c>.
	///
	/// <para>
	/// <b>One roll decides, and the original takes it once at the top.</b> A third of the time a guest
	/// wanders somewhere, a third of the time they are offered a ride, and a third of the time they do
	/// nothing at all and think again next turn.
	/// </para>
	/// <para>
	/// <b>The ride arm.</b>
	/// <see cref="ChooseSomewhereToGo"/> asks <see cref="ParkRideChooser"/>, which walks the world's object
	/// list, filters it with <see cref="ParkRideChoice"/> and scores the survivors with
	/// <see cref="ParkRideScore"/> - the seven-term weighted mean of distance, queue, excitement, thirst,
	/// hunger, relief and illness, weighted by the seven <c>PeepInfo.DecisionVar…Weight</c> constants and
	/// multiplied for newness and for shelter in the rain. A guest who finds nothing worth more than nine
	/// still does nothing, which is the original's own answer rather than a shortfall in this one.
	/// </para>
	/// <para>
	/// <b>The leave test and the arms before the split are not all here.</b> The original leaves when the
	/// happiness byte is nought, when <c>mExitLevel</c> (<c>+0x1bc</c>) is exactly nought, or when the park has
	/// shut (<c>docs/exe/ride-operation.md</c>, "The state-6 turn, in order"); only the shut-park test is
	/// here, and <see cref="Step"/> sends a guest home on the exit level from any state (Q109). The arms
	/// before the split - a happy spot animation, vomit, litter, watching, pranks - are not built (Q111).
	/// </para>
	/// </summary>
	private void Decide( Peep peep, PeepWalk walk, int tick )
	{
		if ( Admission is not { } admission )
			return;

		// A park that has shut under them: they lose heart badly and set off for a bus stop.
		if ( ParkIsClosed )
		{
			peep.Happiness = Peep.Change( peep.Happiness, -admission.BigHappinessChange );

			SendTo( peep, walk, EitherOf( admission.BusStopA, admission.BusStopB ) );
			peep.SetState( PeepState.HeadingForExit, tick, _random );

			return;
		}

		switch ( _random.Next() % 3 )
		{
			// Wander off somewhere reachable. Failing to find anywhere leaves them deciding again, which
			// is the original's own answer - SetRandomDest reports it rather than throwing.
			case 1:
				if ( SetRandomDest( peep, walk ) )
					peep.SetState( PeepState.Wandering, tick, _random );

				peep.TimeStartedIdling = tick;

				break;

			// Being offered somewhere to go. The thirty-turn gate in front of it is what stops a guest being
			// offered a ride every third turn for ever, and it is measured from their own idle stamp.
			case 0:
				if ( peep.TimeStartedIdling + ThinkingGap >= tick )
					break;

				peep.TimeStartedIdling = tick;

				if ( ChooseSomewhereToGo( peep, walk, tick ) )
					peep.SetState( PeepState.GoingToRide, tick, _random );

				break;

			// And a third of the time, nothing happens at all.
			default:
				break;
		}
	}

	/// <summary>
	/// How far a ride's excitement may be from what a guest likes before they turn away at the queue -
	/// <c>FUN_004ffbc0</c> tests the signed difference against <b>44</b>, and prints "ride is not
	/// exciting enough" below and "ride is too exciting" above.
	/// </summary>
	/// <remarks>
	/// The sign is the original's own and is worth not tidying: <c>FUN_004fd4e0</c> returns
	/// <i>preferred minus actual</i>, clamped to fifty, negated - so a NEGATIVE answer means the guest
	/// wanted more excitement than the ride offers. Only the magnitude is used here, because both arms
	/// end the same way.
	/// </remarks>
	public const int ExcitementRefusal = 44;

	/// <summary>
	/// What a guest does on reaching something they chose - the arrival half of <c>FUN_004ffbc0</c>, in its order: the
	/// arrival test, the gates, the join, and the walk to their own place in the queue.
	///
	/// <para>
	/// <b>The arrival test</b> (<c>0x004ffc3d</c>): the guest must stand on the back-of-queue cell
	/// (<see cref="ParkRideChoice.QueueCellsFor"/>, <c>GetBackOfQueue</c>), which is where
	/// <see cref="ChooseSomewhereToGo"/> aimed them. Anywhere else, "The back of the queue has moved while I was
	/// walking here": they are aimed at its centre again and walk on in <see cref="PeepState.GoingToRide"/>, or,
	/// with no back of queue or no route to it, go back to deciding still naming the thing (<c>0x004ffe16</c>).
	/// </para>
	/// <para>
	/// <b>The gates.</b> A guest who fails one goes back to deciding with their choice let go of. The free-space
	/// gate is <c>FUN_004dda20</c>, <c>length &lt; mQueueSizeInCells * 4</c>, and the excitement gate is
	/// <see cref="TurnsAwayFrom"/>. The original's third, <c>FUN_004ddb60</c>, compares the length against a
	/// capacity from <c>FUN_004dda40</c> and is not built.
	/// </para>
	/// <para>
	/// <b>The walk</b> is <see cref="FindQueueDestination"/> (<c>0x004ffdad</c>). A guest who cannot get to their
	/// place - somebody in front of them has stopped queueing, or no route - leaves the queue they have just joined
	/// (<c>0x004ffdf4</c>).
	/// </para>
	/// </summary>
	private void JoinTheQueue( Peep peep, PeepWalk walk, int tick )
	{
		if ( peep.MajorDest == 0 || _park == null || Chosen( peep ) is not { } chosen )
		{
			GiveUpOnIt( peep, tick );

			return;
		}

		// GetBackOfQueue's pair (QueueCellsFor: the record's cached one until the queue is edited, else walked off the
		// map), which the chooser asks too, so the arrival test, the gate and the chooser measure one queue.
		var (backOfQueue, queueCells) = ParkRideChoice.QueueCellsFor( _park, chosen );
		var (x, y) = walk.Position.Cell;

		if ( MapStep.CellId( x, y ) != backOfQueue )
		{
			Log.Info( $"Person {peep.ThingId}: The back of the queue has moved while I was walking here" );

			if ( backOfQueue == 0 || !SendTo( peep, walk, MapStep.CellAt( backOfQueue ) ) )
				peep.SetState( PeepState.Deciding, tick, _random );

			return;
		}

		// Asked of the park as PLAYED, so a queue that filled up while this guest walked to it turns them away.
		if ( !ParkRideChoice.HasQueueRoom( State.QueueLength( chosen.ThingId ), queueCells )
			|| TurnsAwayFrom( peep, chosen ) )
		{
			GiveUpOnIt( peep, tick );

			return;
		}

		State.JoinQueue( chosen.ThingId, peep.ThingId );

		if ( !FindQueueDestination( peep, walk, chosen, tick ) )
		{
			Log.Info( $"Person {peep.ThingId}: Couldn't get to my place in the queue, leaving!" );
			PutOutOfTheQueue( peep, chosen, tick, "the join" );
		}
	}

	/// <summary>
	/// Walks a guest to their own place in the queue of <paramref name="queueing"/> - <c>FUN_00501160</c>,
	/// FindQueueDestination by its own log. See <c>docs/exe/ride-operation.md</c>, "Walking to a new place in the
	/// queue".
	/// </summary>
	/// <remarks>
	/// The place is where the queue walk finds them (<see cref="ParkState.PositionInQueue"/>); a guest it cannot
	/// reach answers false with nothing written. Otherwise the place's low byte goes into
	/// <see cref="Peep.QueuePos"/> before anything can fail, the jitter is drawn once, <see cref="ParkQueuePlace"/>
	/// turns the place into a point inside a cell, and a route to that exact point puts them in
	/// <see cref="PeepState.SteppingUpQueue"/>. No route answers false in the state they were in, and every caller
	/// then puts them out.
	/// <para>
	/// <b>The jitter draws from this behaviour's generator, not the engine's</b> (<c>FUN_00516330</c>, one sequence
	/// for the whole park), whose seed is not established: its range and its one draw a call are the original's,
	/// its sequence is not.
	/// </para>
	/// <para>
	/// The route (<c>FUN_004fa5f0</c>) first refuses a guest whose <c>mStrandedTime</c> no ground change near them
	/// has reached. Nothing here keeps that field; on these three paths it is nought unless a save loaded it.
	/// </para>
	/// </remarks>
	/// <returns>Whether a route to their place was found.</returns>
	private bool FindQueueDestination( Peep peep, PeepWalk walk, ParkWorld.CatalogueObject queueing, int tick )
	{
		var place = State.PositionInQueue( queueing.ThingId, peep.ThingId, _stillQueueing );

		if ( place < 0 )
			return false;

		peep.QueuePos = place & 0xff;

		var point = ParkQueuePlace.For( _park, queueing, place, ParkQueuePlace.Jitter( _random.Next() ) );

		// The original routes with whatever its stack held; this stands them at the cell's centre.
		if ( point.Cell != 0 && !point.Written )
			Unimplemented.Report( "QUEUE_PLACE_DODGY_DIRECTION" );

		// Cell nought, a place past the queue's cells, packs as (127, 255), which no route reaches.
		if ( point.Cell == 0 || !SendTo( peep, walk, point.Position ) )
		{
			Log.Info( $"Person {peep.ThingId}: QQQ - FindQueueDestination SetDest failed, so I'm standing in queue" );

			return false;
		}

		peep.SetState( PeepState.SteppingUpQueue, tick, _random );

		return true;
	}

	/// <summary>
	/// Rolls whether this visit gives the guest what they came for, as they enter the thing - the write
	/// <c>FUN_00501db0</c>'s case <c>0xe</c> makes, <c>person[+0x1f1] = FUN_004e2670( object )</c>.
	///
	/// <para>
	/// <b>This is the gate the whole of spending hangs on.</b> The settle-up splits on that byte: nought
	/// means the guest took nothing from the visit and loses happiness, anything else runs the item's
	/// effects.
	/// </para>
	/// <para>
	/// <b>It overwrites the guest's place in the queue, and that is the original's own overloading rather
	/// than a collision here.</b> The save reader names <c>+0x1f1</c> <c>mQueuePos</c> and the state setter
	/// writes the roll into the same byte; by this point the guest has already been called forward, so the
	/// place is spent. Nothing reads it as a position again before the settle-up reads it as an outcome.
	/// </para>
	/// <para>
	/// <b>Without a catalogue the roll is not made at all</b>, rather than defaulting to a win: an item
	/// nothing can describe has no chance to roll against, and the settle-up refuses the same case one step
	/// later for the same reason.
	/// </para>
	/// </summary>
	private void RollForTheVisit( Peep peep, ParkWorld.CatalogueObject entering )
	{
		if ( _catalogue == null || !_catalogue.TryGet( entering.CatalogueId, out var item ) )
			return;

		var succeeded = ParkRideOperation.Succeeds( item, _random );

		peep.QueuePos = succeeded ? 1 : 0;

		// And the script is told, so a sideshow can play the winning clip rather than the losing one.
		_tellTheScript?.Invoke( entering, succeeded ? 1 : 0 );
	}

	/// <summary>
	/// How far out of place a guest will tolerate being before they re-take their position at once
	/// rather than waiting out <see cref="Peep.QueueMoveDelay"/> - the <c>2</c> in <c>FUN_004ffff0</c>.
	/// </summary>
	public const int QueueDriftAllowed = 2;

	/// <summary>
	/// How many thing sweeps after a spot animation began a queuer only stands - <c>0x00500308</c>. Past it, their
	/// mood is read.
	/// </summary>
	public const int QueueMoodGap = 30;

	/// <summary>How long a queuer stands before boredom would take them - <c>0x00500415</c>. It never does: see <see cref="QueueTurn"/>.</summary>
	public const int QueueBoredAfter = 100;

	/// <summary>Happiness above which a queuer plays spot animation 5 - <c>CMP AL,0x50</c> at <c>0x0050031c</c>.</summary>
	public const int QueueHappyAbove = 80;

	/// <summary>Happiness from which a queuer's toilet is asked about - <c>CMP AL,0x14</c> at <c>0x00500330</c>.</summary>
	public const int QueueToiletFrom = 20;

	/// <summary>Happiness below which a queuer gives up unhappy - <c>CMP AL,0xa</c> at <c>0x00500334</c>.</summary>
	public const int QueueUnhappyBelow = 10;

	/// <summary>A toilet need above which a queuer leaves for a toilet - <c>CMP AL,0x50</c> at <c>0x005003a2</c>.</summary>
	public const int QueueToiletAbove = 80;

	/// <summary>
	/// How many places a thing with no queue path holds before a queuer is too far back -
	/// <c>FUN_004dda40</c>'s answer for a thing without <see cref="ParkWorld.CatalogueObject.QueuePathFlag"/>.
	/// </summary>
	public const int NoQueuePathCapacity = 100;

	/// <summary>
	/// One turn of a guest standing in a queue - <c>FUN_004ffff0</c>, its arms in its own order. Every arm that
	/// gives up ends the same way, <see cref="PutOutOfTheQueue"/>. See <c>docs/exe/ride-operation.md</c>, "The
	/// <c>InQueue</c> turn".
	/// </summary>
	/// <remarks>
	/// <list type="number">
	/// <item><b>Board.</b> At the front (<see cref="Peep.QueuePos"/>, a byte, nought), invited
	/// (<see cref="Peep.BeenAdmitted"/>) and the ride's nominee (<c>FUN_004e0aa0</c>): the invitation is cleared
	/// and they walk to the ride: the entry cell's centre, where the original aims at the stand point on the same
	/// cell (<c>FUN_004dedf0(0)</c>, the item's sub-cell offset turned by the facing). The original forgets them
	/// and puts them out when <c>FUN_004fa5f0</c> fails (<c>0x0050010a</c>), and that also fails without routing on a
	/// retry stamp at <c>+0x198</c> (<c>0x004fa62a</c>) nothing here keeps, so a failed route is counted and they go
	/// on.</item>
	/// <item><b>Wait.</b> At the front and invited but not the nominee: the whole turn is nothing (<c>0x005001d8</c>).</item>
	/// <item><b>The dirt gate</b> puts out a queuer for a toilet whose <c>+0x44</c> truncates below 25
	/// (<c>FUN_004e0390</c>). Nothing here keeps that field, whose meaning is not decoded, so it is counted.</item>
	/// <item><b>The lost place.</b> The queue walk cannot reach them - they are unlinked, or somebody in front has
	/// stopped queueing: put out. The original's log says it closes and reopens the ride; nothing does.</item>
	/// <item><b>In place</b>: too far back for the capacity (<c>FUN_004dda40</c>, counted for a queue path, whose
	/// descriptor pairing is not established), or a car track that is not valid, is put out; a coaster's track
	/// record is counted and let through, as the choice lets it through.</item>
	/// <item><b>Out of place</b>: more than <see cref="QueueDriftAllowed"/> out, or no delay left, re-takes the place
	/// - unless the ride is broken down (<c>FUN_004e0370</c>, state 1), when nothing is re-taken. Re-taking walks them
	/// to it (<see cref="FindQueueDestination"/>, <c>0x00500532</c>) and the mood still runs on the same turn; one who
	/// cannot get there is put out (<c>0x005004b3</c>). Otherwise a delay is spent.</item>
	/// <item><b>The mood</b>, read once <see cref="QueueMoodGap"/> sweeps have passed since a spot animation: above
	/// <see cref="QueueHappyAbove"/> and from <see cref="QueueUnhappyBelow"/> to 19 a spot animation (counted);
	/// from <see cref="QueueToiletFrom"/> to 80 with a toilet need above <see cref="QueueToiletAbove"/>, thought 4
	/// (counted), and out unless the thing is a toilet; below <see cref="QueueUnhappyBelow"/>, out - <b>held
	/// until Q85</b>, see below.</item>
	/// <item><b>Within the gap</b>, one turn in ten turns the heading (counted); boredom would put them out once
	/// <see cref="QueueBoredAfter"/> sweeps have passed since they began to stand, and <b>never fires</b>:
	/// <c>mTimeStartedIdling</c> is stamped on every return to the queue at least eleven sweeps after the spot
	/// animation that began the gap, so it cannot be a hundred past within thirty. It is counted, not built.</item>
	/// </list>
	/// <para>
	/// <b>The unhappy arm is counted, not built, and that is a deviation.</b> The original's new guest starts at
	/// happiness 50 (<c>FUN_004faec0</c>, <c>0x004fb075</c>); one arriving here starts at nought (Q85), so the arm
	/// would put every arrival out of every queue it joins. Alexah held it for Q85.
	/// </para>
	/// <para>
	/// <b>Spot animations are not built</b>, so <see cref="Peep.TimeOfLastSpotAnim"/> stays at nought, which the
	/// thing tick has passed by more than thirty once the lobby has run about seven seconds: a queuer's mood is read
	/// on every turn and the window is not reached, where the original's happy guest stands out an animation and the
	/// gap after it between readings.
	/// </para>
	/// </remarks>
	private void QueueTurn( Peep peep, PeepWalk walk, int tick )
	{
		if ( Chosen( peep ) is not { } queueing )
			return;

		var recorded = peep.QueuePos & 0xff;

		if ( recorded == 0 && peep.BeenAdmitted )
		{
			if ( State.PersonBeingLoaded( queueing.ThingId ) != peep.ThingId )
				return;

			peep.BeenAdmitted = false;

			if ( !SendTo( peep, walk, (queueing.EntryCellX, queueing.EntryCellY) ) )
				Unimplemented.Report( "QUEUE_BOARD_NO_ROUTE" );

			peep.SetState( PeepState.BeingAdmitted, tick, _random );

			return;
		}

		if ( queueing.IsToilet )
			Unimplemented.Report( "QUEUE_TOILET_DIRT_GATE" );

		var place = State.PositionInQueue( queueing.ThingId, peep.ThingId, _stillQueueing );

		if ( place < 0 )
		{
			Log.Info( $"Person {peep.ThingId}: Problem with a queue - shouldn't be fatal" );
			PutOutOfTheQueue( peep, queueing, tick );

			return;
		}

		if ( recorded == place )
		{
			if ( queueing.HasQueuePath )
			{
				Unimplemented.Report( "QUEUE_CAPACITY_RECHECK" );
			}
			else if ( recorded > NoQueuePathCapacity )
			{
				Unimplemented.Report( "QUEUE_TURN_THOUGHT_0x10" );
				PutOutOfTheQueue( peep, queueing, tick );

				return;
			}

			var track = TrackTypeOf( queueing );

			if ( track == ItemDescriptionFile.CoasterTrack )
			{
				Unimplemented.Report( "QUEUE_TURN_COASTER_TRACK_RECORD" );
			}
			else if ( track == ItemDescriptionFile.CarTrack && queueing.IsTrackRideValid == 0 )
			{
				Unimplemented.Report( "QUEUE_TURN_THOUGHT_0xD" );
				PutOutOfTheQueue( peep, queueing, tick );

				return;
			}
		}
		else if ( peep.QueueMoveDelay == 0 || (uint)(recorded - place) > QueueDriftAllowed )
		{
			if ( queueing.State != ParkRideChoice.StateRefusedOne && !FindQueueDestination( peep, walk, queueing, tick ) )
			{
				Log.Info( $"Person {peep.ThingId}: Couldn't get to my intended queue position" );
				PutOutOfTheQueue( peep, queueing, tick );

				return;
			}
		}
		else
		{
			--peep.QueueMoveDelay;
		}

		if ( (uint)(tick - peep.TimeOfLastSpotAnim) <= QueueMoodGap )
		{
			Unimplemented.Report( (uint)tick <= (uint)(peep.TimeStartedIdling + QueueBoredAfter)
				? "QUEUE_TURN_HEADING"
				: "QUEUE_TURN_BOREDOM" );

			return;
		}

		var happiness = (int)peep.Happiness & 0xff;

		if ( happiness > QueueHappyAbove )
		{
			Unimplemented.Report( "QUEUE_SPOT_ANIMATION" );

			return;
		}

		if ( happiness >= QueueToiletFrom )
		{
			if ( ((int)peep.Toilet & 0xff) <= QueueToiletAbove )
				return;

			Log.Info( $"Person {peep.ThingId}: needs the toilet, in the queue for {queueing.ThingId} "
				+ $"(happiness {peep.Happiness:0}, toilet {peep.Toilet:0})" );

			Unimplemented.Report( "QUEUE_TURN_THOUGHT_4" );

			if ( !queueing.IsToilet )
				PutOutOfTheQueue( peep, queueing, tick );

			return;
		}

		if ( happiness >= QueueUnhappyBelow )
		{
			Unimplemented.Report( "QUEUE_SPOT_ANIMATION" );

			return;
		}

		Unimplemented.Report( "QUEUE_TURN_UNHAPPY" );
	}

	/// <summary>
	/// Where every arm of <see cref="QueueTurn"/> that gives up ends - <c>0x0050049e</c> - and the join's and the
	/// door's failed walks to a place: the ride lets them go (<see cref="_leaveQueue"/>, <c>FUN_004ddd20</c>), they
	/// are put out (<see cref="DismissFromTheQueue"/>, <c>FUN_005012f0</c>), and the kids' sound plays at their feet
	/// for an id divisible by eight.
	/// </summary>
	/// <param name="by">What put them out, for the log.</param>
	private void PutOutOfTheQueue( Peep peep, ParkWorld.CatalogueObject queueing, int tick,
		string by = "their own turn" )
	{
		_leaveQueue?.Invoke( queueing, peep.ThingId );
		DismissFromTheQueue( peep, tick );

		Log.Info( $"People: guest {peep.ThingId} put out of thing {queueing.ThingId}'s queue by {by}, "
			+ $"now {peep.State} with happiness {peep.Happiness:0}" );

		ParkPeople.PutOffAtTheirFeet( peep, queueing );
	}

	/// <summary>What kind of track a thing runs on, from its item, or nought for one the catalogue does not know.</summary>
	private int TrackTypeOf( ParkWorld.CatalogueObject thing )
		=> _catalogue != null && _catalogue.TryGet( thing.CatalogueId, out var item ) ? item.TrackType : 0;

	/// <summary>The object this guest set off for, or null if the park no longer has it.</summary>
	/// <remarks>
	/// <b>It asks the park as PLAYED</b>, so that a thing bought this session is found.
	/// <see cref="JoinTheQueue"/> bails into <see cref="GiveUpOnIt"/> when this answers null, which clears
	/// <see cref="Peep.MajorDest"/> and returns the guest to <see cref="PeepState.Deciding"/> - so asking the
	/// file's list would leave a guest choosing a bought ride, walking to it and giving up on arrival, over
	/// and over, with nothing complaining.
	/// </remarks>
	private ParkWorld.CatalogueObject? Chosen( Peep peep )
		=> State.TryObject( peep.MajorDest, out var chosen ) ? chosen : null;

	/// <summary>Lets go of what they chose and thinks again, which is where every refusal above ends.</summary>
	private void GiveUpOnIt( Peep peep, int tick )
	{
		peep.MajorDest = 0;
		peep.SetState( PeepState.Deciding, tick, _random );
	}

	/// <summary>Which arm of <see cref="ThingRemoved"/> a guest took.</summary>
	internal enum PutOff
	{
		/// <summary>Their destination is something else, so the message changes nothing.</summary>
		No,

		/// <summary>They were riding it (state 16 exactly, <c>0x004fb38d</c>).</summary>
		Riding,

		/// <summary>They were queueing for it - <see cref="ParkRideOperation.IsQueueing"/>, <c>FUN_00502430</c>.</summary>
		Queueing,

		/// <summary>They were walking to it, walking away from it, or still naming it.</summary>
		Heading
	}

	/// <summary>
	/// A guest's answer to the object destructor's type-10 message, which every guest is sent when a thing
	/// is sold or picked up to be moved - <c>FUN_004fb360</c>. See <c>docs/exe/park-engine.md</c>, "Selling
	/// and the people on it".
	/// </summary>
	/// <remarks>
	/// <b>Only <see cref="Peep.MajorDest"/> chooses who answers</b> (<c>0x004fb383</c>), whatever their state.
	/// A queuer is first put out of the queue (<see cref="DismissFromTheQueue"/>); then everyone chosen loses
	/// <see cref="ParkAdmission.SmallHappinessChange"/>, lets the thing go and goes to
	/// <see cref="PeepState.Deciding"/>. <b>Nobody is moved.</b> A rider stays on the entry cell they boarded
	/// from, which is where their record has been all ride. The rider's arm also plays a sound, which the
	/// caller plays, since only it knows where the rider is drawn.
	/// <para>
	/// <b>Three things the original does here have nothing to act on.</b> Each arm writes an entry into the
	/// guest's event ring (0xd for a rider, 6 for a queuer), whose only reader is a debug dump. Every guest,
	/// chosen or not, also clears a saved second destination (<c>+0x1de</c>) and any <c>mPreviousRides</c>
	/// entry naming the thing. This project keeps none of the three.
	/// </para>
	/// </remarks>
	internal PutOff ThingRemoved( Peep peep, int thingId, int tick )
	{
		ArgumentNullException.ThrowIfNull( peep );

		if ( thingId == 0 || peep.MajorDest != thingId )
			return PutOff.No;

		var how = PutOff.Heading;

		if ( peep.State == PeepState.Riding )
		{
			how = PutOff.Riding;
		}
		else if ( ParkRideOperation.IsQueueing( peep ) )
		{
			how = PutOff.Queueing;
			DismissFromTheQueue( peep, tick );
		}

		if ( Admission is { } mood )
			peep.Happiness = Peep.Change( peep.Happiness, -mood.SmallHappinessChange );

		peep.MajorDest = 0;
		peep.SetState( PeepState.Deciding, tick, _random );

		return how;
	}

	/// <summary>
	/// Whether this guest, at the door, thinks the thing too expensive - see <see cref="PeepPriceOpinion"/>.
	/// With no catalogue, or an item it does not know, nothing can say what the thing is worth, and the
	/// price is left unjudged.
	/// </summary>
	private bool ThinksTooExpensive( Peep peep, ParkWorld.CatalogueObject thing )
		=> _catalogue != null && _catalogue.TryGet( thing.CatalogueId, out var item )
			&& PeepPriceOpinion.TooExpensive( peep, thing.PricePerUse, item );

	/// <summary>
	/// Walking away from a thing too expensive to board - <c>FUN_005006b0</c>'s first arm, <i>"Person %d:
	/// Object %d is too expensive, I'm leaving the queue"</i>: <see cref="ParkAdmission.MediumHappinessChange"/>
	/// off (<c>0x00500778</c>), the ride told to forget them and the queue left (<see cref="_walkAway"/>),
	/// then put out of the queue (<see cref="DismissFromTheQueue"/>, <c>0x005007b4</c>), which takes the same
	/// again. Thirty in this park.
	/// </summary>
	/// <remarks>
	/// Thought 6 and the object's walk-away count (<c>FUN_004e1670</c>: <c>mNumWalkAways</c> and the dword at
	/// <c>+0x230</c>) are counted; nothing here draws a thought or keeps either counter. The event-ring entry
	/// (event 10) is not kept, as for every other way out of a queue.
	/// </remarks>
	private void WalkAwayFromTheDoor( Peep peep, ParkWorld.CatalogueObject thing, int tick )
	{
		Log.Info( $"Person {peep.ThingId}: Object {thing.ThingId} is too expensive, I'm leaving the queue "
			+ $"(price {thing.PricePerUse}, cash {peep.Cash})" );

		Unimplemented.Report( "DOOR_PRICE_THOUGHT_6" );

		if ( Admission is { } mood )
			peep.Happiness = Peep.Change( peep.Happiness, -mood.MediumHappinessChange );

		Unimplemented.Report( "DOOR_WALK_AWAY_COUNT" );

		_walkAway?.Invoke( thing, peep.ThingId );
		DismissFromTheQueue( peep, tick );

		ParkPeople.PutOffAtTheirFeet( peep, thing );
	}

	/// <summary>
	/// Put out of a queue - <c>FUN_005012f0</c>: happiness down by
	/// <see cref="ParkAdmission.MediumHappinessChange"/>, both of the guest's own queue links, the
	/// invitation, the destination and the place in the queue cleared, and back to deciding.
	/// </summary>
	/// <remarks>
	/// The original has seven callers, and the other six unlink the guest from the thing's queue first
	/// (<c>FUN_004ddd20</c>). A sale does not, so the thing is not told: see
	/// <see cref="ParkState.ForgetQueueLinks"/>, which undoes the links of a guest who is still in them and
	/// does nothing to one who was unlinked already. All seven are built here: the sale, a queue measured shorter
	/// (<see cref="QueueShortened"/>), a closing ride's completion (<c>ParkPeople.CompleteOrTurnAway</c>), a price
	/// too high at the door (<see cref="WalkAwayFromTheDoor"/>), and, through <see cref="PutOutOfTheQueue"/>, the
	/// queuer's own turn and the failed walks to a place after joining and after a refused door
	/// (<c>docs/exe/ride-operation.md</c>, "Every way out of a queue").
	/// </remarks>
	internal void DismissFromTheQueue( Peep peep, int tick )
	{
		if ( Admission is { } mood )
			peep.Happiness = Peep.Change( peep.Happiness, -mood.MediumHappinessChange );

		State.ForgetQueueLinks( peep.ThingId );

		peep.BeenAdmitted = false;
		peep.MajorDest = 0;
		peep.QueuePos = 0;
		peep.SetState( PeepState.Deciding, tick, _random );
	}

	/// <summary>
	/// A queuer's answer to their queue being measured again - <c>FUN_00501390</c>, <i>"The queue was
	/// shortened and there's no room for me any more"</i>. See <see cref="ParkPeople.QueueRemeasured"/>,
	/// which asks everybody in the queue but the ride's nominee.
	/// </summary>
	/// <remarks>
	/// <b>A guest whose place is at or past four to a cell is put out</b> (<c>0x00501413</c>..<c>0x0050141c</c>),
	/// unless they are already <see cref="PeepState.EnteringRide"/> (<c>0x00501422</c>). The comparison is
	/// unsigned, so a place of -1 - a guest the walk could not reach, see
	/// <see cref="ParkState.PositionInQueue"/> - is past the end too. Going out is <c>FUN_004ddd20</c> and then
	/// <see cref="DismissFromTheQueue"/>: <see cref="ParkAdmission.MediumHappinessChange"/> and nothing else.
	/// The kids' sound is the caller's, which knows where the guest is drawn.
	/// <para>
	/// <b>One guest in three also thinks something</b> (thought <c>0xd</c> when the id divides by three,
	/// <c>0x0050148a</c>), and nothing here draws a thought, so it is counted.
	/// </para>
	/// </remarks>
	/// <param name="place">Where the queue walk found them, or -1.</param>
	/// <param name="room">How many the queue now holds: its cells, times four.</param>
	/// <param name="script">The ride's script, whose admission slot is emptied if it names them.</param>
	/// <returns>Whether they were put out.</returns>
	internal bool QueueShortened( Peep peep, int place, int room, RideScript? script, int tick )
	{
		ArgumentNullException.ThrowIfNull( peep );

		if ( peep.MajorDest == 0 || (uint)place < (uint)room || peep.State == PeepState.EnteringRide )
			return false;

		if ( peep.ThingId % 3 == 0 )
			Unimplemented.Report( "QUEUE_SHORTENED_THOUGHT_0xD" );

		ParkRideOperation.LeaveQueue( State, script, peep.MajorDest, peep.ThingId );
		DismissFromTheQueue( peep, tick );

		return true;
	}

	/// <summary>
	/// Whether the ride is too far from what this guest likes - see <see cref="ExcitementRefusal"/>.
	/// </summary>
	/// <remarks>
	/// <b>An item declaring no excitement is never refused</b>, which is the original's own gate rather
	/// than a guard against missing data: <c>FUN_004e0860(1)</c> decides whether the comparison happens at
	/// all, and it is the same test that drops the excitement weight out of the ride scorer.
	/// </remarks>
	private bool TurnsAwayFrom( Peep peep, ParkWorld.CatalogueObject chosen )
	{
		if ( _catalogue == null || !_catalogue.TryGet( chosen.CatalogueId, out var item )
			|| item.ExcitementLevel == 0 )
			return false;

		var wanted = _chooser.Score.PreferredExcitementFor( peep.PersonType );

		return Math.Abs( wanted - item.ExcitementLevel ) > ExcitementRefusal;
	}

	/// <summary>
	/// What the chooser answers for one guest and where it would aim them - one of the two halves
	/// <see cref="ChooseSomewhereToGo"/> collapses into a single bool. Whether they got a route is read from
	/// their own state and <see cref="Peep.MajorDest"/>, not tested here - see the note inside.
	/// </summary>
	/// <remarks>
	/// <b>It exists because no census here can tell those halves apart.</b> <see cref="Peep.MajorDest"/>
	/// is written only after <see cref="PeepWalk.PlanRoute"/> succeeds, so a guest who chooses somewhere
	/// and cannot route to it leaves no trace whatever - and an empty <c>dest</c> census then reads
	/// exactly like "nothing was ever chosen". The two want opposite fixes.
	/// <para>
	/// It makes the SAME call <see cref="ChooseSomewhereToGo"/> makes, rather than asking the question its
	/// own way: a census that recomputes is not an observation.
	/// </para>
	/// </remarks>
	internal string Explain( Peep peep, PeepWalk walk, int tick )
	{
		var (x, y) = walk.Position.Cell;

		var wants = new ParkRideScore.Wants( peep.PersonType,
			peep.Thirst, peep.Hunger, peep.Toilet, peep.Vomit );

		if ( _chooser.ChooseFor( wants, x, y, tick, queueLength: o => State.QueueLength( o.ThingId ) )
			is not { } chosen )
			return $"at ({x},{y}) the chooser picked NOTHING";

		// <b>THE ROUTE IS DELIBERATELY NOT TESTED HERE, and both ways of testing it were wrong.</b>
		//
		// Asking <c>CellRoute.Reaches</c> - the accessible static - measures the wrong thing: it is a
		// straight-LINE test, the one route straightening uses, not the pathfinder. It answered OPEN
		// only for a guest already standing beside the queue and SHUT from everywhere else, which reads
		// exactly like "nothing can reach it" and means "nothing can see it in a clear line".
		//
		// Calling the real <c>PlanRoute</c> is worse, because it PERTURBS: <c>Renavigate</c> rebuilds
		// the route through <c>NavigateTo</c> and zeroes the steering's last-progress mark on failure,
		// and restoring <c>Navigator.Target</c> afterwards undoes neither. A census that re-planned
		// every guest every few seconds would steer the park it is supposed to be watching, and any
		// boarding it then saw would be its own doing.
		//
		// So this answers the half it can answer honestly. The other half is read from the guest's own
		// state and <see cref="Peep.MajorDest"/>, which the simulation writes for itself.
		var (aimX, aimY) = MapStep.CellAt( ParkRideChoice.QueueCellsFor( _park, chosen ).BackOfQueue );

		return $"at ({x},{y}) chose thing {chosen.ThingId} aim ({aimX},{aimY}) dest {peep.MajorDest}";
	}

	/// <summary>
	/// Offers this guest the best thing in the park and sets them off for it - <c>FUN_004fcb10</c>.
	///
	/// <para>
	/// <b>The walk is committed to only once a route exists</b>, the order <c>StaffBehaviour.GoAndRest</c>
	/// follows too: a guest never claims somewhere they cannot get to, and a best candidate that cannot be
	/// reached leaves them deciding again next turn. The original routes every candidate that beats the
	/// best as it walks them, so a better one that cannot be routed leaves the walker failed under the
	/// earlier winner's name (Q104).
	/// </para>
	/// <para>
	/// <b>The chosen thing is recorded in <see cref="Peep.MajorDest"/></b> - the person's own
	/// <c>+0x1dc</c>, which is where the original writes it and which the queueing states read back. It is
	/// written after the route for the same reason the state is.
	/// </para>
	/// <para>
	/// <b>Queue lengths ARE passed, and are measured from the park as played rather than as saved.</b> The
	/// save leaves <c>mFirstInQ</c> at nought on every object - nobody had ever queued in it - so every
	/// queue starts genuinely empty; but guests join them, so the length has to be read live.
	/// </para>
	/// </summary>
	/// <returns>Whether somewhere was chosen and a route to it planned.</returns>
	private bool ChooseSomewhereToGo( Peep peep, PeepWalk walk, int tick )
	{
		// Whatever they named before is let go of first, chosen or not (FUN_004fcb10, 0x004fcb21).
		peep.MajorDest = 0;

		var (x, y) = walk.Position.Cell;

		var wants = new ParkRideScore.Wants( peep.PersonType,
			peep.Thirst, peep.Hunger, peep.Toilet, peep.Vomit );

		// Queues are measured from the park as it is being PLAYED, not as it was saved - a guest who joined
		// one a moment ago has to count.
		if ( _chooser.ChooseFor( wants, x, y, tick, queueLength: o => State.QueueLength( o.ThingId ) )
			is not { } chosen )
			return false;

		// Aimed at the centre of the back-of-queue cell (0x004fcbc4), where JoinTheQueue's arrival test asks them to
		// stand. The chooser offers nothing without one.
		var (backOfQueue, _) = ParkRideChoice.QueueCellsFor( _park, chosen );

		if ( backOfQueue == 0 || !SendTo( peep, walk, MapStep.CellAt( backOfQueue ) ) )
			return false;

		peep.MajorDest = chosen.ThingId;

		return true;
	}

	/// <summary>
	/// Sends a guest somewhere to wander to - <c>FUN_004f9490</c>, whose own log line is "Peep can't
	/// SetRandomDest anywhere" (<c>docs/exe/ride-operation.md</c>, "SetRandomDest").
	///
	/// <para>
	/// <b>It first counts the links of the cell the guest stands on</b> (<c>0x004f95b9</c>). None - a cell
	/// a sale has cleared, a lone path cell - takes <see cref="WanderFromNowhere"/>, the nearest path on
	/// seven rays and then five random cells near by. The original takes that arm for any person; this is
	/// the guest's half, and the staff's is <see cref="StaffBehaviour"/>'s.
	/// </para>
	/// <para>
	/// <b>A linked cell sends them to one of its four neighbours</b>, a random point INSIDE it rather than
	/// its centre: the original masks a roll to <c>0x7f</c> and clamps it to 5..123 of the 256 sub-cell
	/// units. Its linked arm walks one to five cells from the mask of the cell being left, with three
	/// filters; this steps one cell from the entered cell's mask (Q108).
	/// </para>
	/// <para>
	/// <b>The stranded bookkeeping is absent</b> (Q110): the original refuses a guest whose stamp says
	/// nothing near them has changed, stamps one who reaches a dead end and raises a thought bubble, and
	/// neither the stamp nor the thought system exists here, so a guest who can reach nowhere stays where
	/// they are and is asked again.
	/// </para>
	/// </summary>
	/// <returns>Whether somewhere was found and a route to it planned.</returns>
	internal bool SetRandomDest( Peep peep, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

		// The original's pass count, r % 5 + 1, drawn on every call before the count (0x004f9534). Only its linked
		// walk reads it, and ours steps one cell (Q108), so it is drawn and not used.
		_ = (_random.Next() % 5) + 1;

		// A park that was never loaded has no cells to count, and is taken as linked - the answer Connects gives.
		if ( _park != null && CellEdge.Links( ParkState.CellFor( _park, x, y ).Neighbours ) == 0 )
			return WanderFromNowhere( peep, walk, x, y );

		// The four candidates, in the original's slot order, empty where that side is closed.
		var candidates = new (int X, int Y)?[SlotOrder.Length];
		var found = 0;

		for ( var slot = 0; slot < SlotOrder.Length; ++slot )
		{
			if ( walk.Blocked( x, y, SlotOrder[slot] ) )
				continue;

			var step = MapStep.Beyond( x, y, SlotOrder[slot] );

			if ( !Connects( step.X, step.Y, SlotOrder[slot] ) )
				continue;

			candidates[slot] = step;
			++found;
		}

		if ( found == 0 )
			return false;

		// Start at a random slot and take the first one open, which is how the original spreads guests
		// across the ways out of a cell rather than always preferring north.
		var first = _random.Next() & (SlotOrder.Length - 1);

		for ( var step = 0; step < SlotOrder.Length; ++step )
		{
			var slot = (first + step) & (SlotOrder.Length - 1);

			if ( candidates[slot] is not { } cell )
				continue;

			peep.Navigator.Target = new FixedVector( SomewhereIn( cell.X ), SomewhereIn( cell.Y ) );

			return walk.PlanRoute();
		}

		return false;
	}

	/// <summary>
	/// SetRandomDest's arm for a cell with no links, <c>0x004f9a05</c>..<c>0x004f9d5f</c>
	/// (<c>docs/exe/ride-operation.md</c>, "SetRandomDest", the no-links arm).
	///
	/// <para>
	/// <b>First the nearest path</b>: each of <see cref="NoLinksProbes"/> that is path (type 1,
	/// <c>FUN_00536310</c>) is aimed at its centre and routed, and a route that fails moves on to the next
	/// probe. <b>Then five random cells</b> within five of the guest, x drawn before y, of any type; a draw
	/// off the map uses a try. Five failures answer nought, and the original stamps, thinks and logs nothing.
	/// </para>
	/// </summary>
	private bool WanderFromNowhere( Peep peep, PeepWalk walk, int x, int y )
	{
		var probe = 0;

		foreach ( var (atX, atY) in NoLinksProbes( x, y ) )
		{
			++probe;

			if ( ParkState.CellFor( _park, atX, atY ).Type != CellEdge.Path )
				continue;

			if ( SendTo( peep, walk, (atX, atY) ) )
			{
				Log.Info( $"Peep {peep.ThingId}: no links at ({x},{y}); probe {probe} aims at path ({atX},{atY})" );

				return true;
			}
		}

		for ( var attempt = 1; attempt <= NowhereTries; ++attempt )
		{
			var toX = x + (_random.Next() % 11) - 5;
			var toY = y + (_random.Next() % 11) - 5;

			if ( !ParkState.OnMap( toX, toY ) )
				continue;

			if ( SendTo( peep, walk, (toX, toY) ) )
			{
				Log.Info( $"Peep {peep.ThingId}: no links at ({x},{y}) and no path on the rays; try {attempt} aims at "
					+ $"({toX},{toY}), type {ParkState.CellFor( _park, toX, toY ).Type}" );

				return true;
			}
		}

		Log.Info( $"Peep {peep.ThingId}: no links at ({x},{y}), no path on the rays and {NowhereTries} tries failed" );

		return false;
	}

	/// <summary>How many random cells the no-links arm tries once its probes find no path - <c>0x004f9d19</c>.</summary>
	public const int NowhereTries = 5;

	/// <summary>
	/// The cells the no-links arm probes for path, in its order, those off the map left out: direction d 0..7
	/// outside, distance k 0..3 inside, through the table at <c>0x004f9e40</c>.
	///
	/// <para>
	/// <b>The table's quirks are the original's.</b> Direction 0 leaves both offsets nought, as does every
	/// k 0, so the guest's own cell is probed eleven times; the other seven rays are (k, k), (k, 0), (k, −k),
	/// (0, −k), (−k, −k), (−k, 0) and (−k, k), so <b>nothing is probed at (0, +k)</b>. The probe is the own
	/// cell id plus dy × 128 + dx in sixteen bits, so an x past column 0 or 127 wraps into the next row.
	/// </para>
	/// </summary>
	internal static IEnumerable<(int X, int Y)> NoLinksProbes( int x, int y )
	{
		var own = MapStep.CellId( x, y );

		for ( var direction = 0; direction < 8; ++direction )
		{
			for ( var k = 0; k < 4; ++k )
			{
				var (dx, dy) = direction switch
				{
					1 => (k, k),
					2 => (k, 0),
					3 => (k, -k),
					4 => (0, -k),
					5 => (-k, -k),
					6 => (-k, 0),
					7 => (-k, k),
					_ => (0, 0)
				};

				var (atX, atY) = MapStep.CellAt( own + (dy * MapStep.MapSize) + dx );

				if ( ParkState.OnMap( atX, atY ) )
					yield return (atX, atY);
			}
		}
	}

	/// <summary>
	/// Whether the cell being entered says it connects the way we are coming from - the stored
	/// <c>mNeighbours</c> mask, bit-tested, which is what the original builds its wander candidates from.
	///
	/// <para>
	/// <b>The mask keeps a linked wander off the road outside.</b> The original picks a wander destination in
	/// <c>FUN_004f9490</c> from the byte <c>FUN_00522770</c> hands back - the cell's own <c>+0xc</c> - and
	/// <b>only 91 of this park's 16,384 cells carry a non-zero one</b>. The road outside is cell type 30 and
	/// its mask is nought, so the engine can never choose it, however walkable its edges are.
	/// </para>
	/// <para>
	/// <b>The mask is read from the cell being ENTERED, about the side facing the cell being left</b>, and
	/// the bit is set when the two connect - see <see cref="MapStep"/>, which records why that reading and
	/// its mirror image cannot be told apart by measuring this park.
	/// </para>
	/// <para>
	/// <b>This narrows rather than replaces.</b> The original uses the mask <i>instead of</i> an edge test
	/// here; keeping both means the mask can only ever close a way and never open one, so no route this
	/// build already walks can be widened by it. A park that was never loaded has no cells to ask, and
	/// answers yes - the same thing every other null-park arm in this class does.
	/// </para>
	/// </summary>
	private bool Connects( int x, int y, StepDirection direction )
	{
		if ( _park == null )
			return true;

		if ( !ParkState.OnMap( x, y ) )
			return false;

		// The RUNNING park's mask, not the file's. A path a player has just laid carries its connections
		// only in the overlay, and asking ParkWorld would answer nought for every one of them - so a new
		// walkway would draw perfectly and no guest would ever wander onto it.
		var cell = ParkState.CellFor( _park, x, y );

		return (cell.Neighbours & CellEdge.BitFor( direction )) != 0;
	}

	/// <summary>The near edge of a cell plus a clamped roll - see <see cref="SetRandomDest"/>.</summary>
	private int SomewhereIn( int cell )
	{
		var within = Math.Clamp( _random.Next() & 0x7f, 5, 0x7b );

		// A cell is 256 of these sub-units and One is a whole cell, so a sub-unit is One / 256.
		return (cell * PeepNavigator.One) + (within * (PeepNavigator.One / 256));
	}
}
