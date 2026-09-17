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
/// handler.</b> That distinction decides what can be built: the inline cases are 0, 7, 8, 12, 14, 16, 17
/// and 20, and <b>no guest in Lost Kingdom is in any of them</b> - the park's thirteen are in
/// <see cref="PeepState.HeadingForGate"/>, <see cref="PeepState.Entering"/> and
/// <see cref="PeepState.WaitingForOpening"/>, all three of which delegate. Building "the inline states"
/// would therefore have been a green feature that moved nobody, which is exactly the mistake the walk
/// itself made once when four states were read where eleven were meant.
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
/// goes on to decide what to do. Of that loop the two arrivals are here and the middle is not - see
/// <see cref="Step"/> for each deferral and the reason it is deferred.
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
	public PeepBehaviour( ParkWorld? park, Random? random = null,
		ParkAdmission? admission = null, Func<int>? gateStatus = null )
		// Zero is open, which is the way round the name is not - see ParkWorld.ParkClosed.
		: this( park is not null && park.ParkClosed != 0, park?.NumberOfVisitorsToDate ?? 0, random,
			admission, gateStatus )
	{
	}

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
		ParkIsClosed = parkIsClosed;
		VisitorsToDate = visitorsToDate;
		Admission = admission;
		_gateStatus = gateStatus;
		_random = random ?? new Random();
	}

	private readonly Func<int>? _gateStatus;

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
	public int Takings { get; private set; }

	/// <summary>
	/// What the park's rides are worth to a guest deciding whether the price is fair - the sum
	/// <c>FUN_004c8240</c> makes.
	///
	/// <para>
	/// <b>Nought, and by the shipped park's own saved state rather than by omission.</b> That sum counts
	/// only things with somebody in their queue, and the save records <c>mNumberOfVisitorsToDate</c> as
	/// nought - nobody has ever been admitted, so no queue can hold anyone. Nothing in this tree operates
	/// a ride either. It is settable so that the term is visible and testable rather than a zero nobody
	/// can see.
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
	public bool ParkIsClosed { get; }

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
	public int VisitorsToDate { get; private set; }

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
	/// One turn of one guest's behaviour.
	///
	/// <para>
	/// <b>The switch comes first and the walk second, which is the original's order and not a detail.</b>
	/// <c>FUN_005019f0</c> decides what state a guest is in and only then asks whether they got anywhere, so
	/// a guest in a state that does not walk never reaches the walk at all. Doing it the other way round -
	/// walking everybody who <i>can</i> walk and then asking what it meant - is what this replaces.
	/// </para>
	/// <para>
	/// <b>What is deliberately absent, each with its reason.</b>
	/// <see cref="PeepState.WaitingForOpening"/> (<c>FUN_004ff7f0</c>) needs the gate thing's script state
	/// and a destination chosen from two gate cells held in globals; <see cref="PeepState.JudgingTheFee"/>
	/// (<c>FUN_004ff9d0</c>) needs the admission fee, which lives on the economy thing the header calls
	/// <c>mBankAccount</c> and nothing reads yet; <see cref="PeepState.Deciding"/> (<c>FUN_004fec90</c>) is
	/// the hub that picks a ride, a stall or a way home. A guest who reaches one of those <b>stops there</b>,
	/// and stopping is the honest thing for them to do rather than a state machine guessing.
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
	/// <see cref="PeepState.PlayingSpotAnimation"/> and <see cref="PeepState.InQueue"/>, neither of which
	/// is built, so it is named rather than guessed at.
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
					peep.VisitorNumber = ++VisitorsToDate;
					peep.SetState( PeepState.Deciding, tick, _random );
				}

				break;

			// Shuffling up a queue, which ends the same way whether they got there or gave up - the
			// original writes the same state from both arms of the test, and that is not a mistake to
			// tidy: a guest who cannot shuffle forward is still in the queue.
			case PeepState.SteppingUpQueue:
				if ( Walked( peep, walk, playing ) != WalkVerdict.Walking )
					peep.SetState( PeepState.InQueue, tick, _random );

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
		}
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

				// FUN_004d0600 - the fee goes on the balance and on the year's profit alike.
				Takings += admission.Fee;

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

		// And the paid arm falls off the end on purpose - see the remarks above.
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
	private static void SendTo( Peep peep, PeepWalk walk, (int X, int Y) cell )
	{
		peep.Navigator.Target = new FixedVector(
			PeepNavigator.WaypointCentre( cell.X ), PeepNavigator.WaypointCentre( cell.Y ) );

		walk.PlanRoute();
	}
}
