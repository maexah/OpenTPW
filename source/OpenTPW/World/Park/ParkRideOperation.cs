namespace OpenTPW;

/// <summary>
/// What a ride does to its own queue on its turn - the part of the original's per-object tick that keeps
/// a queue honest, from <c>FUN_004e0b90</c>.
///
/// <para>
/// <b>A ride is ticked like anything else in the park.</b> <c>FUN_0050b360</c> switches on a thing's
/// model byte and hands model 3 to <c>FUN_004e0b90</c> and then <c>FUN_004e0e00</c>, exactly as it hands
/// a guest to their needs and then their behaviours. This is the first half's tail, which is the piece
/// that can be built honestly today: the rest of that tick turns on the ride's own SCRIPT.
/// </para>
/// <para>
/// <b>Ride operation is script-driven, and that is why only this much is here.</b> The object holds a
/// script handle at <c>+0x24</c>, and the engine talks to the ride through its script's variables -
/// <b>0</b> admit, <b>1</b> dismiss, <b>4</b> breakdown, <b>6</b> closed, <b>7</b> out of service,
/// <b>8</b> dirty. Nothing here binds a script to a ride object yet, and one condition is worse than
/// unbound: <c>FUN_004e0450</c> admits when variable 0 <i>differs</i> from the head of the queue, and
/// what that difference means is not established. Building an admit on a misread trigger would move
/// guests onto rides for the wrong reason, which is worse than not moving them at all.
/// </para>
/// </summary>
public sealed class ParkRideOperation
{
	private readonly ParkState _state;
	private readonly IReadOnlyDictionary<int, Peep> _guests;

	/// <param name="state">The park as it is being played, which owns the queues.</param>
	/// <param name="guests">Every guest by thing id, for asking what the head of a queue is doing.</param>
	public ParkRideOperation( ParkState state, IReadOnlyDictionary<int, Peep> guests )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( guests );

		_state = state;
		_guests = guests;
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
	/// Runs <see cref="DropStaleQueueHead"/> for every object that has a queue, which is how the whole
	/// park stays honest without a caller having to know which things are rides.
	/// </summary>
	/// <returns>How many stale heads were dropped.</returns>
	public int DropStaleQueueHeads( ParkWorld? park )
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
	/// <b>Every refusal of the original's is reproduced, and this paragraph used to say one was not.</b> It
	/// said the object's <c>+0x68</c> "has not been established" - but that field is <c>mCanLoad</c>, it is
	/// serialised, the save reader has always parsed it under that name, and
	/// <see cref="ParkRideChoice.CanBeOffered"/> in this same folder has always refused on it. The claim was
	/// stale rather than the decode missing. The two ride states it refuses on are the pair
	/// <see cref="ParkRideChoice"/> already names.
	/// </para>
	/// <para>
	/// <b>It cannot fire in the shipped park</b>, where <c>mCanLoad</c> is 1 on all fourteen objects. It is
	/// here because the identical test already guards the choice, so leaving it out of the admit would mean
	/// the same field deciding a guest's destination and then being ignored at the door.
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

		// "admitting wrong person" - a ride holds one nominee, and this must be them.
		if ( _state.PersonBeingLoaded( ride.ThingId ) != personId )
			return false;

		// The slot has to be empty. Asked BEFORE the nomination is let go of, so a refusal leaves the
		// ride still holding its nominee rather than losing them.
		if ( script[AdmitVariable] != 0 )
			return false;

		_state.NominateForLoading( ride.ThingId, 0 );

