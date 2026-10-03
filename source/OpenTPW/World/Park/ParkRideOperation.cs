namespace OpenTPW;

/// <summary>
/// What a ride does to its own queue on its turn - the part of the original's per-object tick that keeps
/// a queue honest, from <c>FUN_004e0b90</c>.
///
/// <para>
/// <b>A ride is ticked like anything else in the park.</b> <c>FUN_0050b360</c> switches on a thing's
/// model byte and hands model 3 to <c>FUN_004e0b90</c> and then <c>FUN_004e0e00</c>, exactly as it hands
/// a guest to their needs and then their behaviours. <b>Both halves run</b>, script and all, but not
/// whole: the first half's breakdown request and worn flag, and the state-0 turn's broken and condemned
/// transitions, are unbuilt (<c>docs/exe/ride-operation.md</c>, "The first half of the turn" and
/// "The second half").
/// </para>
/// <para>
/// <b>Ride operation is script-driven, and that is why only this much is here.</b> The object holds a
/// script handle at <c>+0x24</c>, and the engine talks to the ride through its script's variables -
/// <b>0</b> admit, <b>1</b> dismiss, <b>4</b> breakdown, <b>6</b> closed, <b>7</b> broken,
/// <b>8</b> worn. <see cref="ParkRides"/> binds a script to every placed object and pushes the save's own
/// capacity and duration into it; and <c>FUN_004e0450</c> admitting when variable 0 <i>differs</i> from
/// the head of the queue is the <c>VAR_LETMEON</c> handshake:
/// the engine fills that slot and the script zeroes it to acknowledge, so an empty slot is the script
/// saying it is ready. See <see cref="CompleteAdmission"/>, which reads it the right way round.
/// </para>
/// </summary>
public sealed class ParkRideOperation
{
	private readonly ParkState _state;
	private readonly IReadOnlyDictionary<int, Peep> _guests;
	private readonly ParkAdmission? _admission;
	private readonly ParkRideScore? _score;
	private readonly ParkSpriteBanks _banks;

	/// <param name="state">The park as it is being played, which owns the queues.</param>
	/// <param name="guests">Every guest by thing id, for asking what the head of a queue is doing.</param>
	/// <param name="admission">
	/// The park's own mood constants, for the settle-up alone - <c>PeepInfo.MediumHappinessChange</c>, which
	/// is <b>both</b> the penalty for getting nothing out of a visit and the multiplier on what winning is
	/// worth. <b>One balance value with two uses, not two settings.</b> Null leaves both arms alone rather
	/// than moving happiness by a number nobody read, which is what a test asking about the money means.
	/// </param>
	/// <param name="score">
	/// What each kind of guest likes, for the settle-up's excitement match (<see cref="MatchTheExcitement"/>),
	/// which also needs <paramref name="admission"/>. Null leaves the match alone, as a null admission does.
	/// </param>
	/// <param name="banks">
	/// How many banks of each guest kind the park draws over (<see cref="ParkSpriteBanks"/>): a balloon's colour, a
	/// costume and the child a costume gives back are drawn over them. Null counts none, which gives a guest a balloon's
	/// life and no balloon, as the original does with no sprite table.
	/// </param>
	public ParkRideOperation( ParkState state, IReadOnlyDictionary<int, Peep> guests,
		ParkAdmission? admission = null, ParkRideScore? score = null, ParkSpriteBanks? banks = null )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( guests );

