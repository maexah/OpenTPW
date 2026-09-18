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
	/// <b>One refusal of the original's is NOT reproduced:</b> it also gives up when the object's
	/// <c>+0x68</c> is nought, and what that field is has not been established. The two ride states it
	/// refuses on ARE reproduced - they are the same pair <see cref="ParkRideChoice"/> already names.
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
	/// <b>What is NOT reproduced is where they go.</b> The original puts them at the ride's exit
	/// (<c>FUN_004dedf0(1,..)</c>) and gives them a destination; nothing here reads an exit cell, so this
	/// changes their state and clears the slot and leaves the walking to whoever builds that.
	/// </para>
	/// </summary>
	/// <returns>Whether a guest was let off.</returns>
	public bool Dismiss( RideScript? script, int rideId, int tick, Random random )
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

		peep.SetState( PeepState.OnRide, tick, random );

		return script.Set( DismissVariable, 0 );
	}
}