		return script.Set( AdmitVariable, personId );
	}

	/// <summary>
	/// Finishes an admission the script has taken up - <c>FUN_004e0450</c> and the <c>FUN_00500870</c> it
	/// calls.
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
	/// <b>This slot runs the other way.</b> The script WRITES it: <c>UNBOUNCE</c> and
	/// <c>FORCEUNBOUNCE</c> store into their operand, so the script fills <c>VAR_LETMEOFF</c> with whoever
	/// came off (or leaves nought when nobody did), and the engine clears it once they are on their way.
	/// That is why a script skips its unbounce while the slot is still full - the ride has not been
	/// collected from yet.
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
	/// <b>One arm of the original is NOT reproduced, and it is about failure rather than success.</b> There,
	/// setting the destination can fail - <c>FUN_004fa530</c> answers whether a route exists - and when it
	/// does the guest is <i>not</i> moved on: it logs "SetDest on ride exit f[ailed]" and takes a fallback
	/// instead. Reproducing that needs a route to have been attempted, so a call made without a
	/// <paramref name="walk"/> changes the state as before and a call made with one plans the route but
	/// does not yet gate the state on it.
	/// </para>
	/// </summary>
	/// <param name="walkFor">
	/// How to find a guest's walk by thing id, or null where there is nowhere to move anyone.
	/// <b>A LOOKUP rather than one walk, and that is not a stylistic choice.</b> Which guest comes off is
	/// decided inside this method, by whoever the script has named in <c>VAR_LETMEOFF</c> - so a caller
	/// cannot know whose walk to hand over. This took a single <c>PeepWalk</c> when it was written, which
	/// only a test that already knew the answer could satisfy, and no ride's turn ever could.
	/// </param>
	/// <returns>Whether a guest was let off.</returns>
	public bool Dismiss( RideScript? script, ParkWorld.CatalogueObject ride, int tick, Random random,
		Func<int, PeepWalk?>? walkFor = null )
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
			PeepBehaviour.SendTo( peep, walk, (ride.ExitCellX, ride.ExitCellY) );

		peep.SetState( PeepState.OnRide, tick, random );

		return script.Set( DismissVariable, 0 );
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
	/// <b>The fullness test is skipped for a WATER or COASTER track - and this said "car or water", which
	/// was wrong.</b> The original compares the item descriptor's track type against <b>3</b> and then
	/// <b>2</b>, so the exempt pair is <see cref="ItemDescriptionFile.WaterTrack"/> and
	/// <see cref="ItemDescriptionFile.CoasterTrack"/>; a <see cref="ItemDescriptionFile.CarTrack"/> is
	/// <b>not</b> exempt and is stopped by being full like anything else. The wrong pair came of carrying
	/// a phrase over from <see cref="ParkRideChoice.CanBeOffered"/> - which refuses types 1 and 2 for a
	/// quite different reason, a track ride that is not valid - instead of reading this function's own
	/// operands. It reads the type from the item rather than the object, which is why it is passed in.
	/// </para>
	/// <para>
	/// <b>This said TWO arms were unreproduced because their fields were unestablished; it is now one.</b>
	/// The object's <c>+0x68</c> is <c>mCanLoad</c> - serialised, parsed under that name, and already
	/// refused on by <see cref="ParkRideChoice.CanBeOffered"/> - so it is reproduced here too. What remains
	/// genuinely unestablished is <c>+0x33</c>: the original will invite <i>while running</i> when that
	/// byte carries bit 0, a second flags byte beside the one at <c>+0x32</c> this project reads. That one
	/// is still not guessed at.
	/// </para>
	/// </summary>
	/// <returns>The guest invited, or nought if nobody was.</returns>
	public int Invite( RideScript? script, ParkWorld.CatalogueObject ride, int trackType = 0 )
	{
		if ( script == null )
			return 0;

		// mCanLoad, and the original bails on it before it reads a single script variable. Same reading as
		// AdmitPerson's and as ParkRideChoice's: non-zero, not a particular value.
		//
		// NOT reproduced, and named rather than quietly dropped: on this bail the original does not simply
		// leave - it calls FUN_004e0450, the completion, on its way out. CompleteAdmission needs a tick and
		// a Random that this method is not given, so it belongs with whatever drives a ride's turn (where
		// both exist) rather than with an invented signature here. It cannot fire in the shipped park,
		// where mCanLoad is 1 on all fourteen objects.
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
}
