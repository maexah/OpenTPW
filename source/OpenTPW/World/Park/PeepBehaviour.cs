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
	/// <param name="balance">
	/// The park's balance stack, which the chooser's score reads its weights, its multipliers and each guest
	/// type's <c>PeepTypes[n].PreferredExcitement</c> from - the same byte the arrival's excitement refusal
	/// reads (<c>FUN_004fd4e0</c>, <c>0x004fd50a</c>). Null scores with <see cref="ParkRideScore"/>'s own
	/// fallbacks, which prefer 50 for every type.
	/// </param>
	public PeepBehaviour( IParkInitialState? park, Random? random = null,
		ParkAdmission? admission = null, Func<int>? gateStatus = null, ParkState? state = null,
		ParkItemCatalogue? catalogue = null,
		Func<ParkWorld.CatalogueObject, int, bool>? admit = null,
		Func<ParkWorld.CatalogueObject, int, bool>? finishAdmission = null,
		Action<ParkWorld.CatalogueObject, int>? tellTheScript = null,
		Action<ParkWorld.CatalogueObject, int>? walkAway = null,
		Action<ParkWorld.CatalogueObject, int>? leaveQueue = null,
		Func<int, bool>? stillQueueing = null,
		ParkBalance? balance = null )
	{
		_admit = admit;
		_finishAdmission = finishAdmission;
		_tellTheScript = tellTheScript;
		_walkAway = walkAway;
		_leaveQueue = leaveQueue;
		_stillQueueing = stillQueueing;

		// Zero is open, one is shut - see ParkWorld.ParkClosed. ParkState
		// applies that rule itself, so it is not repeated here.
		State = state ?? new ParkState( park );
		Admission = admission;
		_gateStatus = gateStatus;
		_random = random ?? new Random();
		// The state goes in so the chooser walks the RUNNING park's object chain: something bought this
		// session is in that one and in no other, and a guest is never offered what the walk cannot reach.
		_chooser = new ParkRideChooser( park, catalogue, new ParkRideScore( balance ), State );
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

	/// <summary>What each kind of guest likes, which a visit's settle-up measures the thing against.</summary>
	public ParkRideScore Score => _chooser.Score;

	/// <summary>
	/// The park these guests are in, for the two questions joining a queue asks of it: which object they
	/// chose, and where its queue ends. Null leaves a guest unable to join one, which is the same answer
	/// a null park gives everywhere else here.
	/// </summary>
	private readonly IParkInitialState? _park;

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
	internal IParkInitialState? Park => _park;

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
	/// <c>mBalance</c> and <c>mProfitThisYear</c> by the same amount (<c>FUN_004d0600</c>, which also adds it to
	/// the park analyser's month cash in and gate takings), and <see cref="ParkState.Take"/> moves the balance, the
	/// year's profit and this together. The park's money on screen is <see cref="ParkState.Balance"/>.
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
	/// How many guests share each hurried one on the way to the gate: <see cref="HurriesToTheGate"/> takes the
	/// hurry for an id whose low two bits are nought, when the bus is not leaving (<see cref="GateHurry"/>).
	/// </summary>
	public const int GateHurryShare = 4;

	/// <summary>
	/// The bus's <c>VAR_STATUS</c> while it pulls away from the stop - <c>bus.RSE</c> sets it once it is let go
	/// after a load (instruction 57) and holds it through its leaving clip and a second more.
	/// </summary>
	public const int BusIsLeaving = 3;

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
	/// <see cref="HeldByAThing(PeepState)"/> for a guest: one playing a spot animation is held when the state
	/// they will return to is, as <see cref="ParkRideOperation.IsQueueing"/> reads it (<c>FUN_00502430</c>), so
	/// a queuer in the middle of a jump is still their queue's.
	/// </summary>
	internal static bool HeldByAThing( Peep peep )
		=> HeldByAThing( peep.State == PeepState.PlayingSpotAnimation ? peep.SavedState : peep.State );

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
	/// <see cref="Decide"/>. What is absent sits inside those - the arms before Decide's split (Q111) - and
	/// each is recorded where it happens rather than here.
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
				peep.PurposeSpeed = GateHurry( peep, BusStatus() );

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

					// "The person has become stuck on their way to the ride they were interested in"
					// (0x004ffe7a): BigHappinessChange off, the thing let go of, and they think again. The
					// event it pushes first (3) is counted: this project keeps no event ring. The saved major
					// stays (0x004ffe9f clears +0x1dc alone).
					case WalkVerdict.CannotReach:
						Log.Info( $"Person {peep.ThingId}: stuck on the way to thing {peep.MajorDest}, tick {tick}" );

						Unimplemented.Report( "GOING_TO_RIDE_STUCK_EVENT" );

						LoseHeartOnTheWay( peep, tick );
						break;

					case WalkVerdict.Walking:
						WalkOn( peep, walk, tick );
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
						SetWandering( peep, walk, tick );
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
			// THE PARK'S LOOP. Arriving at the cell beyond the ride's exit drops them back into Deciding, which is
			// what lets a guest who has had one go go and have another.
			//
			// ParkRideOperation.Dismiss sets this state from the ride's own turn.
			//
			// <b>The destination is cleared on the stuck arm only, and that asymmetry is the original's.</b>
			// FUN_00500900 zeroes +0x1dc when the walk reports it cannot get through, and on arrival keeps
			// it while it logs "Person %d: successfully left rid[e]". Deciding's ride arm clears MajorDest
			// before it chooses, so the kept id lasts until then - and until then a sale of that ride still
			// puts them off, as it does in the original.
			//
			// Arriving, a guest the minor decision turned aside on their way here is sent on to what they were
			// bound for, unscored (SentOnToTheSavedMajor). Giving up leaves that saved major for the next walk off
			// anything (0x00500a3b clears +0x1dc alone).
			case PeepState.LeavingRide:
				switch ( Walked( peep, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						if ( !SentOnToTheSavedMajor( peep, walk ) )
						{
							Log.Info( $"Person {peep.ThingId}: successfully left ride {peep.MajorDest}, becoming idle" );
							peep.SetState( PeepState.Deciding, tick, _random );
						}
						else
						{
							peep.SetState( PeepState.GoingToRide, tick, _random );
						}

						break;

					case WalkVerdict.CannotReach:
						Log.Info( $"Person {peep.ThingId}: couldn't walk off ride {peep.MajorDest} "
							+ $"(saved major {peep.SavedMajorDest})" );
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

			// Heading for the exit, having decided not to stay - FUN_00500a50. They walk to the cell the arm
			// that sent them here chose, and on arriving go on to pick a cell outside.
			//
			// <b>The change-of-mind arm is absent.</b> The original turns a guest back to Deciding - "Make
			// up your mind!" - when mExitLevel (+0x1bc) is positive AND the happiness byte (+0x19c) is
			// non-zero AND the park is open AND FUN_004fa990 agrees.
			//
			// Getting stuck prints "I'm stuck in the park, even though it's closed!!" and puts them back to
			// Deciding, whose leave test sends them off again if it still holds.
			case PeepState.HeadingForExit:
				switch ( Walked( peep, walk, playing ) )
				{
					case WalkVerdict.Arrived:
						peep.SetState( PeepState.PickingACellOutside, tick, _random );

						break;

					case WalkVerdict.CannotReach:
						Log.Info( $"Person {peep.ThingId}: stuck on the way out, deciding again, tick {tick}" );

						peep.SetState( PeepState.Deciding, tick, _random );

						break;

					default:
						break;
				}

				break;

			// Their day is done and nothing is holding them - ParkPeople.Depart takes them out of the
			// park on the next sweep. It is terminal rather than a no-op: this class owns what a guest
			// wants and never owns the list they are in, so the removal belongs to whoever does.
			case PeepState.Leaving:
				break;

			// Playing a spot animation - FUN_005019f0 case 8 (0x00501d26): they stand, with no walk tick,
			// until the sweep counter is more than SpotAnimationSweeps past the stamp, then enter the state
			// PlaySpotAnimation saved through its own SetState (FUN_004fc890), which asks for that state's
			// animation and, for the queue, stamps the idling time and derives the move delay again.
			case PeepState.PlayingSpotAnimation:
				if ( (uint)tick > (uint)(peep.TimeOfLastSpotAnim + SpotAnimationSweeps) )
				{
					if ( peep.SavedState == PeepState.Wandering )
						SetWandering( peep, walk, tick );
					else
						peep.SetState( peep.SavedState, tick, _random );

					Log.Info( $"Person {peep.ThingId}: spot animation over at tick {tick}, back to {peep.State}" );
				}

				break;

			// <b>Three states answered by standing still, each saying what it waits on.</b> Nothing here sets
			// 9; HeadingForExit and WalkingOutside set 19 and 21, and ParkPeople takes a guest in 19 out
			// of the park. A case that breaks looks exactly like a missing case on screen - the guest stands
			// still either way - so the difference has to be written down, and UnansweredState is what lets
			// the program itself tell them apart.
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
	/// The hurry a guest heading for the gate is given this turn - <c>FUN_004ff730</c>, before it walks them: the
	/// words at <c>0x0075c7f0</c>, <b>50</b> while the bus reports <see cref="BusIsLeaving"/> ("The bus is coming!
	/// RUUUUUUUUUUN!!!!", <c>0x0075d914</c>), else 25 for <see cref="HurriesToTheGate"/>, else nought.
	/// <paramref name="busStatus"/> is <see cref="BusStatus"/>: any other vehicle, or none, is never run for.
	/// </summary>
	public static int GateHurry( Peep peep, int busStatus )
	{
		ArgumentNullException.ThrowIfNull( peep );

		if ( busStatus == BusIsLeaving )
			return Peep.RunningForTheBusSpeed;

		return HurriesToTheGate( peep ) ? Peep.HurryingSpeed : Peep.UnhurriedSpeed;
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
	/// <b>The leaving arms zero <c>mExitLevel</c></b> (<c>+0x1bc</c>, <see cref="Peep.ExitLevel"/>) after the state,
	/// as <see cref="Wait"/>'s does, so a guest put back to Deciding by a walk that sticks leaves again on that
	/// turn (<see cref="WantsToLeave"/>). The 1 the far-too-expensive arm writes to <c>+0x188</c>, the walking
	/// mode, is not reproduced: every walk here is mode 0 (<see cref="ParkPeople.WalkingMode"/>). Both arms aim
	/// at a bus stop where the original's aim at <c>FUN_004d86d0</c>'s cells, the crossing's park side (Q128).
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
				peep.ExitLevel = 0;

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
				{
					peep.SetState( PeepState.HeadingForExit, tick, _random );
					peep.ExitLevel = 0;
				}

				break;

			// "Person: park on the cheap side", then straight on into paying.
			case ParkAdmission.Opinion.OnTheCheapSide:
			case ParkAdmission.Opinion.AboutRight:
				if ( opinion == ParkAdmission.Opinion.OnTheCheapSide )
					peep.Happiness = Peep.Change( peep.Happiness, admission.MediumHappinessChange );

				// FUN_004d0600 - the fee goes on the balance and on the year's profit alike, which is
				// what ParkState.Take does, with the gate's running total beside them.
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
			peep.ExitLevel = 0;

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
	/// It answers whether a route was found. <see cref="ChooseSomewhereToGo"/> passes over a candidate that fails
	/// and leaves the walker as the last asking left it; the boarding arm of
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

	/// <summary>How many sweeps past a guest's idle stamp the sweep counter must be before the chooser is asked (<c>0x004ff425</c>).</summary>
	public const int ThinkingGap = 30;

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
	/// <see cref="ParkRideScore"/>, whose summary lists its steps. A guest who finds nothing worth more than nine,
	/// or nothing they can route to, plays spot animation 4 (<see cref="PlaySpotAnimation"/>), loses
	/// <c>SmallHappinessChange</c> and has their idle stamp set, so the chooser is not asked again for
	/// <see cref="ThinkingGap"/> sweeps; the event either answer pushes is counted.
	/// </para>
	/// <para>
	/// <b>The leave test comes before the split</b> (<see cref="WantsToLeave"/>, <see cref="Leave"/>), and a guest
	/// it finds no route for goes on into the split, still deciding. The turn's one draw serves both: its low bit
	/// orders the leaver's two cells and its remainder picks the split's arm (<c>0x004fecb4</c>).
	/// </para>
	/// <para>
	/// <b>The other arms before the split are not here.</b> Spot animation 5 above happiness 80, the vomit and its
	/// spot animation 7, and litter each return before the leave test in the original; watching and pranks follow
	/// it. None is built (Q111).
	/// </para>
	/// </summary>
	private void Decide( Peep peep, PeepWalk walk, int tick )
	{
		if ( Admission is not { } admission )
			return;

		var draw = _random.Next();

		if ( WantsToLeave( peep ) && Leave( peep, walk, admission, draw, tick ) )
			return;

		switch ( draw % 3 )
		{
			// Wander off somewhere reachable. A route sets them wandering and leaves the idle stamp as it was
			// (0x004ff3d6); failing to find anywhere stamps it and leaves them deciding (0x004ff3f4).
			case 1:
				if ( SetRandomDest( peep, walk ) )
					SetWandering( peep, walk, tick );
				else
				{
					peep.TimeStartedIdling = tick;

					Log.Info( $"Person {peep.ThingId}: nowhere to wander to, idle stamp {tick}" );
				}

				break;

			// Being offered somewhere to go, once the sweep counter is more than ThinkingGap past their idle
			// stamp, unsigned (0x004ff42e). Only an empty hand stamps it again, so the gap is measured from the
			// last time nothing was found, or from the last wander that found nowhere.
			case 0:
				if ( (uint)tick <= (uint)(peep.TimeStartedIdling + ThinkingGap) )
					break;

				// A thing named: event 2 with the thing in it, and off they go (0x004ff44e).
				if ( ChooseSomewhereToGo( peep, walk, tick ) )
				{
					Unimplemented.Report( "DECIDE_CHOSEN_EVENT" );

					peep.SetState( PeepState.GoingToRide, tick, _random );

					break;
				}

				// Nothing named: event 1, spot animation 4, SmallHappinessChange off, and the stamp
				// (0x004ff480..0x004ff4a3). This project keeps no event ring, so the event is counted.
				Unimplemented.Report( "DECIDE_NOTHING_CHOSEN_EVENT" );

				var before = peep.Happiness;

				PlaySpotAnimation( peep, SpotBored, tick );
				peep.Happiness = Peep.Change( peep.Happiness, -admission.SmallHappinessChange );
				peep.TimeStartedIdling = tick;

				Log.Info( $"Person {peep.ThingId}: the chooser found nothing, happiness {before:0} to "
					+ $"{peep.Happiness:0}, tick {tick}" );

				break;

			// And a third of the time, nothing happens at all.
			default:
				break;
		}
	}

	/// <summary>
	/// Whether a deciding guest sets off home this turn - the test at <c>0x004fee5b</c>: the happiness byte nought,
	/// or <c>mExitLevel</c> (<c>+0x1bc</c>) <b>exactly</b> nought, or the park shut.
	/// </summary>
	/// <remarks>
	/// The exit level loses one on each of the guest's needs turns, one sweep in four, in every state and with no
	/// floor (<see cref="Peep.Tick"/>), and nothing but this turn reads it for leaving. So it reads nought for
	/// four sweeps, and a guest who is not deciding during them passes below and stays until they are miserable
	/// or the park shuts (<c>docs/exe/ride-operation.md</c>, "Q109").
	/// </remarks>
	internal bool WantsToLeave( Peep peep )
		=> (byte)(int)peep.Happiness == 0 || peep.ExitLevel == 0 || ParkIsClosed;

	/// <summary>
	/// The leaving arm of the deciding turn (<c>0x004fee87</c>..<c>0x004fef13</c>): <c>BigHappinessChange</c> off,
	/// on every turn the test holds and before any route is asked for, then a route to one of the crossing's two
	/// park-side cells, in the order the draw's low bit picks. The first that routes sets
	/// <see cref="PeepState.HeadingForExit"/>.
	/// </summary>
	/// <remarks>
	/// The original asks for both again in walking mode 1 (<c>+0x188</c>) before it gives up. Every walk here is
	/// mode 0 (<see cref="ParkPeople.WalkingMode"/>), so the second pass is counted and not made.
	/// </remarks>
	/// <returns>Whether a route was found. Without one the guest is still deciding, 25 the worse.</returns>
	private bool Leave( Peep peep, PeepWalk walk, ParkAdmission admission, int draw, int tick )
	{
		var before = peep.Happiness;
		var why = (byte)(int)before == 0 ? "miserable" : peep.ExitLevel == 0 ? "their day ran out" : "the park shut";

		peep.Happiness = Peep.Change( peep.Happiness, -admission.BigHappinessChange );

		var (first, second) = (draw & 1) == 0
			? (admission.CrossingParkSideA, admission.CrossingParkSideB)
			: (admission.CrossingParkSideB, admission.CrossingParkSideA);

		foreach ( var cell in new[] { first, second } )
		{
			if ( !SendTo( peep, walk, cell ) )
				continue;

			Log.Info( $"Person {peep.ThingId}: leaving, {why}, exit level {peep.ExitLevel}, happiness {before:0} to "
				+ $"{peep.Happiness:0}, to ({cell.X},{cell.Y}), tick {tick}" );

			peep.SetState( PeepState.HeadingForExit, tick, _random );

			return true;
		}

		Unimplemented.Report( "LEAVE_ROUTE_MODE_1_RETRY" );

		Log.Info( $"Person {peep.ThingId}: no way out, {why}, happiness {before:0} to {peep.Happiness:0}, tick {tick}" );

		return false;
	}

	/// <summary>
	/// How far a ride's excitement may be from what a guest likes before they turn away at the queue -
	/// <c>FUN_004ffbc0</c> tests the signed difference against <b>44</b>, and prints "ride is not
	/// exciting enough" below and "ride is too exciting" above.
	/// </summary>
	/// <remarks>
	/// The sign is the original's own and is worth not tidying: <c>FUN_004fd4e0</c> returns
	/// <i>preferred minus actual</i>, clamped to fifty, negated - so a NEGATIVE answer means the guest
	/// wanted more excitement than the ride offers. The magnitude refuses, and the sign picks the arm's event and
	/// thought (<see cref="TurnsAwayFrom"/>).
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
	/// <b>The gates.</b> A guest who fails one goes back to deciding, and none is docked. The free-space
	/// gate is <c>FUN_004dda20</c>, <c>length &lt; cells * 4</c> over the walked cells
	/// (<see cref="ParkRideChoice.HasQueueRoom"/>): refused, the guest <b>still names the thing</b>
	/// (<c>0x004ffe0a</c> writes neither <c>+0x1dc</c> nor a refusal), until the chooser next runs and lets it go.
	/// The excitement gate is <see cref="TurnsAwayFrom"/> and the third is <see cref="QueueTooLong"/>, each with the
	/// length counted as the original counts it (<see cref="QueueCount"/>). A guest refused by either of these two
	/// remembers the thing (<see cref="Peep.RememberRefusal"/>) and lets it go, and the excitement refusal also
	/// zeroes <see cref="Peep.TimeStartedIdling"/> (<c>0x004ffcf6</c>), so the thinking gap in front of the chooser
	/// is over at once. Each arm's event and thought is counted: this project keeps no event ring and draws no
	/// thought. See <c>docs/exe/ride-operation.md</c>, "Q103".
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

			// No back of queue, or no route to it: event 0x16 and deciding, the thing still named (0x004ffe4a).
			if ( backOfQueue == 0 || !SendTo( peep, walk, MapStep.CellAt( backOfQueue ) ) )
			{
				Unimplemented.Report( "ARRIVAL_BACK_OF_QUEUE_LOST_EVENT" );

				peep.SetState( PeepState.Deciding, tick, _random );
			}

			return;
		}

		// Asked of the park as PLAYED, so a queue that filled up while this guest walked to it turns them away.
		var queue = QueueCount( chosen );

		// No room: event 0x15 with no thing in it, and deciding (0x004ffe0a). Nothing else is written, so the guest
		// still names the thing, remembers no refusal and keeps their idle stamp.
		if ( !ParkRideChoice.HasQueueRoom( queue, queueCells ) )
		{
			Log.Info( $"Person {peep.ThingId}: no room in thing {chosen.ThingId}'s queue ({queue} in {queueCells} cells), "
				+ $"still naming it, tick {tick}" );
			Unimplemented.Report( "ARRIVAL_NO_ROOM_EVENT" );

			peep.SetState( PeepState.Deciding, tick, _random );

			return;
		}

		// The two refusals push the thing onto the guest's second history (FUN_004fdc60, 0x004ffce6 and
		// 0x004ffd74), which divides its score down until aged out.
		if ( TurnsAwayFrom( peep, chosen, out var tooExciting ) )
		{
			// Event 5 and thought 0xc for a ride not exciting enough, event 4 and thought 0xf for one too exciting
			// (0x004ffca1..0x004ffcd7).
			Log.Info( $"Person {peep.ThingId}: ride is {(tooExciting ? "too exciting" : "not exciting enough")}! "
				+ $"(thing {chosen.ThingId}, tick {tick})" );
			Unimplemented.Report( tooExciting ? "ARRIVAL_TOO_EXCITING_EVENT" : "ARRIVAL_NOT_EXCITING_EVENT" );
			Unimplemented.Report( tooExciting ? "ARRIVAL_TOO_EXCITING_THOUGHT_0xF" : "ARRIVAL_NOT_EXCITING_THOUGHT_0xC" );

			peep.RememberRefusal( chosen.ThingId );
			peep.TimeStartedIdling = 0;
			GiveUpOnIt( peep, tick );

			return;
		}

		if ( QueueTooLong( chosen, ItemOf( chosen ), queue ) )
		{
			Log.Info( $"Person {peep.ThingId}: queue is too long! ({queue} for thing {chosen.ThingId})" );
			Unimplemented.Report( "ARRIVAL_TOO_LONG_EVENT" );
			Unimplemented.Report( "ARRIVAL_TOO_LONG_THOUGHT_0x10" );

			peep.RememberRefusal( chosen.ThingId );
			GiveUpOnIt( peep, tick );

			return;
		}

		// Past every gate, happiness is copied for the settle-up to measure the visit against (0x004ffd92), just
		// before the queue takes them. A re-take of a place later does not copy it again.
		peep.JoinHappiness = peep.Happiness;

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
	/// <b>Without a catalogue the roll is not made at all</b>: an unknown item cannot settle its visit,
	/// so admission retains the same description guard as the settle-up. The object supplies the chance.
	/// </para>
	/// </summary>
	private void RollForTheVisit( Peep peep, ParkWorld.CatalogueObject entering )
	{
		if ( _catalogue == null || !_catalogue.TryGet( entering.CatalogueId, out var item ) )
			return;

		var succeeded = ParkRideOperation.Succeeds( entering, _random );

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

	/// <summary>
	/// How many sweeps past its stamp a spot animation holds its guest - <c>ADD EAX,0xa</c> at <c>0x00501d32</c>.
	/// The return comes on the first sweep above it, the eleventh after the one that began it.
	/// </summary>
	public const int SpotAnimationSweeps = 10;

	/// <summary>Spot animation 4: hands on hips, picture set 14 - a bored or unhappy guest (<c>0x00500387</c>).</summary>
	public const int SpotBored = 4;

	/// <summary>Spot animation 5: a jump with both arms up, picture set 12 - a happy guest (<c>0x00500320</c>).</summary>
	public const int SpotHappy = 5;

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
	/// <c>FUN_004dda40</c>'s floor, the <c>4.0f</c> at <c>0x00700558</c>: a longest queue at or below it, or not a number,
	/// is raised to it before the truncation, which still answers nought for an infinite one (<see cref="LongestQueue"/>).
	/// </summary>
	public const double LongestQueueFloor = 4.0;

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
	/// cell (<c>FUN_004dedf0(0)</c>, the item's sub-cell offset turned by the facing). With no route the ride
	/// forgets them and they are put out (<c>0x0050010a</c>, <see cref="PutOutOfTheQueue"/>). The original's
	/// <c>FUN_004fa5f0</c> also answers nought without routing on a guest's <c>mStrandedTime</c> (<c>+0x198</c>,
	/// <c>0x004fa62a</c>), which every walk tick zeroes, so on this arm it is nought unless a save loaded it, and
	/// nothing here reads a save's: that refusal is not built.</item>
	/// <item><b>Wait.</b> At the front and invited but not the nominee: the whole turn is nothing (<c>0x005001d8</c>).</item>
	/// <item><b>The dirt gate</b> (<c>0x005001f0</c>) puts out a queuer for a dirty toilet
	/// (<see cref="ParkState.IsDirty"/>), which use makes one (<see cref="ParkRideOperation.WearByUse"/>); thought
	/// <c>0xe</c> is counted. A handyman's cleaning restores it and is not built (Q133), so a toilet here stays
	/// dirty (<c>ride-operation.md</c>, "A toilet's dirt").</item>
	/// <item><b>The lost place.</b> The queue walk cannot reach them - they are unlinked, or somebody in front has
	/// stopped queueing: put out. The original's log says it closes and reopens the ride; nothing does.</item>
	/// <item><b>In place</b>: too far back for the thing's longest queue (<see cref="LongestQueue"/>), or a
	/// car track that is not valid, is put out; a coaster's track
	/// record is counted and let through, as the choice lets it through.</item>
	/// <item><b>Out of place</b>: more than <see cref="QueueDriftAllowed"/> out, or no delay left, re-takes the place
	/// - unless the ride is broken down (<c>FUN_004e0370</c>, state 1), when nothing is re-taken. Re-taking walks them
	/// to it (<see cref="FindQueueDestination"/>, <c>0x00500532</c>) and the mood still runs on the same turn; one who
	/// cannot get there is put out (<c>0x005004b3</c>). Otherwise a delay is spent.</item>
	/// <item><b>The mood</b>, read once <see cref="QueueMoodGap"/> sweeps have passed since a spot animation: above
	/// <see cref="QueueHappyAbove"/> spot animation <see cref="SpotHappy"/> and from <see cref="QueueUnhappyBelow"/>
	/// to 19 <see cref="SpotBored"/> (<see cref="PlaySpotAnimation"/>), either ending the turn;
	/// from <see cref="QueueToiletFrom"/> to 80 with a toilet need above <see cref="QueueToiletAbove"/>, thought 4
	/// (counted), and out unless the thing is a toilet; below <see cref="QueueUnhappyBelow"/>, thought <c>0xb</c>
	/// (counted), and out by the common leave path.</item>
	/// <item><b>Within the gap</b>, one turn in ten turns the heading (counted); boredom would put them out once
	/// <see cref="QueueBoredAfter"/> sweeps have passed since they began to stand, and <b>never fires</b>:
	/// <c>mTimeStartedIdling</c> is stamped on every return to the queue at least eleven sweeps after the spot
	/// animation that began the gap, so it cannot be a hundred past within thirty. It is counted, not built.</item>
	/// </list>
	/// <para>
	/// <b>A guest who has played no spot animation has <see cref="Peep.TimeOfLastSpotAnim"/> at nought</b>, which
	/// the thing tick has passed by more than thirty once the lobby has run about seven seconds, so their mood is
	/// read on their first turn in a queue. A queuer above <see cref="QueueHappyAbove"/> then jumps on every 31st
	/// sweep and is in <see cref="PeepState.PlayingSpotAnimation"/> for eleven of them, during which this turn does
	/// not run: no place, mood, toilet or board arm.
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
			{
				Log.Info( $"Person {peep.ThingId}: the player has removed the path from under me and I can no "
					+ $"longer get into object {queueing.ThingId}" );
				PutOutOfTheQueue( peep, queueing, tick, "no route to board", forgotten: true );

				return;
			}

			peep.SetState( PeepState.BeingAdmitted, tick, _random );

			return;
		}

		if ( ParkState.IsDirty( queueing ) )
		{
			Unimplemented.Report( "QUEUE_TURN_THOUGHT_0xE" );
			PutOutOfTheQueue( peep, queueing, tick, "the toilet's dirt" );

			return;
		}

		var place = State.PositionInQueue( queueing.ThingId, peep.ThingId, _stillQueueing );

		if ( place < 0 )
		{
			Log.Info( $"Person {peep.ThingId}: Problem with a queue - shouldn't be fatal" );
			PutOutOfTheQueue( peep, queueing, tick );

			return;
		}

		if ( recorded == place )
		{
			if ( LongestQueue( queueing, ItemOf( queueing ) ) is { } longest && (uint)recorded > longest )
			{
				Unimplemented.Report( "QUEUE_TURN_THOUGHT_0x10" );
				PutOutOfTheQueue( peep, queueing, tick, $"the capacity, place {recorded} past {longest}" );

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
			PlaySpotAnimation( peep, SpotHappy, tick );

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
			PlaySpotAnimation( peep, SpotBored, tick );

			return;
		}

		Unimplemented.Report( "QUEUE_TURN_THOUGHT_0xB" );
		PutOutOfTheQueue( peep, queueing, tick, "unhappiness" );
	}

	/// <summary>
	/// Stops a guest where they stand to play a one-off animation - <c>FUN_004fc800</c>
	/// (<c>docs/exe/ride-operation.md</c>, "Spot animations"): for <see cref="SpotBored"/> a guest whose id has a
	/// low nibble of nought yawns (<c>0x004fc83f</c>, no draw taken, so always the same guests); the sprite is
	/// asked for the animation; the state they are in is saved; and they enter
	/// <see cref="PeepState.PlayingSpotAnimation"/>, which stamps <see cref="Peep.TimeOfLastSpotAnim"/> and asks for
	/// no animation of its own. <see cref="Step"/>'s case for that state brings them back.
	/// </summary>
	/// <remarks>
	/// The queue turn's two arms and the deciding turn's empty hand call it. The original's other two calls are
	/// the deciding turn's 5 and 7 (Q111).
	/// </remarks>
	internal void PlaySpotAnimation( Peep peep, int animation, int tick )
	{
		if ( Yawns( animation, peep.ThingId ) && ParkGuestSprites.Feet( peep.Navigator.Position ) is { } feet )
			ParkAudio.Current?.Yawn( feet );

		peep.NextAnimation = animation;
		peep.SavedState = peep.State;
		peep.SetState( PeepState.PlayingSpotAnimation, tick, _random );

		Log.Info( $"Person {peep.ThingId}: spot animation {animation} from {peep.SavedState} at tick {tick}, "
			+ $"happiness {peep.Happiness:0}" );
	}

	/// <summary>Whether a spot animation starts with a yawn: number 4, for an id whose low nibble is nought (<c>0x004fc82e</c>..<c>0x004fc842</c>).</summary>
	internal static bool Yawns( int animation, int thingId ) => animation == SpotBored && (thingId & 0xf) == 0;

	/// <summary>
	/// Where every arm of <see cref="QueueTurn"/> that gives up ends - <c>0x0050049e</c> - and the join's and the
	/// door's failed walks to a place: the ride lets them go (<see cref="_leaveQueue"/>, <c>FUN_004ddd20</c>), they
	/// are put out (<see cref="DismissFromTheQueue"/>, <c>FUN_005012f0</c>), and the kids' sound plays at their feet
	/// for an id divisible by eight.
	/// </summary>
	/// <param name="by">What put them out, for the log.</param>
	/// <param name="forgotten">
	/// The board arm's: the ride forgets its nominee first (<see cref="_walkAway"/>, <c>FUN_004e0ac0</c> at
	/// <c>0x0050012b</c>, then <c>FUN_004ddd20</c>).
	/// </param>
	private void PutOutOfTheQueue( Peep peep, ParkWorld.CatalogueObject queueing, int tick,
		string by = "their own turn", bool forgotten = false )
	{
		(forgotten ? _walkAway : _leaveQueue)?.Invoke( queueing, peep.ThingId );
		DismissFromTheQueue( peep, tick );

		Log.Info( $"People: guest {peep.ThingId} put out of thing {queueing.ThingId}'s queue by {by}, "
			+ $"now {peep.State} with happiness {peep.Happiness:0}" );

		ParkPeople.PutOffAtTheirFeet( peep, queueing );
	}

	/// <summary>What kind of track a thing runs on, from its item, or nought for one the catalogue does not know.</summary>
	private int TrackTypeOf( ParkWorld.CatalogueObject thing )
		=> _catalogue != null && _catalogue.TryGet( thing.CatalogueId, out var item ) ? item.TrackType : 0;

	/// <summary>A thing's item, or null for one the catalogue does not know.</summary>
	private ParkItemCatalogue.Item? ItemOf( ParkWorld.CatalogueObject thing )
		=> _catalogue != null && _catalogue.TryGet( thing.CatalogueId, out var item ) ? item : null;

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

	/// <summary>
	/// Lets go of what they chose and thinks again, which is where the excitement and too-long refusals above end.
	/// </summary>
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
	/// <b>Every guest, chosen or not, lets go of a saved major naming the thing</b> (<see cref="Peep.SavedMajorDest"/>,
	/// <c>0x004fb4a6</c>..<c>0x004fb4b3</c>) <b>and forgets its visits</b> (<see cref="Peep.ForgetThing"/>,
	/// <c>0x004fb4ba</c>). Each arm also writes an entry into the guest's event ring (0xd for a rider, 6 for a
	/// queuer), whose only reader is a debug dump; this project keeps no ring.
	/// </para>
	/// </remarks>
	internal PutOff ThingRemoved( Peep peep, int thingId, int tick )
	{
		ArgumentNullException.ThrowIfNull( peep );

		if ( thingId == 0 )
			return PutOff.No;

		if ( peep.SavedMajorDest == thingId )
			peep.SavedMajorDest = 0;

		peep.ForgetThing( thingId );

		if ( peep.MajorDest != thingId )
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
			&& PeepPriceOpinion.TooExpensive( peep, thing.PricePerUse, item, thing );

	/// <summary>
	/// Walking away from a thing too expensive to board - <c>FUN_005006b0</c>'s first arm, <i>"Person %d:
	/// Object %d is too expensive, I'm leaving the queue"</i>: <see cref="ParkAdmission.MediumHappinessChange"/>
	/// off (<c>0x00500778</c>), the ride told to forget them and the queue left (<see cref="_walkAway"/>),
	/// then put out of the queue (<see cref="DismissFromTheQueue"/>, <c>0x005007b4</c>), which takes the same
	/// again. Thirty in this park.
	/// </summary>
	/// <remarks>
	/// The object counts the walk-away (<c>FUN_004e1670</c>, <see cref="ParkObjectRings.CountWalkAway"/>). Thought 6
	/// and the event-ring entry (event 10, <c>0x0050076b</c>) are counted; nothing here draws a thought or keeps the
	/// ring.
	/// </remarks>
	private void WalkAwayFromTheDoor( Peep peep, ParkWorld.CatalogueObject thing, int tick )
	{
		Log.Info( $"Person {peep.ThingId}: Object {thing.ThingId} is too expensive, I'm leaving the queue "
			+ $"(price {thing.PricePerUse}, cash {peep.Cash})" );

		Unimplemented.Report( "DOOR_PRICE_THOUGHT_6" );
		Unimplemented.Report( "DOOR_EVENT_HISTORY" );

		if ( Admission is { } mood )
			peep.Happiness = Peep.Change( peep.Happiness, -mood.MediumHappinessChange );

		State.RingsFor( thing.ThingId ).CountWalkAway();

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
	/// all, and it is the same test that drops the excitement weight out of the ride scorer. The comparison is
	/// against the thing's computed excitement, <see cref="ParkRideScore.ExcitementOf"/>, as <c>FUN_004fd4e0</c>
	/// asks <c>FUN_004e0860( object, 0 )</c>. For a dirty toilet (<see cref="ParkState.IsDirty"/>) that function
	/// answers 100 before it compares anything (<c>0x004fd4ea</c>), which is past the refusal and on the too-exciting
	/// side; no jungle toilet declares an excitement, so none reaches it.
	/// </remarks>
	/// <param name="tooExciting">
	/// Which arm a refusal took: true where the thing offers more than the guest's kind prefers (the answer above
	/// nought, <c>0x004ffc85</c>), false where it offers the same or less.
	/// </param>
	private bool TurnsAwayFrom( Peep peep, ParkWorld.CatalogueObject chosen, out bool tooExciting )
	{
		tooExciting = false;

		if ( _catalogue == null || !_catalogue.TryGet( chosen.CatalogueId, out var item )
			|| (item.ExcitementLevel & 0xff) == 0 )
			return false;

		if ( ParkState.IsDirty( chosen ) )
		{
			tooExciting = true;

			return true;
		}

		var wanted = _chooser.Score.PreferredExcitementFor( peep.PersonType ) & 0xff;
		var offered = ParkRideScore.ExcitementOf( chosen, item, State.TrackRides );

		tooExciting = wanted < offered;

		return Math.Abs( wanted - offered ) > ExcitementRefusal;
	}

	/// <summary>
	/// Whether a queue is too long to join - <c>FUN_004ddb60</c>: its count at or past <see cref="LongestQueue"/>,
	/// unsigned.
	/// </summary>
	internal static bool QueueTooLong( ParkWorld.CatalogueObject chosen, ParkItemCatalogue.Item? item, int queue )
		=> LongestQueue( chosen, item ) is { } longest && (uint)queue >= longest;

	/// <summary>
	/// The longest queue a guest joins (<see cref="QueueTooLong"/>) or stays in (<see cref="QueueTurn"/>'s capacity
	/// arm) - <c>FUN_004dda40</c>, which both compare unsigned. <see cref="NoQueuePathCapacity"/> for a thing without
	/// the queue-path bit. With it, <c>trunc( max( capacity × QueueWaitTimeConstant × R / duration, 4 ) )</c> at the
	/// thing's tier, where R is its speed over the tier's <c>InitSpeed</c>, stored as a float, or 1 at a speed of
	/// nought (<c>docs/exe/ride-operation.md</c>, "The longest queue - <c>FUN_004dda40</c>"). A tier stating no constant reads
	/// nought and so holds the floor, 4. Null, and both gates let the guest through, for a tier past the third,
	/// whose constant the original reads from past <c>Upgrades</c>, or a thing the catalogue does not know, which the
	/// original would read through a null descriptor: each counted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The arithmetic is taken at double precision</b>, the runtime's starting precision, as
	/// <see cref="ParkRideScore.Priced"/> is. Which precision the original runs it at is not settled; at 24 bits a
	/// setting whose speed is off its tier's can answer one more.
	/// </para>
	/// <para>
	/// <b>The truncation is <c>__ftol</c>'s</b> (<c>0x0067a830</c>): toward nought through a 64-bit integer, keeping
	/// the low 32 bits. An infinite value stores the integer indefinite, whose low half is nought, so a duration of
	/// nought, or a tier's <c>InitSpeed</c> of nought under a speed, lets nobody in, unless capacity × constant is
	/// nought: that is not a number and holds the floor. The original refuses neither: its asserts on the constant and
	/// the duration do nothing, and it checks no <c>InitSpeed</c>.
	/// </para>
	/// </remarks>
	internal static uint? LongestQueue( ParkWorld.CatalogueObject thing, ParkItemCatalogue.Item? item )
	{
		if ( !thing.HasQueuePath )
			return NoQueuePathCapacity;

		if ( item is not { } described )
		{
			Unimplemented.Report( "QUEUE_CAPACITY_UNKNOWN_ITEM" );

			return null;
		}

		var tier = thing.UpgradeLevel;

		if ( tier >= ItemDescriptionFile.Tiers )
		{
			Unimplemented.Report( "QUEUE_CAPACITY_UPGRADE_TIER" );

			return null;
		}

		// R: FILD of the zero-extended speed, FIDIV the tier's signed InitSpeed, FSTP float (0x004dda87..0x004dda95).
		var speed = (uint)thing.OperatingSpeed;
		var ratio = speed != 0 ? (float)(speed / (double)described.StartingAt( tier ).Speed) : 1f;

		var longest = (thing.OperatingCapacity & 0xff) * (double)described.QueueWaitTimeConstantAt( tier ) * ratio
			/ (thing.OperatingDuration & 0xff);

		// FCOM 4.0 then TEST AH,0x41 (0x004ddb3c): at or below the floor, or unordered, takes the floor.
		if ( !(longest > LongestQueueFloor) )
			longest = LongestQueueFloor;

		// Past 2^63, infinity included, FISTP stores the integer indefinite.
		return longest < 9223372036854775808.0 ? (uint)(long)longest : 0u;
	}

	/// <summary>
	/// How many are queueing for a thing as the original counts them - <see cref="ParkState.QueueCount"/>, up to and
	/// including the first guest no longer queueing. The choice's room test and score and the arrival's gates read it.
	/// </summary>
	private int QueueCount( ParkWorld.CatalogueObject thing ) => State.QueueCount( thing.ThingId, _stillQueueing );

	/// <summary>
	/// Whether drops are falling in the park - the weather thing's <c>mCurrentDrops</c> above nought
	/// (<c>0x004fd277</c>), which the score multiplies shelter by. <see cref="ParkPeople"/> hands in the park's
	/// weather; nothing handed in is dry.
	/// </summary>
	internal Func<bool> Raining { get; set; } = static () => false;

	/// <summary>
	/// What the arrival vehicle reports when it is the bus - <c>FUN_0051aad0</c> answering nought, then
	/// <c>FUN_0051a690</c>, the vehicle script's <c>VAR_STATUS</c> - and -1 when no vehicle is current or it is not
	/// the bus. <see cref="ParkPeople"/> hands in its own; nothing handed in is no vehicle.
	/// </summary>
	internal Func<int> BusStatus { get; set; } = static () => -1;

	/// <summary>What the choice reads of a guest: their kind, needs and histories.</summary>
	private static ParkRideScore.Wants WantsOf( Peep peep )
		=> new( peep.PersonType, peep.Thirst, peep.Hunger, peep.Toilet, peep.Vomit,
			peep.PreviousRides, peep.PreviousTemporaryRides );

	/// <summary>
	/// The chooser's best candidate with no route asked, so no walker is touched - <see cref="Explain"/>'s call.
	/// <see cref="ChooseSomewhereToGo"/> makes the same call with the route asked inside the walk, so its answer can
	/// be a lesser candidate than this one.
	/// </summary>
	private ParkWorld.CatalogueObject? Choose( Peep peep, int x, int y, int tick,
		Action<ParkWorld.CatalogueObject, int>? scored = null )
		=> _chooser.ChooseFor( WantsOf( peep ), x, y, tick, queueLength: QueueCount,
			now: State.CalendarNow, raining: Raining(), scored: scored );

	/// <summary>
	/// What the chooser scores best for one guest and where it would aim them, with no route asked. Whether they
	/// got a route is read from their own state and <see cref="Peep.MajorDest"/>, not tested here - see the note
	/// inside.
	/// </summary>
	/// <remarks>
	/// <b>It exists because no census here can tell the score from the route.</b> <see cref="Peep.MajorDest"/>
	/// is written only after <see cref="PeepWalk.PlanRoute"/> succeeds, so a guest whose best candidate cannot be
	/// routed to names a lesser one or nothing - and an empty <c>dest</c> census then reads exactly like "nothing
	/// was ever chosen". <see cref="ChooseSomewhereToGo"/> logs each route it asks.
	/// <para>
	/// It makes <see cref="ChooseSomewhereToGo"/>'s call on the chooser less the route, rather than asking the
	/// question its own way: a census that recomputes is not an observation. So each candidate whose excitement is
	/// counted (<see cref="ParkRideScore.ExcitementOf"/>) adds to the <c>unimplemented</c> census on every asking.
	/// </para>
	/// </remarks>
	internal string Explain( Peep peep, PeepWalk walk, int tick )
	{
		var (x, y) = walk.Position.Cell;
		var memory = $"visits [{string.Join( ",", peep.PreviousRides )}] refused [{string.Join( ",", peep.PreviousTemporaryRides )}]";

		// Every offered candidate's score, in the chooser's own order, as "thing:score".
		var scores = new List<string>();

		var picked = Choose( peep, x, y, tick, ( candidate, score ) => scores.Add( $"{candidate.ThingId}:{score}" ) );

		memory = $"scores [{string.Join( " ", scores )}] {memory}";

		if ( picked is not { } chosen )
			return $"at ({x},{y}) the chooser picked NOTHING {memory}";

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

		// The age the score used: NotNew, printed "-", for a thing with no stamp that makes a date.
		var age = ParkRideChooser.AgeOf( State.CalendarNow, chosen.Built );

		return $"at ({x},{y}) chose thing {chosen.ThingId} aim ({aimX},{aimY}) dest {peep.MajorDest} "
			+ $"age {(age == ParkRideChooser.NotNew ? "-" : age)} {memory}";
	}

	/// <summary>
	/// Offers this guest the best thing in the park and sets them off for it - <c>FUN_004fcb10</c>.
	///
	/// <para>
	/// <b>The route is asked inside the chooser's walk</b>, of every candidate that beats the best so far, at the
	/// centre of its back-of-queue cell (<c>0x004fcbc4</c>): one that routes becomes the best and is written to
	/// <see cref="Peep.MajorDest"/>, the person's own <c>+0x1dc</c>; one that does not is passed over. Every asking
	/// rewrites the walker, so <b>a better candidate that cannot be routed, met after one that could, leaves the
	/// walker failed under the earlier winner's name</b>. The guest is still answered as having chosen, and their
	/// first turn walking takes the stuck arm (<see cref="LoseHeartOnTheWay"/>). The original's walker can instead
	/// revive the failed route when the ground near the guest has changed since their last plan, and walk them to
	/// the loser's back of queue; <see cref="PeepWalk"/> keeps no ground stamp, so that does not happen here.
	/// See <c>docs/exe/ride-operation.md</c>, "Q104".
	/// </para>
	/// <para>
	/// <b>Queue lengths ARE passed, and are measured from the park as played rather than as saved.</b> The
	/// save leaves <c>mFirstInQ</c> at nought on every object - nobody had ever queued in it - so every
	/// queue starts genuinely empty; but guests join them, so the length has to be read live.
	/// </para>
	/// </summary>
	/// <returns>Whether a thing is named, which is what the caller tests (<c>0x004ff437</c>).</returns>
	internal bool ChooseSomewhereToGo( Peep peep, PeepWalk walk, int tick )
	{
		// Whatever they named before is let go of first, chosen or not (0x004fcb21).
		peep.MajorDest = 0;

		var (x, y) = walk.Position.Cell;

		bool Route( ParkWorld.CatalogueObject candidate, int score )
		{
			var routed = SetOffFor( peep, walk, candidate );

			Log.Info( $"Person {peep.ThingId}: the chooser routes to thing {candidate.ThingId} (score {score}): "
				+ (routed ? "a route" : $"NO ROUTE, the walker left failed, still naming thing {peep.MajorDest}")
				+ $", tick {tick}" );

			return routed;
		}

		return _chooser.ChooseFor( WantsOf( peep ), x, y, tick, queueLength: QueueCount,
			now: State.CalendarNow, raining: Raining(), route: Route ) != null;
	}

	/// <summary>
	/// One candidate's routing in <see cref="ChooseSomewhereToGo"/>: the route to the thing's back of queue, and on
	/// a route <see cref="Peep.MajorDest"/>. Answers whether a route was found.
	/// </summary>
	private bool SetOffFor( Peep peep, PeepWalk walk, ParkWorld.CatalogueObject chosen )
	{
		// Aimed at the centre of the back-of-queue cell (0x004fcbc4), where JoinTheQueue's arrival test asks them to
		// stand. The chooser offers nothing without one.
		var (backOfQueue, _) = ParkRideChoice.QueueCellsFor( _park, chosen );

		if ( backOfQueue == 0 || !SendTo( peep, walk, MapStep.CellAt( backOfQueue ) ) )
			return false;

		peep.MajorDest = chosen.ThingId;

		return true;
	}

	/// <summary>
	/// Sends a guest to a named thing as if they had chosen it, for the debug console's <c>send</c>: the ride arm of
	/// <see cref="Decide"/> with <see cref="Choose"/> skipped and nothing else. An INSTRUMENT: a run using it proves
	/// the thing's side, never the guest's choice. Only a guest in <see cref="PeepState.Deciding"/> or
	/// <see cref="PeepState.Wandering"/> is sent, so no queue or ride loses one.
	/// </summary>
	/// <returns>Why nothing happened, or null when they set off.</returns>
	internal string? SendAsChosen( Peep peep, PeepWalk walk, int thingId, int tick )
	{
		if ( peep.State is not (PeepState.Deciding or PeepState.Wandering) )
			return $"guest {peep.ThingId} is {peep.State}, not deciding or wandering";

		if ( !State.TryObject( thingId, out var chosen ) )
			return $"no thing {thingId}";

		peep.MajorDest = 0;

		if ( !SetOffFor( peep, walk, chosen ) )
			return $"no route to thing {thingId}'s back of queue";

		peep.SetState( PeepState.GoingToRide, tick, _random );

		return null;
	}

	/// <summary>
	/// What <see cref="PeepState.Entering"/>'s arrival does, for a guest the debug console makes inside the park
	/// (<see cref="ParkPeople.AdmitInside"/>): paid, numbered a visitor, and deciding.
	/// </summary>
	internal void AdmitAsEntered( Peep peep, int tick )
	{
		peep.PaidAdmission = true;
		peep.VisitorNumber = State.Admit();
		peep.SetState( PeepState.Deciding, tick, _random );
	}

	/// <summary>
	/// How both failed walks to a chosen thing end - the tails of <c>FUN_004ffbc0</c>'s stuck and shut arms
	/// (<c>0x004ffe94</c>, <c>0x004ffec9</c>): <c>FUN_004fea70( 2 )</c>, which is
	/// <see cref="ParkAdmission.BigHappinessChange"/> off, the thing let go of, and back to
	/// <see cref="PeepState.Deciding"/>.
	/// </summary>
	private void LoseHeartOnTheWay( Peep peep, int tick )
	{
		if ( Admission is { } mood )
			peep.Happiness = Peep.Change( peep.Happiness, -mood.BigHappinessChange );

		GiveUpOnIt( peep, tick );
	}

	/// <summary>
	/// A turn of walking to a chosen thing that has not got there yet - the walking arm of <c>FUN_004ffbc0</c>
	/// (<c>0x004ffef2</c>..<c>0x004fff06</c>): the turn is counted on <see cref="Peep.WalkingTurns"/>, and every
	/// twelfth runs the minor decision (<see cref="MinorDecision"/>), the count zeroed first. Nothing follows it in
	/// the turn.
	/// </summary>
	/// <remarks>
	/// <b>With the park shut nothing is counted</b> (<c>FUN_0051a280</c>, <c>0x004ffeaf</c>): "The park has closed
	/// underneath me!", and <see cref="LoseHeartOnTheWay"/>.
	/// </remarks>
	private void WalkOn( Peep peep, PeepWalk walk, int tick )
	{
		if ( ParkIsClosed )
		{
			Log.Info( $"Person {peep.ThingId}: the park has closed underneath me, bound for thing {peep.MajorDest}, "
				+ $"tick {tick}" );

			LoseHeartOnTheWay( peep, tick );

			return;
		}

		// Stored before the compare, which is unsigned (0x004ffefe), so a byte of 255 wraps to nought.
		peep.WalkingTurns = (byte)(peep.WalkingTurns + 1);

		if ( peep.WalkingTurns <= MinorDecisionAfter )
			return;

		peep.WalkingTurns = 0;

		MinorDecision( peep, walk, tick );
	}

	/// <summary>
	/// How many walking turns pass without a minor decision - <c>CMP AL,0xb</c> / <c>JBE</c> at <c>0x004ffefe</c>, so
	/// it is made on the twelfth.
	/// </summary>
	public const int MinorDecisionAfter = 11;

	/// <summary>
	/// Whether a nearer thing on the way is worth turning aside for - <c>FUN_004fd570</c>, "Minor Decision" by its own
	/// log (<c>docs/exe/ride-operation.md</c>, "A second toilet: the minor decision and the saved major").
	/// </summary>
	/// <remarks>
	/// Nothing without a thing chosen that has a back of queue. The pick is
	/// <see cref="ParkRideChooser.MinorDecisionFor"/>, from the cell the guest stands on after this turn's step. Then
	/// the switch test: <see cref="CellSearch.RouteLength"/> from the chosen thing's entry to the guest's cell, and
	/// from it to the nearer thing's entry, neither -1 and the second <b>shorter</b> (a tie refused,
	/// <c>0x004fd888</c>). A switch saves the chosen thing in <see cref="Peep.SavedMajorDest"/>, whatever that held,
	/// names the nearer one, and aims the guest at the centre of its entry cell with the answer ignored; the state
	/// stays, and <see cref="JoinTheQueue"/>'s arrival test re-aims them from the entry to its back of queue.
	/// <para>
	/// <b>The lengths are measured in mode 0.</b> The original passes the guest's own <c>+0x188</c>, the navigator's
	/// mode, which some of its arms set to 1 (<see cref="ParkPeople.WalkingMode"/>); mode 1 would also let the search
	/// leave a path for bare ground. Whether a guest walking to a thing can hold 1 is not established. The event the
	/// switch pushes (<c>0x17</c>) is counted: this project keeps no event ring.
	/// </para>
	/// </remarks>
	private void MinorDecision( Peep peep, PeepWalk walk, int tick )
	{
		if ( _park == null || Chosen( peep ) is not { } major )
			return;

		var (majorBack, _) = ParkRideChoice.QueueCellsFor( _park, major );

		if ( majorBack == 0 )
			return;

		var (x, y) = walk.Position.Cell;

		if ( _chooser.MinorDecisionFor( WantsOf( peep ), x, y, major.ThingId, MapStep.CellAt( majorBack ), tick,
			QueueCount, State.CalendarNow, Raining() ) is not { } nearer )
			return;

		var entry = (major.EntryCellX, major.EntryCellY);
		var toGuest = CellSearch.RouteLength( entry, (x, y), walk.Blocked );
		var toNearer = CellSearch.RouteLength( entry, (nearer.Thing.EntryCellX, nearer.Thing.EntryCellY), walk.Blocked );

		if ( toGuest == -1 || toNearer == -1 || toNearer >= toGuest )
		{
			Log.Info( $"Person {peep.ThingId}: Minor Decision kept {major.ThingId} at ({x},{y}): thing "
				+ $"{nearer.Thing.ThingId} sc={nearer.Score}, lengths {toGuest} {toNearer}, tick {tick}" );

			return;
		}

		Log.Info( $"Person {peep.ThingId}: Minor Decision: OID={nearer.Thing.CatalogueId}, "
			+ $"@=({nearer.Thing.CellX}, {nearer.Thing.CellY}), sc={nearer.Score} - thing {nearer.Thing.ThingId} "
			+ $"for {major.ThingId} at ({x},{y}), lengths {toGuest} {toNearer}, tick {tick}" );

		Unimplemented.Report( "MINOR_DECISION_EVENT" );

		peep.MajorDest = nearer.Thing.ThingId;
		peep.SavedMajorDest = major.ThingId;

		SendTo( peep, walk, (nearer.Thing.EntryCellX, nearer.Thing.EntryCellY) );
	}

	/// <summary>
	/// A guest who has walked off a thing is sent on to the one the minor decision turned them aside from, unscored -
	/// the arrival arm of <c>FUN_00500900</c> (<c>0x00500913</c>..<c>0x005009d7</c>). False leaves them to think again.
	/// </summary>
	/// <remarks>
	/// With a saved major it becomes their chosen thing and is let go of, before
	/// anything is asked. A thing no longer in the park lets go of that too ("Deleted major dest while I was doing
	/// minor dest!"); the original asks the thing's type byte, and a removed thing is gone here. Otherwise they are
	/// aimed at its back of queue and walk to it again, "Left minor destination, found old major one again!"; with no
	/// back of queue or no route they think again still naming it. <b>No score, no offer gate and no event</b>: a
	/// shut ride, a full queue or a second toilet right after the first is still walked to.
	/// </remarks>
	private bool SentOnToTheSavedMajor( Peep peep, PeepWalk walk )
	{
		if ( peep.SavedMajorDest == 0 )
			return false;

		peep.MajorDest = peep.SavedMajorDest;
		peep.SavedMajorDest = 0;

		if ( Chosen( peep ) is not { } restored )
		{
			Log.Info( $"Person {peep.ThingId}: Deleted major dest while I was doing minor dest!" );
			peep.MajorDest = 0;

			return false;
		}

		var (backOfQueue, _) = ParkRideChoice.QueueCellsFor( _park, restored );

		if ( backOfQueue == 0 || !SendTo( peep, walk, MapStep.CellAt( backOfQueue ) ) )
			return false;

		Log.Info( $"Person {peep.ThingId}: Left minor destination, found old major one again! ({restored.ThingId})" );

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
	/// <b>A linked cell starts <see cref="LinkedWander"/></b>, a walk of the call's r % 5 + 1 passes over linked
	/// cells, and the guest is aimed at a random point INSIDE the last cell rather than its centre: two draws,
	/// x then y, each masked to <c>0x7f</c> and clamped to 5..123 of the 256 sub-cell units
	/// (<c>0x004f991c</c>). A route there sets <see cref="Peep.SetDestSuccessfully"/>.
	/// </para>
	/// <para>
	/// <b>The stranded bookkeeping is absent</b> (Q110): the original refuses a guest whose stamp says
	/// nothing near them has changed, stamps one who reaches a dead end and raises a thought bubble, and
	/// neither the stamp nor the thought system exists here, so a guest at a dead end is counted, stays where
	/// they are and is asked again.
	/// </para>
	/// </summary>
	/// <returns>Whether somewhere was found and a route to it planned.</returns>
	internal bool SetRandomDest( Peep peep, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

		// The original's pass count, drawn on every call before the count (0x004f9534); the linked walk reads it.
		var passes = (_random.Next() % LinkedWander.MostPasses) + 1;

		// A park that was never loaded has no cells to count, and every cell of it is taken as linked on all
		// four sides.
		if ( _park != null && CellEdge.Links( ParkState.CellFor( _park, x, y ).Neighbours ) == 0 )
			return WanderFromNowhere( peep, walk, x, y );

		var stepped = LinkedWander.Walk( CellOf, x, y, passes, _random );

		if ( stepped == null )
		{
			Unimplemented.Report( "WANDER_DEAD_END_STRANDED_STAMP" );
			Log.Info( $"Peep {peep.ThingId}: wander of {passes} from ({x},{y}) met a dead end" );

			return false;
		}

		var cell = stepped[^1];

		peep.Navigator.Target = new FixedVector( SomewhereIn( cell.X ), SomewhereIn( cell.Y ) );

		var routed = walk.PlanRoute();

		Log.Info( $"Peep {peep.ThingId}: wander of {passes} from ({x},{y}) by "
			+ string.Join( " ", stepped.Select( step => $"({step.X},{step.Y})" ) ) + (routed ? "" : ", no route") );

		if ( routed )
			peep.SetDestSuccessfully = true;

		return routed;
	}

	/// <summary>The running park's cell, or with no park a bare cell linked on all four sides.</summary>
	private ParkWorld.MapCell CellOf( int x, int y )
		=> _park == null ? new ParkWorld.MapCell( 0, 0, 0x55, 0, 0, 0, 0, 0 ) : ParkState.CellFor( _park, x, y );

	/// <summary>
	/// Sets a guest wandering - SetState(7), <c>FUN_00501db0</c> case 7, which first routes again to the stored
	/// destination when <see cref="Peep.SetDestSuccessfully"/> is set (<c>FUN_004fa5f0</c>) and takes no notice
	/// of the answer.
	/// </summary>
	private void SetWandering( Peep peep, PeepWalk walk, int tick )
	{
		if ( peep.SetDestSuccessfully )
			walk.PlanRoute();

		peep.SetState( PeepState.Wandering, tick, _random );
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
				peep.SetDestSuccessfully = true;
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
				peep.SetDestSuccessfully = true;
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

	/// <summary>The near edge of a cell plus a clamped roll - see <see cref="SetRandomDest"/>.</summary>
	private int SomewhereIn( int cell )
	{
		var within = Math.Clamp( _random.Next() & 0x7f, 5, 0x7b );

		// A cell is 256 of these sub-units and One is a whole cell, so a sub-unit is One / 256.
		return (cell * PeepNavigator.One) + (within * (PeepNavigator.One / 256));
	}
}
