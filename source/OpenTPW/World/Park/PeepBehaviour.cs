using System;

namespace OpenTPW;

/// <summary>
/// One turn of what a guest is <i>doing</i> - the original's <c>FUN_005019f0</c>, the switch on state that
/// runs after the needs and decides what walking comes to.
///
/// <para>
/// <b>This is the thing that was missing, and its absence was the most misleading shape in the tree.</b>
/// <see cref="Peep.SetState"/> and <see cref="Peep.AnimationFor"/> were written, documented against the
/// disassembly and covered by tests, and <b>nothing in the game ever called them</b>: a guest's state was
/// written once when the park loaded and never again. So a guest walked to wherever the save had been
/// sending them and then stood there for ever, and <see cref="Peep.IsAWalkingState"/> listed states that
/// could not be entered.
/// </para>
/// <para>
/// <b>Eight of the twenty-two cases are answered in the switch's own body; the other fourteen jump to a
/// handler.</b> The inline cases are 0, 7, 8, 12, 14, 16, 17 and 20.
/// </para>
/// <para>
/// <b>This paragraph used to go on to say that no guest in Lost Kingdom is in any of the inline states,
/// and to conclude that building them would move nobody. That was true of the park as SAVED and became
/// false the moment the park ran.</b> The save's thirteen are in <see cref="PeepState.HeadingForGate"/>,
/// <see cref="PeepState.Entering"/> and <see cref="PeepState.WaitingForOpening"/>, all three of which
/// delegate - but a guest who is admitted to a ride is put into <see cref="PeepState.Riding"/>, which is
/// inline, and one who finishes deciding is put into <see cref="PeepState.Wandering"/>, which is inline
/// too. Reasoning about which states matter from the saved file alone is how nine of the twenty-two came
/// to have no case at all, and Alexah found two of those by playing the game rather than by any test
/// failing. <b>What decides whether a state matters is whether anything SETS it, not where the park
/// starts.</b>
/// </para>
/// <para>
/// <b>What decides every one of those three is whether the park is open</b> - <c>FUN_0051a280</c>, which
/// returns <c>world + 0x1da710</c>, which the executable's own field-name table pairs with
/// <c>mParkClosed</c>. Until the save reader kept that field there was no way to ask the question, which
/// is why it was read off the disk first and this was built second.
/// </para>
/// <para>
/// <b>The admission sequence, for whoever carries it on.</b> HeadingForGate arrives and judges the fee;
/// a fee it accepts sets a "paid" flag and sends the guest back to wait for the gate; waiting with that
/// flag set and standing on the right cell becomes Entering; Entering arrives, takes a visitor number and
/// goes on to decide what to do. <b>The whole of that loop is here now</b> - judging and waiting
/// included; this said only the two arrivals were. What is left of it is the paid arm of waiting - see
/// <see cref="Step"/> for each remaining deferral and the reason it is deferred.
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
	/// for a single yes-or-no. Null leaves a guest standing at the ride, which is what happened before
	/// anything called this at all.
	/// </para>
	/// </param>
	public PeepBehaviour( ParkWorld? park, Random? random = null,
		ParkAdmission? admission = null, Func<int>? gateStatus = null, ParkState? state = null,
		ParkItemCatalogue? catalogue = null,
		Func<ParkWorld.CatalogueObject, int, bool>? admit = null,
		Func<ParkWorld.CatalogueObject, int, bool>? finishAdmission = null )
	{
		_admit = admit;
		_finishAdmission = finishAdmission;

		// Zero is open, which is the way round the name is not - see ParkWorld.ParkClosed. ParkState
		// applies that rule itself, so it is not repeated here.
		State = state ?? new ParkState( park );
		Admission = admission;
		_gateStatus = gateStatus;
		_random = random ?? new Random();
		_chooser = new ParkRideChooser( park, catalogue );
		_park = park;
		_catalogue = catalogue;
	}

	/// <summary>
	/// The park these guests are in, as it is being played rather than as it was saved.
	///
	/// <para>
	/// <b>This is where the two workarounds went.</b> <see cref="Takings"/> and
	/// <see cref="VisitorsToDate"/> used to be fields here, each documented as living on the behaviours
	/// only because <see cref="ParkWorld"/> describes a file and could not be moved. Both now read
	/// through this, and both keep their names so that nothing which already asks has to change.
	/// </para>
	/// </summary>
	public ParkState State { get; }

	/// <summary>
	/// The same, from the two facts themselves rather than from a park.
	///
	/// <para>
	/// <b>This exists so that a shut park can be tested at all.</b> The only park that can be loaded is the
	/// one the game ships, and it is saved open - so every branch that turns on the gates being closed
	/// would otherwise be unreachable from a test, including the one that decides whether a guest arriving
	/// at the gate judges the fee or settles down to wait. Taking the facts directly is the smallest thing
	/// that makes both arms reachable.
	/// </para>
	/// </summary>
	/// <param name="admission">
	/// What the park charges and what a guest makes of it. <b>Null leaves the fee unjudged</b> rather than
	/// guessed at: a guest who reaches the ticket booths with nothing able to price the park stands there,
	/// which is what this program did everywhere before any of it was built.
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
	/// <b>It is kept here for the same reason <see cref="VisitorsToDate"/> is</b>: taking a fee moves
	/// <c>mBalance</c> and <c>mProfitThisYear</c> by the same amount (<c>FUN_004d0600</c>, which adds it to
	/// both and to two running totals on the world), and <see cref="ParkWorld"/> describes a file and is
	/// deliberately immutable. So the park's money on screen is the save's balance plus this.
	/// </para>
	/// </summary>
	public int Takings => State.Takings;

	/// <summary>
	/// What the park's rides are worth to a guest deciding whether the price is fair - the sum
	/// <c>FUN_004c8240</c> makes.
	///
	/// <para>
	/// <b>Nought, and by the shipped park's own saved state rather than by omission.</b> That sum counts
	/// only things with somebody in their queue, and the save records <c>mNumberOfVisitorsToDate</c> as
	/// nought - nobody had ever been admitted, so no queue could hold anyone <i>at load</i>. <b>That is
	/// now the starting value rather than the standing one</b>: guests join queues and rides operate, so
	/// a park that has been running a while answers something else. This said a ride was never operated.
	/// It is settable so that the term is visible and testable rather than a zero nobody can see.
	/// </para>
	/// </summary>
	public int ParkExcitement { get; set; }

	/// <summary>
	/// Whether the gate will let anybody through - the pair of questions at the top of
	/// <c>FUN_004ff7f0</c>, which wants <c>mParkClosed</c> nought <b>and</b> the gate reporting 1.
	///
	/// <para>
	/// <b>A null <paramref name="_gateStatus"/> reads as open, and that is a choice with a precedent.</b>
	/// The constructor above already treats a null park as an open one, for the same reason: a park with
	/// no script runtime bound is not a park whose gates are shut, and answering "shut" would strand every
	/// guest at the bus stop on the strength of missing plumbing rather than of anything in the file.
	/// </para>
	/// </summary>
	public bool GateWillAdmit
		=> !ParkIsClosed && (_gateStatus == null || _gateStatus() == ParkRides.GateIsOpen);

	/// <summary>
	/// Whether the park is shut to visitors, as the save left it.
	///
	/// <para>
	/// Read once rather than watched, because nothing in this project can open or close a park yet: the
	/// command that does so is <c>FUN_00519ef0</c>, and the two buttons that would reach it
	/// (<c>InputButton.OpenPark</c> and <c>ClosePark</c>) are among the bindings nothing consumes.
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
	/// script what it is doing - and nothing here runs a script on the bus. So the branch is unreachable
	/// rather than unwritten, and a constant nothing can use would only read as an oversight.
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
	/// states do, so the two cannot be told apart by looking. Thirteen of the twenty-two were answered when
	/// Alexah reported that guests never entered a ride and never came off one, and the two states
	/// responsible had shipped that same day inside work that claimed the ride loop was closed. Recording
	/// the fall-through is what lets a test tell a deliberate stillness from a hole in the machine.
	/// </para>
	/// <para>
	/// <b>It is not a diagnostic switch and it is not tooling.</b> It is one field the behaviour keeps
	/// about itself, written on the one path that should never be taken; there is nothing to turn on and
	/// nothing to turn off.
	/// </para>
	/// </summary>
	public PeepState? UnansweredState { get; private set; }

	/// <summary>
	/// One turn of one guest's behaviour.
	///
	/// <para>
	/// <b>The switch comes first and the walk second, which is the original's order and not a detail.</b>
	/// <c>FUN_005019f0</c> decides what state a guest is in and only then asks whether they got anywhere, so
	/// a guest in a state that does not walk never reaches the walk at all. Doing it the other way round -
	/// walking everybody who <i>can</i> walk and then asking what it meant - is what this replaces.
	/// </para>
	/// <para>
	/// <b>This paragraph used to list three states as absent, and all three are built.</b>
	/// <see cref="PeepState.WaitingForOpening"/> is <see cref="Wait"/>,
	/// <see cref="PeepState.JudgingTheFee"/> is <see cref="Judge"/>, and
	/// <see cref="PeepState.Deciding"/> - the hub a guest returns to whenever they finish anything - is
	/// <see cref="Decide"/>, whose ride arm was the last of them to be answered.
	/// <para>
	/// What remains absent is narrower and sits inside those, not instead of them: the paid arm of
	/// <see cref="Wait"/> needs a runtime map cell nothing here keeps, and two of
	/// <see cref="Decide"/>'s own conditions read fields nothing has named. Each is recorded where it
	/// happens rather than here.
	/// </para>
	/// </para>
	/// <para>
	/// <b>And the give-up path does nothing on purpose.</b> Where the walk reports it cannot get through,
	/// the original's answer in four of these five cases is to print "Big problem - no way this should
	/// happen" through its diagnostic call and leave the guest where they are. There is nothing to
	/// reproduce but the leaving-alone.
	/// </para>
	/// </summary>
	/// <param name="tick">
	/// The thing tick, the same counter <see cref="Peep.Tick"/> is spread across. <b>The original stamps
	/// its own clock here instead</b> - the states that record a time compare it against the world's
	/// <c>mGameTick</c> - and which of the two those comparisons want is not established. It matters to
	/// <see cref="PeepState.PlayingSpotAnimation"/>, which is not built, and
	/// <see cref="PeepState.InQueue"/>, <b>which is</b> - so for the queue the question is live rather
	/// than hypothetical. This said neither was built.
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
				peep.PurposeSpeed = HurriesToTheGate( peep ) ? Peep.HurryingSpeed : Peep.UnhurriedSpeed;

				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					peep.SetState( ParkIsClosed ? PeepState.WaitingForOpening : PeepState.JudgingTheFee,
						tick, _random );
				}

				break;

			// Standing at a ticket booth making their mind up about the price - FUN_004ff9d0, and the state
			// Alexah found five of Lost Kingdom's guests stuck in.
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
			// shut under them, or whose gate is not open, is sent back to head for the gate again. Asking
			// the second half of that means asking the gate thing what its script is doing, which nothing
			// here can do - so the guard is absent rather than half-answered. It changes nothing for this
			// park, whose gates are open, and it would matter the moment a park could be shut while running.
			case PeepState.Entering:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
				{
					peep.VisitorNumber = State.Admit();
					peep.SetState( PeepState.Deciding, tick, _random );
				}

				break;

			// Walking to something they chose - FUN_004ffbc0, and THE CASE THIS SWITCH DID NOT HAVE.
			//
			// <b>Its absence was a real fault rather than a gap.</b> The ride arm of Deciding puts a guest
			// into this state, IsAWalkingState lists it, and AnimationFor gives it the walk - but with no
			// case here the walk was never ticked, so a guest who chose a ride stood exactly where they
			// decided, playing a walk, for ever. No test saw it: the chooser is tested on its own, and the
			// suite never ran a guest from Deciding through to arriving.
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

			// Standing in a queue - the one arm of FUN_004ffff0 that can be built honestly: a guest at the
			// FRONT who has been invited aboard, and whom the ride really has nominated, goes to board.
			//
			// <b>Three things must all hold, and each is the original's own test.</b> QueuePos nought is
			// "at the front"; mBeenAdmitted is the invitation; and FUN_004e0aa0 - which is nothing but
			// person == object's mPersonBeingLoaded - is the ride confirming it means THIS guest. The flag
			// is cleared on the way past, which is what stops one invitation boarding them twice.
			//
			// The rest of that handler is deliberately absent, and the boredom countdown is absent for a
			// SHARPER reason than the others, which is worth stating because it looks easy.
			//
			// Re-taking a place in a queue that moved needs the queue-path walk (FUN_004de7e0); the
			// capacity re-check divides by the per-upgrade descriptor field at +0x1a8, whose pairing
			// ParkRideScore refuses to guess; the dirt gate wants per-object dirt. Those are missing
			// inputs. <b>Boredom is not.</b> Its test is only `gameTick > mTimeStartedIdling + 100` - but
			// it sits INSIDE the branch the original takes only while `gameTick - mTimeOfLastSpotAnim` is
			// under 31, so it fires solely in the window after a spot animation. Nothing here plays one,
			// so mTimeOfLastSpotAnim stays nought and that branch stops being taken after tick 31.
			// Reproducing the countdown alone would therefore make guests give up in circumstances the
			// original never gives up in - a divergence wearing the clothes of a faithful subset.
			//
			// A guest who does not pass the three tests simply keeps queueing, which is what the original
			// does on every turn they are not being called forward.
			// <b>And the step-up below is what was missing, found by playing the park rather than by any
			// test.</b> The boarding arm comes first because that is the original's order.
			case PeepState.InQueue:
				if ( peep.QueuePos == 0 && peep.BeenAdmitted && Chosen( peep ) is { } boarding
					&& State.PersonBeingLoaded( boarding.ThingId ) == peep.ThingId )
				{
					peep.BeenAdmitted = false;

					SendTo( peep, walk, (boarding.EntryCellX, boarding.EntryCellY) );
					peep.SetState( PeepState.BeingAdmitted, tick, _random );

					break;
				}

				StepUpTheQueue( peep );

				break;

			// Walking to the ride that called them forward, and asking it to take them - FUN_005006b0.
			//
			// <b>THE ADMISSION IS THE GUEST'S, NOT THE RIDE'S, and this case is what was missing.</b>
			// Nothing in this tree ever set PeepState.EnteringRide, so CompleteAdmission - which waits on
			// exactly that state - could never fire in a running park. The chain reached BeingAdmitted and
			// stopped, and every test past it built the state by hand. That is the third time this project
			// has shipped an arm no test could see.
			//
			// Arriving and getting STUCK are one path, which is the original's own shape: it logs "Person
			// %d: Got stuck in middle o[f]..." and then carries on into the same test rather than treating
			// it as a failure.
			//
			// <b>Two arms of the original are named rather than invented.</b> Before admitting, it asks
			// whether the thing is too expensive (FUN_004fde50, which weighs a price against what a guest
			// thinks the thing is worth) and sends them back to Deciding if it is - nothing here models
			// that opinion, and ParkAdmission judges the GATE fee, which is a different question. And when
			// the admission is refused it tries to rejoin the front of the queue (FUN_00501160, unread)
			// before giving up. A guest here simply waits and asks again next turn, which is right for the
			// common refusal: AdmitPerson says no while the script still holds the last rider, and the
			// script clears that on its own next turn.
			case PeepState.BeingAdmitted:
				if ( Walked( peep, walk, playing ) != WalkVerdict.Walking
					&& Chosen( peep ) is { } arriving
					&& _admit?.Invoke( arriving, peep.ThingId ) == true )
				{
					peep.SetState( PeepState.EnteringRide, tick, _random );
				}

				break;

			// Waiting for the script to take them up, and coming off the queue when it has -
			// FUN_005019f0's case 0xe, which is FUN_00500870 inlined.
			//
			// <b>THE COMPLETION IS THE GUEST'S TOO, and that is why nothing finished one.</b>
			// ParkPeople's ride turn calls CompleteAdmission only for a ride that is closing or broken
			// (states 1, 2 and 4), which is faithful - the original does not call it from a healthy
			// ride's turn either. Its only other callers there are SetState and Invite's mCanLoad bail,
			// and that bail cannot fire in this park. So a guest reached EnteringRide and stayed in it:
			// measured, not inferred - a full run saw EnteringRide and never once saw Riding.
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
			// same 0x400 this does when none is. The other heading needs the arrival vehicle, so the
			// no-bus reading is what is reproduced, and it is the one this park is in.
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
			// doing. This used to add that with no bus thing in the world that function returns 1 at its
			// first test, so the gate stood open for every guest and the branch was unreachable rather
			// than unwritten. <b>A bus now stands in the park and its script runs</b> - ParkFixedItems
			// binds it - so that argument no longer holds and whether the branch is reachable has not
			// been measured. The gate is still left open here, which is what the code has always done.
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
			// <b>Its absence is why nobody Alexah watched ever rode twice, and it shipped inside the very
			// commits that claimed the ride loop was closed.</b> ParkRideOperation.Dismiss sets this state
			// from the ride's own turn; the switch had no case for it, so a guest who had been let off stood
			// at the ride's exit for ever. No test saw it because every test of the dismissal asserted the
			// STATE was reached, and reaching a state says nothing about what the state then does.
			//
			// <b>The destination is cleared on the stuck arm only, and that asymmetry is the original's.</b>
			// FUN_00500900 zeroes +0x1dc when the walk reports it cannot get through, and on arrival keeps
			// it while it logs "Person %d: successfully left rid[e]". Deciding's ride arm overwrites
			// MajorDest anyway, so reproducing the asymmetry costs nothing and tidying it would quietly make
			// this a different function.
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
			// <b>The change-of-mind arm is absent because two of its four terms have no name.</b> The
			// original turns a guest back to Deciding - "Make up your mind!" - when +0x1bc is positive AND a
			// float conversion of something is non-zero AND the park is open AND FUN_004fa990 agrees.
			// Decide already records +0x1bc as unidentified, so this is that same gap seen from the other
			// side rather than a second one.
			//
			// Getting stuck prints "I'm stuck in the park, even though it's closed!!" and leaves them where
			// they are, which is the give-up path this switch takes everywhere.
			case PeepState.HeadingForExit:
				if ( Walked( peep, walk, playing ) == WalkVerdict.Arrived )
					peep.SetState( PeepState.PickingACellOutside, tick, _random );

				break;

			// <b>The five states nothing in this tree SETS, grouped so that each is answered and each says
			// what it waits on.</b> A case that breaks looks exactly like a missing case on screen - the
			// guest stands still either way - so the difference has to be written down, and
			// UnansweredState is what lets the program itself tell them apart.
			//
			// PlayingSpotAnimation (8) returns to SavedState once ten ticks have passed; FUN_004fc890 is
			// one line and both halves of it exist here. What does not exist is anything that PLAYS a spot
			// animation, so the state is never entered.
			//
			// GoingToMinorDestination (9) walks to a shop or a toilet and, on arrival, runs THAT THING'S
			// script - FUN_004fff20 hands the thing's +0x24 to the script runtime. Walking a guest there
			// without running it would be a guest queueing at a drinks machine that never serves them.
			//
			// Leaving (17) deletes the guest unless some thing is holding them - it sweeps the thing list
			// for one whose +0x212 names this person. Nothing here removes a guest from a park, so entering
			// this state would strand them rather than end their day.
			//
			// PickingACellOutside (19) and AtTheBusStop (21) walk to cells from FUN_004d8650, and WHICH
			// balance-file pair that getter returns is NOT YET PROVEN. The +1 among its four candidates
			// ({c, c+1, c-0x100, c-0xff}) rules out BusStopA/B, whose cells are (42,5) and (53,5) and are
			// not adjacent; CrossingParkSideA/B reads right and is not established. Guessing between two
			// readings a test cannot tell apart is what made P4's rest areas inert, so the pair stays
			// unread until it is measured.
			//
			// <b>The second half of that argument has since fallen, and only the first still holds.</b>
			// Both states also consult the BUS - FUN_0051a690 for its script state, FUN_0051aad0 for
			// whether one is here - and this used to add that no bus thing ran a script here, under which
			// the original's own answer for 21 was to do nothing. A bus now stands in the park and its
			// script runs: ParkFixedItems binds it, and the rides census reports it running. So these stay
			// unanswered on the unproven cell pair ALONE, and answering that would now be enough.
			case PeepState.PlayingSpotAnimation:
			case PeepState.GoingToMinorDestination:
			case PeepState.Leaving:
			case PeepState.PickingACellOutside:
			case PeepState.AtTheBusStop:
				break;

			// <b>And the guard this switch did not have.</b> Twenty-two states were declared, thirteen were
			// answered, and the nine that were not fell out of the bottom in silence - so a guest put into
			// one stood still for ever and nothing in the program could say so. Alexah found two of them by
			// playing the game. The original needs no default because its switch answers all twenty-two;
			// this one records rather than throws, because crashing a park is worse than a guest standing
			// still, and because a test can read a record.
			default:
				UnansweredState = peep.State;

				// And say so. This was recorded and never looked at by anything but a test, which is the
				// same silence the state machine had before it recorded anything at all.
				Unimplemented.Report( $"guest state {peep.State}" );

				break;
		}

		// <b>And the cell they are standing on is told, which is what makes the gate work at all.</b>
		// The original keeps a list of the things on each cell - head at the cell's +0x24, links on the
		// things themselves - and maintains it when a thing moves cell (FUN_0050b6a0), is created
		// (FUN_0050afe0) or destroyed. ParkState.StandOn answers the first two together.
		//
		// <b>This is called every turn, unconditionally, and that matters.</b> It was written as a
		// from/to move guarded by "did the cell change", and the one guest the shipped park leaves
		// STANDING STILL was then never entered into any cell's list at all - so the gate could not see
		// them, and they waited at the booth through a whole run while the five who walked there went
		// through. StandOn decides for itself that the cell is unchanged, exactly as FUN_0050b6a0 does.
		//
		// <b>It lives here rather than in ParkPeople on purpose.</b> Putting it in the driver would leave
		// it unreachable from every test that calls Step directly - which is precisely how GoingToRide
		// came to be set with no case, and how the ride loop came to be reported closed while nothing
		// drove it. Every caller goes through Step, so every caller keeps the list honest.
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
	/// the way <i>into</i> a walking state, through <c>FUN_00510100</c>; those entries are the parts of the
	/// state machine that are not built, and a guest restored from a file carries a destination and no route
	/// at all - the route being the one part of it the save deliberately does not keep. So the first turn of
	/// walking is what asks for one.
	/// </para>
	/// <para>
	/// <b>What has gone from here, though, is the standing animation on arrival.</b> That was the second
	/// departure this file's absence forced, and it is retired: a guest who arrives now enters a state, and
	/// the state queues its own animation through <see cref="Peep.AnimationFor"/> exactly as the original
	/// does. Every arrival above lands in a state that stands.
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
	/// writes 1 to <c>+0x188</c> and nought to <c>+0x1bc</c>, and nothing here reads either, so inventing
	/// names for them would be worse than leaving them out.
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
				// what ParkState.Take does: one call moving both, where this used to move a running
				// total the park's own balance knew nothing about.
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
	/// <b>The paid arm is NOT built, and the reason is a field rather than an omission.</b> A guest who has
	/// paid waits until the cell they are standing on names <i>them</i>: the original reads a short at
	/// <c>+0x24</c> of that cell's <b>runtime</b> record - 0x44 bytes each, against the 52 the file
	/// carries, so the offset cannot be translated into anything the save reader sees - and compares it
	/// against the guest's own thing id, which <c>FUN_0050b350</c> copies out of the front of the thing.
	/// That is the gate admitting one guest at a time, and nothing here keeps a mutable map cell or knows
	/// what writes that field. Three probes came back negative (<c>FUN_004dd0a0</c> destroys a thing,
	/// <c>FUN_0050afe0</c> constructs one, <c>FUN_004fa990</c> is an unrelated mode check); the next lead
	/// is <c>FUN_004d8480</c>, which the cell helpers all forward to.
	/// </para>
	/// <para>
	/// So a guest who has paid <b>stands and waits</b>, which is the honest thing for them to do and is
	/// what the original does on every turn the cell has not yet named them.
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

		// <b>And here is the paid arm, which this method went without until 2026-09-18.</b> A guest who has
		// paid goes through when the cell they are standing on NAMES THEM - the original reads a short at
		// the cell's +0x24 and compares it with the guest's own thing id.
		//
		// <b>That short is the head of the cell's thing list, not a reservation</b>, which is what took so
		// long to see: FUN_004d91f0 ends `cell[0x24] = thing`, and FUN_004d9280 repairs it. So the test
		// reads "am I the FIRST thing standing here?", and THAT is the gate letting one guest through at a
		// time - as each is admitted and steps off the cell, whoever is behind them becomes the head.
		// ParkState keeps the list; Step maintains it as guests move.
		//
		// <b>Alexah found this by playing: six of the park's guests stood at the ticket booths for the
		// whole of a run</b>, having judged the fee and paid, because nothing here ever let them through.
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
	/// Whether a route was found is deliberately not answered here: <see cref="Walked"/> asks again on the
	/// guest's next turn and reports a guest who cannot get through as having given up, so a failure has
	/// one place it is noticed rather than two.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b><c>internal</c> rather than <c>private</c> so that a ride can use it too.</b>
	/// <see cref="ParkRideOperation.Dismiss"/> puts a guest down at the ride's exit, and the original does
	/// that through the same pair of steps this does - a destination, then a route. Widening one method is
	/// cheaper than a second way of moving a peep, which is how the two would drift apart.
	/// </remarks>
	internal static void SendTo( Peep peep, PeepWalk walk, (int X, int Y) cell )
	{
		peep.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) );

		walk.PlanRoute();
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
	/// <b>The ride arm IS built now, and this said it was not until the scorer existed.</b>
	/// <see cref="ChooseSomewhereToGo"/> asks <see cref="ParkRideChooser"/>, which walks the world's object
	/// list, filters it with <see cref="ParkRideChoice"/> and scores the survivors with
	/// <see cref="ParkRideScore"/> - the seven-term weighted mean of distance, queue, excitement, thirst,
	/// hunger, relief and illness, weighted by the seven <c>PeepInfo.DecisionVar…Weight</c> constants and
	/// multiplied for newness and for shelter in the rain. A guest who finds nothing worth more than nine
	/// still does nothing, which is the original's own answer rather than a shortfall in this one.
	/// </para>
	/// <para>
	/// <b>Two of the original's own conditions are absent because they read fields nothing here has
	/// named.</b> The leave path is reached either when the park has shut or when two unidentified fields
	/// (<c>+0x1bc</c> and the value behind a float conversion) say so; only the shut-park half is
	/// reproduced, because guessing at the other would be inventing behaviour. The same goes for the
	/// need-driven arms at the top of the function, which fire on a need this project does not yet score.
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
	/// What a guest does on reaching something they chose - the arrival half of <c>FUN_004ffbc0</c>.
	///
	/// <para>
	/// <b>Joining is the original's own order:</b> the gates first, then the queue itself, then the place
	/// in it, and only then the state. A guest who fails a gate goes back to deciding with their choice
	/// let go of, rather than standing at a ride they cannot join.
	/// </para>
	/// <para>
	/// <b>Two arms of the original are deliberately NOT reproduced, each for a stated reason.</b> Its
	/// second queue gate (<c>FUN_004ddb60</c>) compares the length against a capacity from
	/// <c>FUN_004dda40</c>, which divides the ride's operating speed by a per-upgrade descriptor field at
	/// <c>+0x1a8</c> - the very field whose pairing <see cref="ParkRideScore"/> refuses to guess - so
	/// reproducing it would mean building a gate on an unverified number. The free-space gate below is
	/// the one that IS established, twice over: <c>FUN_004dda20</c> is literally
	/// <c>length &lt; mQueueSizeInCells * 4</c>. And the destination is the queue's END rather than the
	/// guest's own place in it, because turning a position into a cell means walking the queue path
	/// (<c>FUN_004de7e0</c>), which nothing here does.
	/// </para>
	/// </summary>
	private void JoinTheQueue( Peep peep, PeepWalk walk, int tick )
	{
		if ( peep.MajorDest == 0 || _park == null || Chosen( peep ) is not { } chosen )
		{
			GiveUpOnIt( peep, tick );

			return;
		}

		// The gate that is established - and note it is asked of the park as PLAYED, so a queue that
		// filled up while this guest was walking to it turns them away.
		if ( !ParkRideChoice.HasQueueRoom( chosen, State.QueueLength( chosen.ThingId ) )
			|| TurnsAwayFrom( peep, chosen ) )
		{
			GiveUpOnIt( peep, tick );

			return;
		}

		peep.QueuePos = State.JoinQueue( chosen.ThingId, peep.ThingId );

		// The back of the queue is a packed cell - decode by subtracting one FIRST, the same packing
		// mEntryPos and the patrol corners use. An object with none leaves them where they stand.
		if ( chosen.BackOfQueue != 0 )
		{
			SendTo( peep, walk,
				((chosen.BackOfQueue - 1) % ParkWorld.MapSize, (chosen.BackOfQueue - 1) / ParkWorld.MapSize) );
		}

		peep.SetState( PeepState.SteppingUpQueue, tick, _random );
	}

	/// <summary>
	/// How far out of place a guest will tolerate being before they re-take their position at once
	/// rather than waiting out <see cref="Peep.QueueMoveDelay"/> - the <c>2</c> in <c>FUN_004ffff0</c>.
	/// </summary>
	public const int QueueDriftAllowed = 2;

	/// <summary>
	/// Keeps a queueing guest's recorded place in step with the place the queue actually gives them -
	/// the middle arm of <c>FUN_004ffff0</c>, and <b>the fix for a park that died after one rider</b>.
	///
	/// <para>
	/// <b>Nothing renumbers a queue when somebody leaves it, in the original or here.</b>
	/// <see cref="ParkState.LeaveQueue"/> unlinks and fixes the head, exactly as <c>FUN_004ddd20</c>
	/// does, and neither touches anybody's <c>mQueuePos</c>. The original copes by recomputing it from
	/// the links every turn - <see cref="ParkState.PositionInQueue"/> - and this was the one arm of
	/// <see cref="PeepState.InQueue"/> that was left out. The cost was total:
	/// <see cref="ParkRideOperation.Invite"/> only calls forward a head whose place is nought, so once
	/// the first rider boarded, the guest who became head still held the 1 they had joined with and no
	/// further guest was ever invited. Three guests stood on one cell for two minutes of a measured run.
	/// </para>
	/// <para>
	/// <b>The drift is compared in unsigned BYTE arithmetic, and that is not a wart to tidy.</b> The
	/// original's field is a byte and it tests <c>2 &lt; mQueuePos - truePos</c> on it, so a guest whose
	/// true place is FURTHER BACK than their recorded one wraps to a large number and re-takes it
	/// immediately instead of waiting; signed arithmetic would have them sit out the delay instead.
	/// </para>
	/// <para>
	/// <b>What is deliberately not here is the walk.</b> Having recomputed the place, the original hands
	/// it to <c>FUN_00501160</c>, which turns it into a cell through <c>FUN_004de7e0</c> and sends the
	/// guest there as <see cref="PeepState.SteppingUpQueue"/>. That needs the queue-path walk - one cell
	/// per FOUR guests (<c>FUN_004de840</c>), or a virtual queue of at most four places for an object
	/// with no queue-path flag (<c>FUN_004dec30</c>) - together with the sub-cell placement its own
	/// direction byte decides. None of that is built, and <b>faking a destination would be worse than
	/// leaving it</b>: when the original cannot route a guest to their new place it makes them abandon
	/// the queue altogether. So the place is corrected and the guest stands still, which keeps the ride
	/// loading while the shuffle stays honestly unbuilt.
	/// </para>
	/// </summary>
	private void StepUpTheQueue( Peep peep )
	{
		if ( Chosen( peep ) is not { } queueing )
			return;

		var place = State.PositionInQueue( queueing.ThingId, peep.ThingId );

		// Not in the queue at all is the original's "Problem with a queue - shouldn't..." arm, which
		// gives up on it; a guest already in the right place has nothing to do.
		if ( place < 0 || peep.QueuePos == place )
			return;

		var drift = (peep.QueuePos - place) & 0xff;

		if ( peep.QueueMoveDelay != 0 && drift <= QueueDriftAllowed )
		{
			--peep.QueueMoveDelay;

			return;
		}

		peep.QueuePos = place;
	}

	/// <summary>The object this guest set off for, or null if the park no longer has it.</summary>
	private ParkWorld.CatalogueObject? Chosen( Peep peep )
	{
		if ( _park == null )
			return null;

		foreach ( var thing in _park.Objects )
		{
			if ( thing.ThingId == peep.MajorDest )
				return thing;
		}

		return null;
	}

	/// <summary>Lets go of what they chose and thinks again, which is where every refusal above ends.</summary>
	private void GiveUpOnIt( Peep peep, int tick )
	{
		peep.MajorDest = 0;
		peep.SetState( PeepState.Deciding, tick, _random );
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
	/// Offers this guest the best thing in the park and sets them off for it - <c>FUN_004fcb10</c>, and
	/// the arm this file recorded as unbuilt until the scorer existed to answer it.
	///
	/// <para>
	/// <b>The walk is committed to only once a route exists</b>, which is the order the original uses and
	/// the same one <c>StaffBehaviour.GoAndRest</c> follows: a guest never claims somewhere they cannot
	/// get to. A candidate that scores well but cannot be reached leaves them deciding again next turn,
	/// which is what the original does too.
	/// </para>
	/// <para>
	/// <b>The chosen thing is recorded in <see cref="Peep.MajorDest"/></b> - the person's own
	/// <c>+0x1dc</c>, which is where the original writes it and which the queueing states read back. It is
	/// written after the route for the same reason the state is.
	/// </para>
	/// <para>
	/// <b>Queue lengths ARE passed, and are measured from the park as played rather than as saved.</b> The
	/// save leaves <c>mFirstInQ</c> at nought on every object - nobody had ever queued in it - so every
	/// queue starts genuinely empty; but guests join them now, so the length has to be read live. This
	/// paragraph said they were not passed, while the call below already passed them.
	/// </para>
	/// </summary>
	/// <returns>Whether somewhere was chosen and a route to it planned.</returns>
	private bool ChooseSomewhereToGo( Peep peep, PeepWalk walk, int tick )
	{
		var (x, y) = walk.Position.Cell;

		var wants = new ParkRideScore.Wants( peep.PersonType,
			peep.Thirst, peep.Hunger, peep.Toilet, peep.Vomit );

		// Queues are measured from the park as it is being PLAYED, not as it was saved - a guest who joined
		// one a moment ago has to count.
		if ( _chooser.ChooseFor( wants, x, y, tick, queueLength: o => State.QueueLength( o.ThingId ) )
			is not { } chosen )
			return false;

		peep.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( chosen.EntryCellX ),
			PeepNavigator.WaypointCentre( chosen.EntryCellY ) );

		if ( !walk.PlanRoute() )
			return false;

		peep.MajorDest = chosen.ThingId;

		return true;
	}

	/// <summary>
	/// Sends a guest to a random reachable cell next to the one they are standing on - the guest half of
	/// <c>FUN_004f9490</c>, whose own log line is "Peep can't SetRandomDest anywhere".
	///
	/// <para>
	/// <b>The destination is a random point INSIDE the cell rather than its centre</b>, and that is the
	/// original's arithmetic rather than a choice: it masks a roll to <c>0x7f</c> and clamps it to 5..123
	/// of the 256 sub-cell units, so a wandering guest always aims at the near half of the target cell. A
	/// centre would have been tidier and would not be what the engine does.
	/// </para>
	/// <para>
	/// <b>What is deliberately left out.</b> The staff arms - patrol areas, and the fallback that tries
	/// five random cells within five of the guest before giving up - belong to the five person-kinds nothing
	/// here simulates. The stranded bookkeeping is also absent: the original stamps a "do not try again
	/// until" time and raises a thought bubble, and neither the stamp nor the thought system exists here, so
	/// a guest who can reach nowhere simply stays where they are and is asked again.
	/// </para>
	/// </summary>
	/// <returns>Whether somewhere was found and a route to it planned.</returns>
	private bool SetRandomDest( Peep peep, PeepWalk walk )
	{
		var (x, y) = walk.Position.Cell;

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
	/// Whether the cell being entered says it connects the way we are coming from - the stored
	/// <c>mNeighbours</c> mask, bit-tested, which is what the original builds its wander candidates from.
	///
	/// <para>
	/// <b>Alexah found this by playing: guests walked out of the park and down the road.</b> The original
	/// picks a wander destination in <c>FUN_004f9490</c> from the byte <c>FUN_00522770</c> hands back -
	/// the cell's own <c>+0xc</c> - and <b>only 91 of this park's 16,384 cells carry a non-zero one</b>.
	/// The road outside is cell type 30 and its mask is nought, so the engine can never choose it; ours
	/// asked only whether an edge was walkable, and the road's edges are.
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

		return (_park.CellAt( x, y ).Neighbours & CellEdge.BitFor( direction )) != 0;
	}

	/// <summary>The near edge of a cell plus a clamped roll - see <see cref="SetRandomDest"/>.</summary>
	private int SomewhereIn( int cell )
	{
		var within = Math.Clamp( _random.Next() & 0x7f, 5, 0x7b );

		// A cell is 256 of these sub-units and One is a whole cell, so a sub-unit is One / 256.
		return (cell * PeepNavigator.One) + (within * (PeepNavigator.One / 256));
	}
}