		_state = state;
		_guests = guests;
		_admission = admission;
		_score = score;
		_banks = banks ?? new ParkSpriteBanks( 0, 0, 0 );
	}

	/// <summary>
	/// Whether this visit gave the guest what they came for - the original's <c>FUN_004e2670</c>, rolled as
	/// a guest enters the thing and written into their <c>mQueuePos</c> by <c>FUN_00501db0</c>'s case
	/// <c>0xe</c>.
	///
	/// <para>
	/// <b>It is <c>r % 100 &lt;= chance</c>, and the chance lives on the OBJECT at <c>+0x190</c></b>,
	/// built as <c>100 - UsageInfo.InitChanceOfLoosing</c> when the thing is made. <c>FUN_004e2670</c> fixes
	/// it as the object's, because the same pointer it reads is the one it takes the catalogue
	/// id and the script handle from, and both of those are object fields.
	/// </para>
	/// <para>
	/// <b>Two departures.</b> The original draws <c>r</c> from the park's own generator (<c>FUN_00516330</c>,
	/// <c>0x004e26c6</c>), one draw for every admission to anything, where this draws from the
	/// <see cref="Random"/> it is handed; and this reads the item's chance, not the object's (Q97).
	/// </para>
	/// <para>
	/// <b>A shop always wins, and that is the mechanism rather than an accident.</b> Nothing in the
	/// <c>shops</c> folder declares a chance of losing, so a shop's chance is a hundred and the roll cannot
	/// fail - which is why a drink is always served. The Jungle Spray declares 75 in its own file, so its
	/// chance is <b>25</b>. The original's own assertion says the same from the other side: it insists the
	/// object is a sideshow <i>or</i> that this value is a hundred.
	/// </para>
	/// </summary>
	public static bool Succeeds( ParkItemCatalogue.Item item, Random random )
	{
		ArgumentNullException.ThrowIfNull( random );

		return random.Next() % 100 <= item.ChanceOfWinning;
	}

	/// <summary>
	/// Whether this guest is in a queue as far as the engine is concerned - <c>FUN_00502430</c>.
	/// </summary>
	/// <remarks>
	/// <b>Four states count, and the fifth defers.</b> The original reads the state at <c>+0x220</c>, and
	/// <b>if it is 8 it reads the saved state at <c>+0x224</c> instead</b> - eight being
	/// <see cref="PeepState.PlayingSpotAnimation"/>, the one state that exists to be returned from. It
	/// then answers true for anything above ten and below fifteen, which is
	/// <see cref="PeepState.InQueue"/>, <see cref="PeepState.SteppingUpQueue"/>,
	/// <see cref="PeepState.BeingAdmitted"/> and <see cref="PeepState.EnteringRide"/>.
	/// <para>
	/// That the range lands exactly on those four is a check on this project's own state numbering,
	/// arrived at from a completely different direction - the switch in <c>FUN_005019f0</c>.
	/// </para>
	/// </remarks>
	public static bool IsQueueing( Peep peep )
	{
		ArgumentNullException.ThrowIfNull( peep );

		var state = peep.State == PeepState.PlayingSpotAnimation ? peep.SavedState : peep.State;

		return state is PeepState.InQueue or PeepState.SteppingUpQueue
			or PeepState.BeingAdmitted or PeepState.EnteringRide;
	}

	/// <summary>
	/// Drops the head of a ride's queue if whoever it names has stopped queueing - the tail of
	/// <c>FUN_004e0b90</c>, which every ride runs on every turn of its own tick.
	///
	/// <para>
	/// <b>This is what stops a queue outliving the guest at the front of it.</b> A guest can leave the
	/// queueing states without the queue being told: they give up, the park shuts under them, or they are
	/// sent home. The original answers it the blunt way - if the head names nobody the engine knows, or
	/// names somebody who is not queueing, the head is simply cleared.
	/// </para>
	/// <para>
	/// <b>It clears the head and nothing else</b>, which is the original's own reach and worth not
	/// tidying: the rest of the chain is left exactly as it was, so the second guest is not promoted here.
	/// Whoever is behind them keeps a <c>mQPrev</c> pointing at somebody no longer at the front, and the
	/// original lives with that until the next join or leave repairs it.
	/// </para>
	/// </summary>
	/// <returns>Whether a stale head was dropped.</returns>
	public bool DropStaleQueueHead( int rideId )
	{
		var head = _state.FirstInQueue( rideId );

		if ( head == 0 )
			return false;

		if ( _guests.TryGetValue( head, out var peep ) && IsQueueing( peep ) )
			return false;

		_state.ClearQueueHead( rideId );

		return true;
	}

	/// <summary>
	/// Runs <see cref="DropStaleQueueHead"/> for every object the save placed. <b>Dead by CODE:</b> only a
	/// test calls it; the park's own turn (<c>ParkPeople.TakeTheRidesTurns</c>) asks each object itself.
	/// </summary>
	/// <returns>How many stale heads were dropped.</returns>
	public int DropStaleQueueHeads( IParkInitialState? park )
	{
		if ( park == null )
			return 0;

		var dropped = 0;

		foreach ( var thing in park.Objects )
		{
			if ( DropStaleQueueHead( thing.ThingId ) )
				++dropped;
		}

		return dropped;
	}

	/// <summary>
	/// Takes a guest out of a queue the whole way the original does - <c>FUN_004ddd20</c>: the ride's
	/// admission slot is emptied when it names them (<c>0x004ddd4e</c>..<c>0x004ddd7d</c>), and then
	/// <see cref="ParkState.LeaveQueue"/> joins up whoever stood either side of them.
	/// </summary>
	/// <remarks>
	/// It splices with the leaver's own links and tests no membership (<c>0x004ddde9</c>), so a leaver the queue
	/// walk could not reach is still spliced out, and one with nobody in front moves the head: see
	/// <see cref="ParkState.LeaveQueue"/>.
	/// </remarks>
	/// <returns>Whether they were at its head or linked to anybody when they left.</returns>
	internal static bool LeaveQueue( ParkState state, RideScript? script, int objectId, int guestId )
	{
		ArgumentNullException.ThrowIfNull( state );

		if ( script != null && guestId != 0 && script[AdmitVariable] == guestId )
			script.Set( AdmitVariable, 0 );

		return state.LeaveQueue( objectId, guestId );
	}

	/// <summary>The script variable a ride is handed its next rider in - <b>by name, never by index</b>.</summary>
	/// <remarks>
	/// The name matters rather than the number: a script numbers its variables in the order it declares
	/// them, and a ride archive's companion scripts declare none of the common set - see
	/// <see cref="RideVariables"/>, and <c>ParkRides</c>, which reaches the gate's variables the same way.
	/// </remarks>
	public const string AdmitVariable = "VAR_LETMEON";

	/// <summary>And the one it reports whoever has come off in - see <see cref="Dismiss"/>.</summary>
	public const string DismissVariable = "VAR_LETMEOFF";

	/// <summary>
	/// The slot a thing's script is told how the visit went in - <b>variable 11</b>, which every item's main
	/// script declares as <c>VAR_PARAM</c>.
	///
	/// <para>
	/// <b>It is what makes a sideshow play the right animation.</b> <c>FUN_004e2670</c> writes the roll into
	/// it as a guest enters, and <c>Junspray.RSE</c> copies it per lane
	/// (<c>COPY VAR_LANERESn, VAR_PARAM</c>) and then branches on it to trigger one of two clips - the
	/// winning one or the losing one. Left unwritten, every guest gets the losing animation.
	/// </para>
	/// <para>
	/// <b>Reached by name like everything else here</b>, though this one is written to scripts that all
	/// declare the common twelve in order, so the name and the index agree.
	/// </para>
	/// </summary>
	public const string OutcomeVariable = "VAR_PARAM";

	/// <summary>
	/// Hands the ride's script the guest it has nominated - the original's <c>FUN_004e0900</c>, whose own
	/// line is "Object %d: AdmitPerson - person b...".
	///
	/// <para>
	/// <b>It writes only into an EMPTY slot, and that is the whole handshake.</b> The script consumes
	/// <c>VAR_LETMEON</c> and writes nought back over it (<c>TEST</c> / <c>BOUNCE</c> / <c>COPY x, 0</c>),
	/// so an empty slot is how the script says it is ready for another rider. Writing into a full one
	/// would drop whoever was already there, which is why the original refuses instead.
	/// </para>
	/// <para>
	/// <b>Every refusal of the original's is reproduced, and no other.</b> The object's <c>+0x68</c> is
	/// <c>mCanLoad</c>, which <see cref="ParkRideChoice.CanBeOffered"/> refuses on too, and the two ride
	/// states it refuses on are the pair <see cref="ParkRideChoice"/> already names. Past those it lets go
	/// of the nominee whoever asked, and a full slot refuses only after that.
	/// </para>
	/// <para>
	/// <b>A ride closed while its nominee walked up refuses them here</b> - <see cref="Close"/> lets the
	/// nominee go and clears <c>mCanLoad</c> - and the ride's own turn then puts them out of the queue.
	/// </para>
	/// </summary>
	/// <returns>Whether the guest was handed over.</returns>
	public bool AdmitPerson( RideScript? script, ParkWorld.CatalogueObject ride, int personId )
	{
		if ( script == null || personId == 0 )
			return false;

		// Out of service: the original logs "Object %d, type %d: cannot admit..." and gives up.
		if ( ride.State is ParkRideChoice.StateRefusedOne or ParkRideChoice.StateRefusedFour )
			return false;

		// mCanLoad, tested for non-zero rather than for a value - the same reading ParkRideChoice takes.
		if ( ride.CanLoad == 0 )
			return false;

		// "admitting wrong person": the original only logs it (0x004e092c) and admits whoever asked.
		var nominee = _state.PersonBeingLoaded( ride.ThingId );

		if ( nominee != personId )
			Log.Warning( $"Object {ride.ThingId}: mPersonBeingLoaded is {nominee}, but admitting person {personId}"
				+ " - admitting wrong person" );

		// The nomination is let go of BEFORE the slot is asked (0x004e09b0), so a refusal there leaves the
		// ride free to call somebody else forward (docs/exe/ride-operation.md, "At the door").
		_state.NominateForLoading( ride.ThingId, 0 );

		if ( script[AdmitVariable] != 0 )
		{
			Log.Info( $"Object {ride.ThingId}: cannot admit person {personId}, "
				+ $"script changed its mind about admission! ({script[AdmitVariable]})" );

			return false;
		}

		return script.Set( AdmitVariable, personId );
	}

	/// <summary>
	/// Finishes an admission the script has taken up - the first arm of <c>FUN_004e0450</c> and the
	/// <c>FUN_00500870</c> it calls. Its other arm, which puts the head out, is the caller's:
	/// <c>ParkPeople.CompleteOrTurnAway</c>.
	///
	/// <para>
	/// <b>The trigger is the slot being EMPTY while somebody is still at the head of the queue</b>, which
	/// is the original's "variable 0 differs from <c>mFirstInQ</c>" read the right way round: the script
	/// has taken the rider and zeroed the slot, so the head can now be taken out of the queue. They must
	/// be in <see cref="PeepState.EnteringRide"/> to be ready for it.
	/// </para>
	/// <para>
	/// The original asserts the guest keeps no queue links afterwards ("Person not correctly removed from
	/// queue") - <see cref="ParkState.LeaveQueue"/> clears both, and the tests check it.
	/// </para>
	/// </summary>
	/// <returns>Whether a guest was taken out of the queue and put on the ride.</returns>
	public bool CompleteAdmission( RideScript? script, int rideId, int tick, Random random )
	{
		ArgumentNullException.ThrowIfNull( random );

		if ( script == null )
			return false;

		var head = _state.FirstInQueue( rideId );

		if ( head == 0 || script[AdmitVariable] == head )
			return false;

		if ( !_guests.TryGetValue( head, out var peep ) || peep.State != PeepState.EnteringRide )
			return false;

		_state.LeaveQueue( rideId, head );
		peep.SetState( PeepState.Riding, tick, random );

		return true;
	}

	/// <summary>
	/// Lets off whoever the script has reported - <c>FUN_004e1410</c>.
	///
	/// <para>
	/// <b>This slot runs the other way.</b> The script WRITES it, filling <c>VAR_LETMEOFF</c> with whoever
	/// came off (or leaving nought when nobody did), and the engine clears it once they are on their way.
	/// That is why a script skips its dismissal while the slot is still full - the ride has not been
	/// collected from yet.
	/// </para>
	/// <para>
	/// <b>WHICH instruction does the writing depends on the ride, and there are six.</b> Across the 22
	/// Lost Kingdom ride scripts: <c>UNBOUNCE</c>/<c>FORCEUNBOUNCE</c> (Bouncy alone), <c>BUMP 2</c>
	/// (bumper, GoKarts, Wateride), <c>COAST 3</c> (the three coasters), <c>WALKGET</c> (incagod, Lookout,
	/// Totem, tvsim), <c>HOP</c> with <c>DELHEAD</c> (Mumbo, PorkPie, Spider, Volcano, Monkey), and
	/// <c>TOUR 4</c> (TourRide).
	/// </para>
	/// <para>
	/// <b>They are not blocked on the same thing, either.</b> <c>COAST 3</c> is implemented and its
	/// coasters still never dismiss anybody, because it drains the finished-rider queue that nothing
	/// fills - see <see cref="RideState.FinishRider"/>, which marks where a ride simulation will connect.
	/// That is a missing simulation, not a missing opcode.
	/// </para>
	/// <para>
	/// <b>They are put down at the ride's EXIT, which is a different cell from the one they queued at.</b>
	/// <c>FUN_004e1410</c> calls <c>FUN_005014e0</c> - whose own line is "Person %d: ExitRide, leaving
	/// rid..." - and that asks <c>FUN_004dedf0( thing, 1, &amp;x, &amp;y )</c> for the exit point. The
	/// non-zero argument is what selects the exit over the stand point; see
	/// <see cref="ParkWorld.CatalogueObject.ExitCellX"/> for why only one object in the shipped park can
	/// tell that decode from a wrong one.
	/// </para>
	/// <para>
	/// <b>A guest is PUT DOWN at the exit and then aimed one cell PAST it, and a walk to the exit itself is
	/// one the map refuses.</b> <c>FUN_005014e0</c> reads the exit point, calls
	/// <c>FUN_004fa930</c> to set the person's position outright, and only then sets a destination: the
	/// neighbour of the exit cell in the direction that cell faces, flipped to the opposite when
	/// <c>mExitPos</c> equals <c>mEntryPos</c> (which is true of ten of this park's eleven objects). Walking
	/// a guest TO the exit cannot work: <see cref="CellEdge"/> only opens a ride end along
	/// the way it faces, so the route fails and the guest gives up where they stand.
	/// </para>
	/// <para>
	/// <b>The failure arm is deliberately NOT reproduced, and it is drastic rather than quiet.</b> When the
	/// destination will not route the original refuses the dismissal and calls <c>FUN_004df150</c>, which
	/// <i>closes the ride</i> as <see cref="Close"/> does, after posting a type-<c>0x14</c> message, and only
	/// when it is open. The player's way to open that one ride again, the ride window's door, is unbuilt, so a
	/// ride shut over a routing failure would open only when its queue was next measured
	/// (<see cref="ReopenAfterRemeasure"/>) or the park's door was shut and opened again. A guest whose
	/// neighbour will not route is dismissed anyway and drops to <see cref="PeepState.Deciding"/> standing on
	/// the exit, which is where they are - by the walk off's give-up arm, which keeps
	/// <see cref="Peep.SavedMajorDest"/> for their next walk off anything, where the original never enters that
	/// walk at all.
	/// </para>
	/// </summary>
	/// <param name="walkFor">
	/// How to find a guest's walk by thing id, or null where there is nowhere to move anyone.
	/// <b>A LOOKUP rather than one walk, and that is not a stylistic choice.</b> Which guest comes off is
	/// decided inside this method, by whoever the script has named in <c>VAR_LETMEOFF</c> - so a caller
	/// cannot know whose walk to hand over.
	/// </param>
	/// <returns>Whether a guest was let off.</returns>
	public bool Dismiss( RideScript? script, ParkWorld.CatalogueObject ride, int tick, Random random,
		Func<int, PeepWalk?>? walkFor = null, IParkInitialState? park = null, ParkItemCatalogue? catalogue = null )
	{
		ArgumentNullException.ThrowIfNull( random );

		if ( script == null )
			return false;

		var leaving = script[DismissVariable];

		if ( leaving == 0 )
			return false;

		// The original asserts this rather than testing it - "trying to dismiss person %d" - so somebody
		// who is not on the ride is a fault in the script, not a case to handle quietly.
		if ( !_guests.TryGetValue( leaving, out var peep ) || peep.State != PeepState.Riding )
			return false;

		// An object that declares no exit has nowhere to put them, which is every unplaced one - its
		// mExitPos is the sentinel that unpacks to the corner of the map.
		if ( ride.ExitPos != 0 && walkFor?.Invoke( leaving ) is { } walk )
			PutDownAtTheExit( peep, walk, ride, park );

		SettleUp( peep, ride, catalogue, random, tick );

		peep.SetState( PeepState.LeavingRide, tick, random );

		// Case 0xf of the state setter builds the balloon boarding took away, in the same colour, when there is life
		// left and the thing left does not give balloons itself (0x00501fd3..0x0050208a): no event, the life as it was.
		// The original asks it of mMajorDest, the thing being left.
		if ( peep.BalloonLife != 0 && ItemOf( ride, catalogue )?.AppearanceEffect != Balloon.AppearanceEffect )
			peep.Balloon = Balloon.Make( peep.ThingId, _banks.BalloonSets, SpriteClock( tick ) );

		return script.Set( DismissVariable, 0 );
	}

	/// <summary>
	/// Puts a guest down on the ride's exit cell and aims them one cell beyond it - the first half of
	/// <c>FUN_005014e0</c>, which is <c>FUN_004fa930</c> (place) followed by <c>FUN_004fa530</c> (aim).
	///
	/// <para>
	/// <b>The placement is a teleport, and the original's is too.</b> <c>FUN_004fa930</c> writes the
	/// position with zero velocity rather than setting a destination, because a guest on a ride is not
	/// standing anywhere a route could start from. Doing it through the navigator and then re-planning is
	/// what makes it stick: <see cref="PeepWalk"/> keeps two views of the position and writes the steering
	/// one back at the end of every step, so a position set without re-seeding them would be silently
	/// undone on the guest's next turn.
	/// </para>
	/// <para>
	/// <b>The sub-cell part of the exit point is not reproduced.</b> <c>FUN_004dedf0</c> builds a
	/// fixed-point position whose low byte comes from the item's own <c>.sam</c> - the descriptor's
	/// <c>+0xdc</c> and <c>+0xe0</c>, which it validates with "Dodgy X exit point in SAM file" - and
	/// nothing here reads those. The cell's centre is used instead, which puts a guest in the right cell
	/// and up to half a cell from the exact spot.
	/// </para>
	/// <para>
	/// <b>Without a park there is no direction to read</b>, so the guest is put down and left: the cell's
	/// facing lives on the map, and a caller with no world is a test asking about the state change rather
	/// than about the geography.
	/// </para>
	/// <para>
	/// <b>The placement happens whatever is beyond the exit, and that is the original's behaviour rather
	/// than a convenience here.</b> Alexah has watched it: in the original, a ride whose exit is not
	/// connected to the rest of the park still <i>teleports</i> the guest onto the exit, and they then
	/// stand on it with a <b>?</b> over their head. The decompilation agrees - <c>FUN_004fa930</c> is
	/// called before the facing is so much as read - so the position is set first and unconditionally,
	/// and only the aim can fail.
	/// </para>
	/// <para>
	/// <b>What cannot be shown is the ? itself.</b> If it is thought <c>0x11</c>, the stranded bubble, only
	/// <c>FUN_004f9490</c> raises it, at its linked walk's dead end or its refusal after one, and which
	/// picture it shows is not established (<c>docs/exe/ride-operation.md</c>, "Leaving a ride"). This
	/// project has no thought
	/// system at all - see <see cref="PeepBehaviour.SetRandomDest"/>, which records the same absence from
	/// the other side. So a guest who cannot leave the exit stands there silently instead of asking.
	/// </para>
	/// </summary>
	private static void PutDownAtTheExit( Peep peep, PeepWalk walk, ParkWorld.CatalogueObject ride,
		IParkInitialState? park )
	{
		peep.Navigator.Position = new FixedVector(
			PeepNavigator.WaypointCentre( ride.ExitCellX ),
			PeepNavigator.WaypointCentre( ride.ExitCellY ) );

		// A teleport carries its previous position with it, or the drawing would slide the guest all the
		// way from the queue to the exit over the next quarter of a second. The original does exactly this
		// at 0x004fa95d, re-stamping previous := current at the end of its own place-a-peep routine.
		peep.Navigator.StampPrevious();

		if ( park == null )
		{
			// Re-plan anyway, so the two views are seeded from where they now are.
			walk.PlanRoute();

			return;
		}

		// The running park's cell, not the file's - a queue a player has laid at a ride's exit since the
		// park loaded exists only in the overlay, and this is the test that keeps a dismissed guest from
		// being aimed into one. See ParkRideChoice.LiveCell, which reads the same seam for the same reason.
		var exit = ParkState.CellFor( park, ride.ExitCellX, ride.ExitCellY );

		// mExitPos == mEntryPos is ten of this park's eleven objects, and for those the original turns
		// the cell's facing round before stepping off it - FUN_004d8c00.
		var facing = ride.ExitPos == ride.EntryPos ? CellEdge.Opposite( exit.Direction ) : exit.Direction;

		// The bit names a side of the exit cell itself, and FUN_004d97e0 steps through it: 0x04 east, 0x10 south,
		// 0x40 west, 0x01 north. DirectionFor answers from the side of the cell being entered, so the side is turned
		// round first.
		if ( CellEdge.DirectionFor( CellEdge.Opposite( facing ) ) is not { } towards )
		{
			walk.PlanRoute();

			return;
		}

		var (nextX, nextY) = MapStep.Beyond( ride.ExitCellX, ride.ExitCellY, towards );

		var beyond = ParkState.CellFor( park, nextX, nextY );

		if ( !ParkState.OnMap( nextX, nextY ) || CellEdge.IsQueue( beyond.Type ) )
		{
			// The original refuses the dismissal here rather than aiming them into a queue - see the
			// remarks on Dismiss for why the refusal itself is not reproduced.
			walk.PlanRoute();

			return;
		}

		PeepBehaviour.SendTo( peep, walk, (nextX, nextY) );
	}

	/// <summary>
	/// Everything leaving a visitable thing does to a guest - <c>FUN_004fd970</c>, of which the charge is
	/// one arm rather than the whole.
	///
	/// <para>
	/// <b>The effects are gated, and the gate is the win roll.</b> The original
	/// splits on the guest's <c>+0x1f1</c>: nought logs "Person lost this sideshow..." and docks
	/// happiness, and anything else runs the effects. That byte is <c>mQueuePos</c> by the save reader's
	/// own naming, but every rider has been through <c>FUN_00501db0</c>'s case <c>0xe</c>, which
	/// overwrites it with <c>FUN_004e2670</c>'s roll for any kind of thing - and every thing but a
	/// sideshow has a chance of a hundred, so for them it is always the effects (<c>docs/exe/ride-operation.md</c>,
	/// "<c>+0x1f1</c> at the settle-up is the win roll").
	/// </para>
	/// <para>
	/// The byte is written on admission by <see cref="PeepBehaviour"/>'s roll through <see cref="Succeeds"/>,
	/// and the "lost" arm docks <c>PeepInfo.MediumHappinessChange</c> (<c>DAT_0078505c</c>), both built.
	/// Before the gate, on both arms, the guest counts the visit by the item's kind and the object counts a
	/// customer (<c>docs/exe/ride-operation.md</c>, "The settle-up's bookkeeping", steps 1 and 2). Behind the gate
	/// come the cost of goods a shop or sideshow books (<see cref="ParkState.BookCostOfGoods"/>) and a sideshow's
	/// prize, the excitement match (<see cref="MatchTheExcitement"/>), the item's effects, a toilet's relief
	/// (<see cref="UseTheToilet"/>) and a sideshow winner's count and cheer, in the original's order; then three times
	/// the happiness gained since the guest joined the queue, averaged into the object's satisfaction for the day, and
	/// the object's served count (steps 4, 5 and 7). Counted and not kept: the guest's event history, the park
	/// analyser's sample and a sideshow's thoughts, win or lose.
	/// </para>
	/// <para>
	/// After the five effects come the object's own terms, on its amount of special ingredient
	/// (<see cref="TakeTheIngredient"/>), then the appearance effect: a balloon (<see cref="GiveABalloon"/>) or a
	/// costume (<see cref="DressOrUndress"/>) (<c>docs/exe/ride-operation.md</c>, "The effects of a visit", 4).
	/// </para>
	/// <para>
	/// <b>The visit is remembered first</b> (<see cref="Peep.RememberVisit"/>, <c>0x004fd98b</c>), before the
	/// charge, whatever the thing is: a shop, a sideshow or a toilet counts as much as a ride. The original reaches it
	/// only when the exit routes (<c>0x005015e3</c>, <c>0x005015ef</c>); a guest this dismisses anyway (see
	/// <see cref="Dismiss"/>) is remembered and charged anyway.
	/// </para>
	/// </summary>
	private void SettleUp( Peep peep, ParkWorld.CatalogueObject ride, ParkItemCatalogue? catalogue, Random random, int tick )
	{
		peep.RememberVisit( ride.ThingId );

		// No catalogue is a test asking about the money rather than about the visit, and an item the
		// catalogue does not know cannot say what it does to anybody.
		var known = ItemOf( ride, catalogue );

		// The guest counts the visit by the item's kind, before the charge (0x004fd9ac..0x004fd9d2).
		if ( known is { } kind )
			CountTheVisit( peep, kind.UiType );

		Charge( peep, ride, known?.UiType );

		// And the object counts a customer, won or lost (FUN_004e1690, 0x004fd9e2).
		var rings = _state.RingsFor( ride.ThingId );
		rings.CountCustomer();

		// The original then takes the item's FatigueEffect off the guest's mTiredness, held to 0..100
		// (0x004fd9e7..0x004fda00). Nothing raises mTiredness: the constructor zeroes it, a load restores the saved one
		// (file 521, nought on every shipped guest), and a subtraction from nought is held at nought, so nothing is
		// done here.

		if ( known is not { } item )
			return;

		// <b>The gate, and it is the ROLL rather than a place in a queue.</b> Entering the thing writes
		// Succeeds() into mQueuePos, and the original splits on that byte here: nought logs "Person lost
		// this sideshow..." and docks happiness, anything else runs the effects.
		if ( peep.QueuePos == 0 )
		{
			// The middle of the three mood changes - fifteen in this park. NOT a penalty of its own: the
			// same number multiplies what winning is worth a few lines below.
			if ( _admission is { } lost )
				peep.Happiness = Peep.Change( peep.Happiness, -lost.MediumHappinessChange );

			// A sideshow's loser also thinks thought 6 and gains an event-history entry (0x004fdc25).
			if ( item.UiType == SideshowUiType )
				Unimplemented.Report( "SETTLE_UP_SIDESHOW_THOUGHT" );

			return;
		}

		// Every visit behind the gate is entered in the guest's event history (FUN_0050c100, 0x004fe204).
		Unimplemented.Report( "SETTLE_UP_EVENT_HISTORY" );

		// <b>A sideshow PAYS OUT, and it pays the cost of goods rather than the price.</b> FUN_004fe1e0 books the
		// object's +0x188, built from UsageInfo.InitCostOfGoods, against the object and the park's bank, then adds
		// the same to the guest's cash: fifty, for the Jungle Spray, against the twenty they were just charged. A shop
		// books its own cost of goods and pays nobody (docs/exe/ride-operation.md, "The cost of goods and the park's
		// money"). Both read the item's cost of goods where the original reads the object's (Q97).
		if ( item.UiType == SideshowUiType )
		{
			_state.BookCostOfGoods( ride.ThingId, item.CostOfGoods );
			peep.Cash += item.CostOfGoods;
		}
		else if ( item.UiType == ShopUiType )
			_state.BookCostOfGoods( ride.ThingId, ShopCostOfGoods( item, ride ) );

		// Before the item's own effects, so the sickness reads the guest's hunger as they came off (0x004fe259).
		MatchTheExcitement( peep, ride, item );

		ApplyEffects( peep, item );

		TakeTheIngredient( peep, ride, item, random );

		// The descriptor's +0x15c: nought skips both, and anything but 1 or 2 logs a balance-file error into the bare
		// RET and does nothing (0x004fe615..0x004fe63d).
		switch ( item.AppearanceEffect )
		{
			case Balloon.AppearanceEffect:
				GiveABalloon( peep, ride, tick );
				break;

			case CostumeEffect:
				DressOrUndress( peep, ride, random );
				break;
		}

		// The object's flags & 1 (0x004fe78f), which is what IsToilet reads.
		if ( ride.IsToilet )
			UseTheToilet( peep, ride );

		// A shop stops at the effects, which is where its thirst, its litter and its five points of happiness
		// come from; a sideshow's winner is counted (0x004fe81f) and then cheers.
		if ( item.UiType == SideshowUiType )
		{
			peep.NumSideshowsWon++;
			peep.Happiness = Peep.Change( peep.Happiness, WinningIsWorth( item, ride ) );
		}

		// Three times the change since the join, each side truncated to its low byte (0x004fda1b..0x004fda47),
		// logged and averaged into the object's satisfaction for the day (FUN_004e1e00). Happiness is not moved.
		var change = 3 * (((int)peep.Happiness & 0xff) - ((int)peep.JoinHappiness & 0xff));

		Log.Info( $"Person {peep.ThingId}: Happiness changed by {change} since using object {ride.ThingId}" );

		rings.Satisfy( change );

		// A shop posts change + 50 to the park analyser by its special ingredient and its appearance effect, a
		// sideshow always (0x004fda92..0x004fdb2c); nothing reads those samples.
		if ( item.UiType == SideshowUiType
			|| (item.UiType == ShopUiType && (item.SpecialIngredient is >= 1 and <= 4 || item.AppearanceEffect is 1 or 2)) )
			Unimplemented.Report( "SETTLE_UP_ANALYSER_SAMPLE" );

		// Served, for every kind of object (FUN_004e19f0, 0x004fdb33).
		rings.Served.Today++;

		// A sideshow's winner thinks thought 5 and gains an event-history entry (0x004fdb38..0x004fdb7f).
		if ( item.UiType == SideshowUiType )
			Unimplemented.Report( "SETTLE_UP_SIDESHOW_THOUGHT" );
	}

	/// <summary>
	/// What a shop books for one sale - <c>FUN_004e1b40</c>: its cost of goods times one plus a quality term and
	/// plus or minus an ingredient term, truncated toward nought by <c>__ftol</c>
	/// (<c>docs/exe/ride-operation.md</c>, "The cost of goods and the park's money").
	/// </summary>
	/// <remarks>
	/// Each term is the low byte of the object's <c>mQualityOfGoods</c> or <c>mAmountOfSpecialIngredient</c>, less 50,
	/// times 0.005, held to ±0.5; the ingredient's is taken off for fat and ice (<c>SpecialIngredient</c> 1 and 3)
	/// and added for anything else. At 50 and 50 it is the cost itself. The two terms are stored as floats; the sum
	/// and the product are in double, the runtime's starting precision, where which precision is live is not
	/// settled (<c>docs/exe/park-engine.md</c>, "Which rounding is live"): only off the steps of 50 the saves hold
	/// does it matter, and a Drinks Shop at a quality of nought and an amount of 10 books 18 here and 19 at 24 bits.
	/// The cost is the item's where the original reads the object's <c>+0x188</c>, as unsigned (Q97).
	/// </remarks>
	internal static int ShopCostOfGoods( ParkItemCatalogue.Item item, ParkWorld.CatalogueObject shop )
	{
		var quality = Math.Clamp( ((shop.QualityOfGoods & 0xff) - 50f) * 0.005f, -0.5f, 0.5f );
		var ingredient = Math.Clamp( ((shop.AmountOfSpecialIngredient & 0xff) - 50f) * 0.005f, -0.5f, 0.5f );

		if ( item.SpecialIngredient is 1 or 3 )
			ingredient = -ingredient;

		var factor = ((double)quality + ingredient) - -1.0;

		// __ftol: truncated through a 64-bit integer, its low dword kept.
		return unchecked((int)(long)(factor * (uint)item.CostOfGoods));
	}

	/// <summary>
	/// The guest's count of the visit by the descriptor's <c>+0x4ac</c> (<c>0x004fd9b1</c>): a ride's rides ridden,
	/// a shop's purchases made, a sideshow's sideshows played. A feature, a toilet among them, counts none.
	/// </summary>
	private static void CountTheVisit( Peep peep, int uiType )
	{
		switch ( uiType )
		{
			case RideUiType:
				peep.NumRides++;
				break;

			case ShopUiType:
				peep.NumShops++;
				break;

			case SideshowUiType:
				peep.NumSideshows++;
				break;
		}
	}

	/// <summary>The item a thing is, or null with no catalogue or one that does not know it.</summary>
	private static ParkItemCatalogue.Item? ItemOf( ParkWorld.CatalogueObject thing, ParkItemCatalogue? catalogue )
		=> catalogue != null && catalogue.TryGet( thing.CatalogueId, out var item ) ? item : null;

	/// <summary>The item's <c>UsageInfo.AppearanceEffect</c> that gives a costume.</summary>
	private const int CostumeEffect = 2;

	/// <summary>
	/// A Costume Shop's arm of the settle-up (<c>0x004fe642</c>). A guest not in
	/// costume (<see cref="Peep.SpriteKind"/> not exactly 2) is dressed in one: the costume kind and a bank drawn over the
	/// theme's, <c>(r &gt;&gt; 2) %</c> the count, with no reseed - one in every theme, so always nought, and the draw
	/// taken all the same - and event <c>0xb</c> naming the shop. One already in costume goes back to the child they
	/// arrived as (<see cref="ParkSpriteBanks.ChildOf"/>), with no event. The picture changes as they leave the shop,
	/// which is where the original builds their sprite again, straight after this (<c>docs/exe/ride-operation.md</c>, "A
	/// costume", 4).
	/// </summary>
	/// <remarks>
	/// The costume's draw is from the ride turn's <see cref="Random"/>, where the original's is the park's one generator.
	/// </remarks>
	private void DressOrUndress( Peep peep, ParkWorld.CatalogueObject shop, Random random )
	{
		if ( peep.SpriteKind != ParkSpriteBanks.CostumeKind )
		{
			var draw = (uint)random.Next();

			peep.SpriteKind = ParkSpriteBanks.CostumeKind;
			peep.SpriteBank = _banks.CostumeBanks > 0 ? (int)((draw >> 2) % (uint)_banks.CostumeBanks) : 0;

			// The guest's event history takes event 0xb naming the shop (FUN_0050c100, the shared tail at 0x004fe787).
			Unimplemented.Report( "SETTLE_UP_COSTUME_EVENT" );

			Log.Info( $"Person {peep.ThingId}: dressed at object {shop.ThingId}, costume bank {peep.SpriteBank}" );
			return;
		}

		// "Customer returning a costume." (0x0075d798), into the bare RET.
		peep.SpriteKind = ParkSpriteBanks.ChildKind;
		peep.SpriteBank = _banks.ChildOf( peep.ThingId );

		Log.Info( $"Person {peep.ThingId}: returned a costume at object {shop.ThingId}, child bank {peep.SpriteBank}" );
	}

	/// <summary>
	/// The sprite clock at a thing sweep, in milliseconds: the sweep's game tick, eight to a sweep, at 31 each - what
	/// <see cref="ParkPeople"/> steps the sprites on.
	/// </summary>
	private static int SpriteClock( int thingTick )
		=> thingTick * ParkPeople.ThingTickEvery * ParkPeople.MillisecondsPerTick;

	/// <summary>
	/// A Balloon Shop's arm of the settle-up - <c>FUN_004fe1e0</c>, <c>0x004fe6ba</c>..<c>0x004fe78a</c>: a balloon in
	/// the guest's own colour, and a life from the shop's quality (<see cref="Balloon.LifeFor"/>), whatever it had
	/// left. The original asserts the guest holds none into a bare <c>RET</c>, and boarding has put any away.
	/// </summary>
	private void GiveABalloon( Peep peep, ParkWorld.CatalogueObject shop, int tick )
	{
		peep.Balloon = Balloon.Make( peep.ThingId, _banks.BalloonSets, SpriteClock( tick ) );
		peep.BalloonLife = Balloon.LifeFor( shop.QualityOfGoods );

		// The guest's event history takes event 0xc naming the shop (FUN_0050c100, 0x004fe775).
		Unimplemented.Report( "SETTLE_UP_BALLOON_EVENT" );

		Log.Info( $"Person {peep.ThingId}: bought a balloon at object {shop.ThingId}, colour "
			+ $"{peep.Balloon?.Sprite.Set.ToString() ?? "none"}, life {peep.BalloonLife}" );
	}

	/// <summary>
	/// What a toilet does for the guest who has used it - the toilet arm of <c>FUN_004fe1e0</c>, in its order
	/// (<c>docs/exe/ride-operation.md</c>, "The effects of a visit", step 5): the need to nought (<c>0x004fe7b6</c>),
	/// illness to nought when its truncated byte is above
	/// <see cref="ToiletClearsIllnessAbove"/>, and the hurry speed to <see cref="Peep.HurryingSpeed"/> whatever
	/// else happened.
	/// </summary>
	/// <remarks>
	/// Counted and not kept: the dirtying of the toilet by the need's byte (<c>FUN_004e2440</c>, Q100), and the two
	/// entries in the guest's event ring, <c>0x11</c> naming the toilet and <c>0x12</c> for the illness.
	/// </remarks>
	private static void UseTheToilet( Peep peep, ParkWorld.CatalogueObject toilet )
	{
		var (need, illness) = (peep.Toilet, peep.Vomit);

		// FUN_004e2440 is handed the need's truncated byte before it is emptied (0x004fe7a8).
		Unimplemented.Report( "SETTLE_UP_TOILET_DIRTYING" );

		peep.Toilet = 0f;

		Unimplemented.Report( "SETTLE_UP_TOILET_EVENT" );

		// __ftol, then the low byte compared unsigned (0x004fe7dc); the event is pushed before illness is emptied.
		if ( ((int)peep.Vomit & 0xff) > ToiletClearsIllnessAbove )
		{
			Unimplemented.Report( "SETTLE_UP_TOILET_ILLNESS_EVENT" );

			peep.Vomit = 0f;
		}

		// The hurry-speed word, 25 (0x0075c7f2).
		peep.PurposeSpeed = Peep.HurryingSpeed;

		Log.Info( $"Person {peep.ThingId}: used toilet {toilet.ThingId}, need {need:0.0} to {peep.Toilet:0.0}, "
			+ $"illness {illness:0.0} to {peep.Vomit:0.0}" );
	}

	/// <summary>
	/// The illness a toilet leaves alone - <c>CMP AL,0x5a</c> / <c>JBE</c> at <c>0x004fe7dc</c>: 91 and over is emptied.
	/// </summary>
	public const int ToiletClearsIllnessAbove = 90;

	/// <summary>
	/// Which <c>Info.WhichUIType</c> a sideshow is - the file's own comment reads "0=rides, 1=shops,
	/// 2=sideshows, 3=features".
	/// </summary>
	/// <remarks>
	/// The original splits on the descriptor's <c>+0x4ac</c>, a copy of <c>Info.WhichUIType</c> made at
	/// <c>0x004134f5</c>, so its 2 is this.
	/// </remarks>
	public const int SideshowUiType = 2;

	/// <summary>A shop's <c>Info.WhichUIType</c>, the descriptor's <c>+0x4ac</c> 1.</summary>
	public const int ShopUiType = 1;

	/// <summary>A ride's <c>Info.WhichUIType</c>, the descriptor's <c>+0x4ac</c> 0.</summary>
	public const int RideUiType = 0;

	/// <summary>
	/// What winning at a sideshow does to a guest's mood -
	/// <c>log2( costOfGoods / pricePerUse ) * MediumHappinessChange</c>, the tail of <c>FUN_004fe1e0</c>.
	///
	/// <para>
	/// <b>It is a RISE for the shipped sideshow.</b> The Jungle
	/// Spray's own file sets a cost of goods of <b>50</b> against a price of 20 - so the ratio is two and a
	/// half, its log is about 1.32, and fifteen of those is <b>+19</b>. A guest pays twenty, wins fifty and
	/// cheers up, which is what makes the engine's own "Sideshow won - happiness up %d points" an honest
	/// line rather than a perverse one.
	/// </para>
	/// <para>
	/// <b>The sign of this whole arm turns on the prize:</b> a prize SMALLER than the price would make the
	/// log negative and the winner unhappy.
	/// </para>
	/// <para>
	/// <b>The divisor is applied as an integer while the numerator is a float</b>, which is the original's
	/// <c>FILD</c> / <c>FIDIV</c> pair rather than a tidy-up, and the whole product is truncated toward
	/// nought by its <c>__ftol</c>.
	/// </para>
	/// <para>
	/// <b>A price or a prize of nought is refused rather than computed</b>, and that is a declared
	/// deviation: the original guards neither, so it would divide by nought or take the log of nought. No
    /// shipped sideshow does either, so reproducing the fault would be inventing behaviour nothing can
	/// show - and returning nought keeps the arm honest instead of producing an infinity.
	/// </para>
	/// </summary>
	private int WinningIsWorth( ParkItemCatalogue.Item item, ParkWorld.CatalogueObject ride )
	{
		if ( _admission is not { } mood || item.CostOfGoods <= 0 || ride.PricePerUse <= 0 )
			return 0;

		return (int)(Math.Log2( item.CostOfGoods / (float)ride.PricePerUse ) * mood.MediumHappinessChange);
	}

	/// <summary>
	/// How the thing's excitement suited the guest who has just had it - <c>FUN_004fdcc0</c>, which the
	/// settle-up runs behind its <c>+0x1f1</c> gate and before the item's effects (<c>docs/exe/ride-operation.md</c>,
	/// "The excitement match").
	///
	/// <para>
	/// <b>A thing with no excitement does nothing at all</b>, neither half. Otherwise the gap between the
	/// thing's excitement and the guest's kind's <c>PreferredExcitement</c>, both as bytes, adds
	/// <see cref="ParkAdmission.PerfectRide"/> to happiness under 5, <see cref="ParkAdmission.GoodRide"/> under
	/// 15 and <see cref="ParkAdmission.OKRide"/> under 40, and nothing from 40 on.
	/// </para>
	/// <para>
	/// <b>Then, whatever the gap, the ride makes them sick</b> by the excitement over
	/// <see cref="ParkAdmission.RideVomitDivisor"/> times <c>(100 - hunger) / 20</c> - every division a whole
	/// number's, and the hunger truncated first - so the less hungry the guest, the sicker the ride makes
	/// them: the Inca Totem's 70 is 7, five times that for a guest whose hunger is under one, four times up to
	/// twenty, and nothing for a hungry one from 81. Both meters are held to 0..100.
	/// </para>
	/// <para>
	/// The excitement is worked out once, where the original asks for it three times; the answers agree
	/// while nothing it reads moves in between, which nothing does. And no shipped thing has both an
	/// excitement and a hunger effect - a shop declares no excitement - so that this reads the hunger before
	/// the item's effect is the original's order, but not one the park can show. It reads
	/// <see cref="ParkRideScore.ExcitementOf"/>, so the departures listed there reach it too.
	/// </para>
	/// </summary>
	private void MatchTheExcitement( Peep peep, ParkWorld.CatalogueObject ride, ParkItemCatalogue.Item item )
	{
		if ( _admission is not { } mood || _score is not { } score )
			return;

		var excitement = ParkRideScore.ExcitementOf( ride, item, _state.TrackRides ) & 0xff;

		if ( excitement == 0 )
			return;

		var gap = Math.Abs( (score.PreferredExcitementFor( peep.PersonType ) & 0xff) - excitement );
		// From forty on the original skips the add; adding nought to a meter already in 0..100 is the same.
		var cheer = gap < 5 ? mood.PerfectRide : gap < 15 ? mood.GoodRide : gap < 40 ? mood.OKRide : 0;
		var happyWas = peep.Happiness;

		peep.Happiness = Peep.Change( peep.Happiness, cheer );

		var sickness = (100 - ((int)peep.Hunger & 0xff)) / 20 * (excitement / mood.RideVomitDivisor);
		var vomitWas = peep.Vomit;

		peep.Vomit = Peep.Change( peep.Vomit, sickness );

		Log.Info( $"Person {peep.ThingId}: object {ride.ThingId}'s excitement {excitement} against the kind's "
			+ $"{score.PreferredExcitementFor( peep.PersonType ) & 0xff}, gap {gap}: happiness {cheer:+0;-0;+0} "
			+ $"({happyWas:0.#} to {peep.Happiness:0.#}), vomit +{sickness} ({vomitWas:0.#} to {peep.Vomit:0.#}) "
			+ $"at hunger {(int)peep.Hunger}" );
	}

	/// <summary>
	/// The object's own terms of a visit, after the five effects - the rest of <c>FUN_004fe1e0</c>'s step 3 and its
	/// step 3b (<c>docs/exe/ride-operation.md</c>, "The effects of a visit"), each reading the low byte of the
	/// object's <c>mAmountOfSpecialIngredient</c> (50 on a bought thing).
	///
	/// <para>
	/// <b>Two docks, hunger's then thirst's</b>: each effect that is not nought takes one draw <c>r</c>, whether or not
	/// its dock can fire, and docks <c>PeepInfo.SmallHappinessChange</c>'s low byte when <c>(r &amp; 7)</c> + the amount
	/// + the effect is under 30, unsigned. Then happiness gains the amount times the happiness effect over a hundred,
	/// truncated toward nought; then the special ingredient: fat adds the amount to the toilet need, salt adds it to thirst, ice
	/// adds the amount times the thirst effect over a hundred to thirst, each held to 0..100, and sugar adds the amount
	/// times six over a hundred to the guest's <see cref="Peep.AdjustorSpeed"/>, a word with no hold, which quickens
	/// their walk for a few sweeps (<see cref="Peep.Pace"/>). Any other ingredient does nothing.
	/// </para>
	/// <para>
	/// <b>The draws are not the original's sequence.</b> It draws from the park's one generator, which every system
	/// shares; OpenTPW keeps one per system, and this is the ride turn's. A dock needs the park's
	/// mood constants, as the lost arm does: with none, the draws are made and nothing is docked.
	/// </para>
	/// </summary>
	internal void TakeTheIngredient( Peep peep, ParkWorld.CatalogueObject thing, ParkItemCatalogue.Item item, Random random )
	{
		var amount = thing.AmountOfSpecialIngredient & 0xff;

		var (happiness, thirst, toilet, adjustor) = (peep.Happiness, peep.Thirst, peep.Toilet, peep.AdjustorSpeed);
		var docked = 0;

		foreach ( var effect in new[] { item.HungerEffect, item.ThirstEffect } )
		{
			if ( effect == 0 )
				continue;

			var r = random.Next();

			if ( unchecked((uint)((r & 7) + amount + effect)) >= DockUnder || _admission is not { } mood )
				continue;

			peep.Happiness = Peep.Change( peep.Happiness, -(mood.SmallHappinessChange & 0xff) );
			docked++;
		}

		peep.Happiness = Peep.Change( peep.Happiness, amount * item.HappinessEffect / 100 );

		switch ( item.SpecialIngredient )
		{
			case Fat:
				peep.Toilet = Peep.Change( peep.Toilet, amount );
				break;

			case Salt:
				peep.Thirst = Peep.Change( peep.Thirst, amount );
				break;

			case Ice:
				peep.Thirst = Peep.Change( peep.Thirst, amount * item.ThirstEffect / 100 );
				break;

			case Sugar:
				peep.AdjustorSpeed = (peep.AdjustorSpeed + (amount * 6 / 100)) & 0xffff;
				break;
		}

		Log.Info( $"Person {peep.ThingId}: object {thing.ThingId}'s ingredient {item.SpecialIngredient} at {amount}: "
			+ $"happiness {happiness:0.##} to {peep.Happiness:0.##} ({docked} docked), thirst {thirst:0.##} to {peep.Thirst:0.##}, "
			+ $"toilet {toilet:0.##} to {peep.Toilet:0.##}, adjustor {adjustor} to {peep.AdjustorSpeed}" );
	}

	/// <summary>A dock on the amount fires under this sum (<c>CMP EAX,0x1e</c> at <c>0x004fe44e</c>).</summary>
	private const uint DockUnder = 30;

	/// <summary>
	/// The item's <c>UsageInfo.SpecialIngredient</c>, as <c>shops/Shops.sam</c>'s own comment names them: the four the
	/// settle-up's switch acts on (table <c>0x004fe8e8</c>).
	/// </summary>
	private const int Fat = 1, Salt = 2, Ice = 3, Sugar = 4;

	/// <summary>
	/// What an item does to the guest who used it - the five effects <c>FUN_004fe1e0</c> applies from the
	/// descriptor's <c>+0x144</c> to <c>+0x154</c>.
	///
	/// <para>
	/// <b>Deduct two, add three, and the asymmetry is the data's own rather than a choice.</b> The
	/// balance file says so in its comment column - "How much thirst to deduct" against "How much vomit
	/// to add" - and the decompile agrees, doing <c>-(float)desc + meter</c> for thirst and hunger and
	/// <c>+(float)desc + meter</c> for vomit, happiness and litter.
	/// </para>
	/// <para>
	/// <b>Every one is a shops block.</b> The eight items declaring the five keys are the eight shops by
	/// name; <c>Shops.sam</c> declares all five at 5 as a category default which each then overrides,
	/// while <c>Rides.sam</c> and <c>SideShow.sam</c> declare none. So a ride reading nought here is a
	/// real inherited fallback rather than a coincidence - which is what makes this safe to apply to
	/// anything a guest leaves.
	/// </para>
	/// </summary>
	private static void ApplyEffects( Peep peep, ParkItemCatalogue.Item item )
	{
		peep.Thirst = Peep.Change( peep.Thirst, -item.ThirstEffect );
		peep.Hunger = Peep.Change( peep.Hunger, -item.HungerEffect );

		peep.Vomit = Peep.Change( peep.Vomit, item.VomitEffect );
		peep.Happiness = Peep.Change( peep.Happiness, item.HappinessEffect );
		peep.Litter = Peep.Change( peep.Litter, item.LitterEffect );
	}

	/// <summary>
	/// Takes what a guest owes for what they have just been on - <c>FUN_004fe1a0</c>, the charge, reached
	/// from the settle-up <c>FUN_004fd970</c> that <c>ExitRide</c> (<c>FUN_005014e0</c>) runs on the way out.
	///
	/// <para>
	/// <b>A guest pays on LEAVING, not on boarding</b>: read the price from the object (<c>mPricePerUse</c>,
	/// <c>+0x194</c>), bank it and credit the object (<c>FUN_004e16b0</c>, <see cref="ParkState.TakeAt"/>), play a
	/// sound at the guest, and subtract the price from the guest's cash at <c>+0x1a0</c>. A price of nought skips all
	/// of it - which is this park's one ride, priced free; the Jungle Spray charges 20 and the Drinks Shop 30. Counted,
	/// not built: the sound, and a shop's or sideshow's month totals in the park analyser and its challenge posts.
	/// </para>
	/// <para>
	/// <b>There is no affordability test and no clamp, and both are the original's.</b> It subtracts
	/// whatever the price is, so a guest can be left short; what stops that in practice is
	/// <c>FUN_004fde50</c>, which the guest asks at the door before boarding (<c>0x00500715</c>,
	/// <see cref="PeepPriceOpinion"/>) - a gate on boarding, never on paying. Adding a check here would be
	/// inventing a refusal the engine does not make.
	/// </para>
	/// </summary>
	private void Charge( Peep peep, ParkWorld.CatalogueObject ride, int? uiType )
	{
		var price = ride.PricePerUse;

		if ( price == 0 )
			return;

		_state.TakeAt( ride.ThingId, price );

		// FUN_004e16b0's shop and sideshow arms add the price to the park analyser's month totals (+0x20130, 0x004e1711;
		// +0x20380, 0x004e18a6), which the month's change pushes into its 144-month rings, and post it to the
		// challenge manager, which takes it only while a challenge of that type is on. A ride's does neither.
		if ( uiType is ShopUiType or SideshowUiType )
		{
			Unimplemented.Report( "CHARGE_ANALYSER_MONTH_TOTAL" );
			Unimplemented.Report( "CHARGE_CHALLENGE_POST" );
		}

		// Sample 0xd0 at the guest (FUN_004faa00, 0x004fe1be..0x004fe1cb).
		Unimplemented.Report( "CHARGE_SOUND" );

		peep.Cash -= price;

		Log.Info( $"Person {peep.ThingId}: paid {price} at object {ride.ThingId}, cash {peep.Cash}" );
	}

	/// <summary>How many the ride may hold - <c>VAR_CAPACITY</c>, which its own script keeps.</summary>
	public const string CapacityVariable = "VAR_CAPACITY";

	/// <summary>How many are aboard right now - <c>VAR_ONRIDE</c>.</summary>
	public const string OnRideVariable = "VAR_ONRIDE";

	/// <summary>
	/// How long a go lasts - <c>VAR_DURATION</c>, which the engine writes beside the capacity when a ride
	/// is opened (<c>FUN_004df8f0</c>, logging "DUR = %d") and a script only ever reads.
	/// </summary>
	public const string DurationVariable = "VAR_DURATION";

	/// <summary>Whether the ride is mid-run - <c>VAR_RUNNING</c>, and it will not invite while it is.</summary>
	public const string RunningVariable = "VAR_RUNNING";

	/// <summary>
	/// Whether the ride has broken - <c>VAR_BROKEN</c>, which a ride's own turn reads to decide whether to
	/// let anybody off at all.
	/// </summary>
	/// <remarks>
	/// <c>FUN_004e14e0</c>, the state-0 turn, invites and then reads variable <b>7</b>: nought lets it
	/// dismiss, anything else sends it to BROKEN or CONDEMNED instead. Seven is <c>VAR_BROKEN</c> in the
	/// twelve names every ride script declares - reached by name, as everything here is.
	/// </remarks>
	public const string BrokenVariable = "VAR_BROKEN";

	/// <summary>
	/// Picks the guest at the head of the queue and invites them aboard - the original's
	/// <c>FUN_004e1220</c>, which a ride runs first thing on its own turn.
	///
	/// <para>
	/// <b>This is the head of the boarding chain, and without it the rest of it never fires.</b> It does
	/// two things together: it calls <c>OnAdmittance</c> on the guest - which is nothing but
	/// <c>mBeenAdmitted = 1</c> - and it nominates them at the ride's <c>+0x6c</c>. The guest's own
	/// <see cref="PeepState.InQueue"/> turn then sees both and sets off to board, which is why neither
	/// half is any use alone.
	/// </para>
	/// <para>
	/// <b>Four things must be true before it will invite.</b> The admit slot must be empty (the script
	/// has taken the last rider); the ride must not be full (<c>VAR_CAPACITY</c> above
	/// <c>VAR_ONRIDE</c>); it must not be mid-run (<c>VAR_RUNNING</c> nought); and it must not already
	/// hold a nominee. The guest itself must be queueing AND at the front - <c>FUN_00501290</c> is just
	/// <c>state == InQueue &amp;&amp; mQueuePos == 0</c>.
	/// </para>
	/// <para>
	/// <b>The fullness test is skipped for a WATER or COASTER track.</b> The original compares the item
	/// descriptor's track type against <b>3</b> and then
	/// <b>2</b>, so the exempt pair is <see cref="ItemDescriptionFile.WaterTrack"/> and
	/// <see cref="ItemDescriptionFile.CoasterTrack"/>; a <see cref="ItemDescriptionFile.CarTrack"/> is
	/// <b>not</b> exempt and is stopped by being full like anything else.
	/// <see cref="ParkRideChoice.CanBeOffered"/> refuses types 1 and 2 for a quite different reason, a
	/// track ride that is not valid. Invite reads the type from the item rather than the object, which is
	/// why it is passed in.
	/// </para>
	/// <para>
	/// <b>One arm is not reproduced.</b> The object's <c>+0x68</c> is <c>mCanLoad</c>, refused on here as
	/// <see cref="ParkRideChoice.CanBeOffered"/> refuses on it. <c>+0x33</c> bit 0 is <c>RunsContinuously</c>,
	/// from the descriptor's <c>+0x48</c> (<c>docs/exe/ride-operation.md</c>, "Object fields"): the original
	/// invites <i>while running</i> when it is set, and nothing here reads it.
	/// </para>
	/// </summary>
	/// <returns>The guest invited, or nought if nobody was.</returns>
	public int Invite( RideScript? script, ParkWorld.CatalogueObject ride, int trackType = 0 )
	{
		if ( script == null )
			return 0;

		// mCanLoad, and the original bails on it before it reads a single script variable. Same reading as
		// AdmitPerson's and as ParkRideChoice's: non-zero, not a particular value. On this bail the original
		// runs FUN_004e0450 and returns (0x004e13fc), skipping the watchdog; the ride's turn does both, in
		// ParkPeople.CompleteOrTurnAway, where the tick and the guest's side are.
		if ( ride.CanLoad == 0 )
			return 0;

		// The script has not taken the last rider yet.
		if ( script[AdmitVariable] != 0 )
			return 0;

		// Full - unless it is a WATER or COASTER track, which keep loading while they run. The original
		// tests the descriptor against 3 and then 2, so a CAR track is NOT exempt; see the remarks.
		if ( script[CapacityVariable] <= script[OnRideVariable]
			&& trackType is not (ItemDescriptionFile.WaterTrack or ItemDescriptionFile.CoasterTrack) )
			return 0;

		if ( script[RunningVariable] != 0 || _state.PersonBeingLoaded( ride.ThingId ) != 0 )
			return 0;

		var head = _state.FirstInQueue( ride.ThingId );

		if ( head == 0 || !_guests.TryGetValue( head, out var peep ) )
			return 0;

		// FUN_00501290 - queueing, and at the front of it.
		if ( peep.State != PeepState.InQueue || peep.QueuePos != 0 )
			return 0;

		// OnAdmittance, then the nomination - in the original's order.
		peep.BeenAdmitted = true;
		_state.NominateForLoading( ride.ThingId, head );

		return head;
	}

	/// <summary>
	/// Forgets a guest who is leaving - <c>FUN_004e0ac0</c>, "Object %d: person %d left after I'd admitted
	/// him": the nominee at <c>+0x6c</c> let go of, whoever it is, and <see cref="AdmitVariable"/> emptied
	/// when it names the leaver. The original asserts the leaver is the nominee and clears it either way.
	/// </summary>
	public void Forget( RideScript? script, int rideId, int personId )
	{
		Log.Info( $"Object {rideId}: person {personId} left after I'd admitted him "
			+ $"(person being loaded was {_state.PersonBeingLoaded( rideId )})" );

		_state.NominateForLoading( rideId, 0 );

		if ( script != null && personId != 0 && script[AdmitVariable] == personId )
		{
			Log.Info( $"Object {rideId}: person {personId} is in LETMEON but wants to leave.  Zeroing LETMEON" );
			script.Set( AdmitVariable, 0 );
		}
	}

	/// <summary>
	/// Drops a nominee who has not set off to board - the tail of <c>FUN_004e1220</c>, whose own line is
	/// "Object %d thinks person %d is be...".
	///
	/// <para>
	/// <b>It is a watchdog, and the ride runs it on every turn it does not invite.</b> A guest who was
	/// invited but is no longer heading for the ride - they gave up, or the park shut under them - would
	/// otherwise hold the nomination for ever and stop anybody else being called forward.
	/// </para>
	/// </summary>
	/// <returns>Whether a stale nomination was dropped.</returns>
	public bool DropUnreadyNominee( ParkWorld.CatalogueObject ride )
	{
		var nominee = _state.PersonBeingLoaded( ride.ThingId );

		if ( nominee == 0 )
			return false;

		if ( _guests.TryGetValue( nominee, out var peep ) && peep.State == PeepState.BeingAdmitted )
			return false;

		_state.NominateForLoading( ride.ThingId, 0 );

		return true;
	}

	/// <summary>
	/// The script's closed flag - <c>VAR_RIDECLOSED</c>, which every close writes 1 and every open 0. The
	/// engine never reads it back; a ride's own script does, and the Belly Bounce's stops admitting and lets
	/// its riders off while it is set.
	/// </summary>
	public const string ClosedVariable = "VAR_RIDECLOSED";

	/// <summary>
	/// Closes a ride - <c>FUN_004df300</c>, "Object %d: Closing...": <c>mCanLoad</c> nought, the nominee at
	/// <c>+0x6c</c> let go of, and <see cref="ClosedVariable"/> set. It has no guard, leaves <c>mState</c> as it
	/// was and runs no completion; the ride's own turn turns the queue away afterwards, one head at a time
	/// (<c>docs/exe/ride-operation.md</c>, "The closed ride").
	/// </summary>
	/// <remarks>
	/// Its last call, <c>FUN_00454550( model, 1 )</c>, changes the ride's model, and nothing here draws it.
	/// </remarks>
	public void Close( RideScript? script, int rideId )
	{
		if ( !_state.TryObject( rideId, out var ride ) )
			return;

		Log.Info( $"Object {rideId}: Closing... (person being loaded was {_state.PersonBeingLoaded( rideId )})" );

		_state.ReplaceObject( ride with { CanLoad = 0 } );
		_state.NominateForLoading( rideId, 0 );
		script?.Set( ClosedVariable, 1 );

		Unimplemented.Report( "CLOSED_RIDE_MODEL_CHANGE" );
	}

	/// <summary>
	/// Opens a ride - <c>FUN_004df390</c>: <c>mCanLoad</c> 1, <see cref="ClosedVariable"/> nought, and
	/// SetState(0), which for state 0 is the store alone. It leaves the nominee alone.
	/// </summary>
	/// <remarks>
	/// <b>The original asks <see cref="MayOpen"/> again first and opens whatever it answers</b>, logging
	/// "Opening non-openable ride!" five times when it refuses (<c>0x004df3ea</c>). Every caller here has just
	/// asked it, so the second asking is left out. <c>FUN_004547c0( model )</c>, the model's side of opening,
	/// is not drawn.
	/// </remarks>
	public void Open( RideScript? script, int rideId )
	{
		if ( !_state.TryObject( rideId, out var ride ) )
			return;

		Log.Info( $"Object {rideId}: opened" );

		_state.ReplaceObject( ride with { CanLoad = 1 } );
		Unimplemented.Report( "OPENED_RIDE_MODEL_CHANGE" );
		script?.Set( ClosedVariable, 0 );

		if ( _state.TryObject( rideId, out var opened ) )
			_state.ReplaceObject( opened with { State = 0 } );
	}

	/// <summary>
	/// Whether a closed ride may be opened - <c>FUN_004df290</c>, which the park's door asks of every object
	/// before opening it: not broken down (1), condemned (4) or waiting for an upgrade (2), no mechanic called
	/// (<c>mRequestedService</c>), and the back of its queue connected (<see cref="BackOfQueueConnected"/>).
	/// </summary>
	/// <remarks>
	/// <b>A coaster is let through here, and counted.</b> For track type 3 the original also asks
	/// <c>FUN_00441970</c> whether its circuit is closed, which <see cref="ParkRideChoice.CircuitClosed"/> answers
	/// for the choice; the door does not ask it. The shipped park holds no coaster.
	/// </remarks>
	public static bool MayOpen( IParkInitialState? park, ParkWorld.CatalogueObject ride, int trackType )
	{
		// 2 is an upgrade waiting to be done; the original asks 1, 4, the service, then 2.
		if ( ride.State is ParkRideChoice.StateRefusedOne or ParkRideChoice.StateRefusedFour
			|| ride.RequestedService != 0 || ride.State == 2 )
			return false;

		if ( !BackOfQueueConnected( park, ride ) )
			return false;

		if ( trackType == ItemDescriptionFile.CoasterTrack )
			Unimplemented.Report( "OPEN_GUARD_COASTER_TRACK_RECORD" );

		return true;
	}

	/// <summary>
	/// Whether the back of a ride's queue joins anything - <c>FUN_004de4a0</c>, "Back of queue is
	/// %sconnected".
	/// </summary>
	/// <remarks>
	/// <b>A thing with a queue path</b> (<see cref="ParkWorld.CatalogueObject.HasQueuePath"/>) asks its back
	/// cell (<see cref="ParkRideChoice.QueueCellsFor"/>, <c>FUN_004de130</c>): connected when that cell's
	/// <c>mNeighbours</c> differs from its <c>mDirection</c>, so it links somewhere other than the one cell
	/// ahead of it. No back cell is not connected.
	/// <para>
	/// <b>Any other thing asks its entrance.</b> The angle names the side it faces - 0 as <c>0x10</c>, 90 as
	/// <c>0x04</c>, 180 as <c>0x01</c>, 270 as <c>0x40</c> - and the cell one step the other way from the entry
	/// cell must be path linked back to it, with the entry cell linked to it. The original reads an unset byte
	/// for any other angle (<c>0x004de510</c>..<c>0x004de53f</c>); here that is not connected.
	/// </para>
	/// </remarks>
	public static bool BackOfQueueConnected( IParkInitialState? park, ParkWorld.CatalogueObject ride )
	{
		if ( park == null )
			return false;

		if ( ride.HasQueuePath )
		{
			var (back, _) = ParkRideChoice.QueueCellsFor( park, ride );

			if ( back == 0 )
				return false;

			var (backX, backY) = MapStep.CellAt( back );
			var cell = ParkState.CellFor( park, backX, backY );

			return cell.Neighbours != cell.Direction;
		}

		var facing = ride.Angle switch { 0 => 0x10, 90 => 0x04, 180 => 0x01, 270 => 0x40, _ => 0 };

		if ( facing == 0 )
			return false;

		var away = CellEdge.Opposite( facing );
		var (entryX, entryY) = MapStep.CellAt( ride.EntryPos );
		var (beyondX, beyondY) = ParkBuilding.Step( entryX, entryY, away );

		if ( !ParkState.OnMap( beyondX, beyondY ) )
			return false;

		var entrance = ParkState.CellFor( park, entryX, entryY );
		var beyond = ParkState.CellFor( park, beyondX, beyondY );

		return beyond.Type == CellEdge.Path && (beyond.Neighbours & facing) != 0 && (entrance.Neighbours & away) != 0;
	}

	/// <summary>
	/// The tail of <c>FUN_004de1f0</c>, after a queue measured again has told its people: a closed ride
	/// whose queue now joins something is opened again, whatever the park's own door says, and the ride
	/// forgets which member of staff was servicing it.
	/// </summary>
	/// <remarks>
	/// The reopen asks <c>mCanLoad</c> nought, <see cref="MayOpen"/>, and for a track ride (types 1 to 3)
	/// <c>mIsTrackRideValid</c> as well (<c>0x004de2f7</c>..<c>0x004de3df</c>), then opens as
	/// <see cref="Open"/> does. <c>mAssignedStaffMember</c> is zeroed whatever was decided (<c>0x004de48c</c>).
	/// </remarks>
	public void ReopenAfterRemeasure( RideScript? script, int rideId, IParkInitialState? park, int trackType )
	{
		if ( !_state.TryObject( rideId, out var ride ) )
			return;

		Log.Info( $"Object {rideId}: back of queue is {(BackOfQueueConnected( park, ride ) ? "" : "not ")}connected" );

		if ( ride.CanLoad == 0 && MayOpen( park, ride, trackType )
			&& (trackType is not (ItemDescriptionFile.CarTrack or ItemDescriptionFile.WaterTrack
				or ItemDescriptionFile.CoasterTrack) || ride.IsTrackRideValid != 0) )
			Open( script, rideId );

		if ( _state.TryObject( rideId, out var now ) )
			_state.ReplaceObject( now with { AssignedStaff = 0 } );
	}
}
