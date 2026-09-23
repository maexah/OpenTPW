namespace OpenTPW;

/// <summary>
/// What a ride does to its own queue on its turn - the part of the original's per-object tick that keeps
/// a queue honest, from <c>FUN_004e0b90</c>.
///
/// <para>
/// <b>A ride is ticked like anything else in the park.</b> <c>FUN_0050b360</c> switches on a thing's
/// model byte and hands model 3 to <c>FUN_004e0b90</c> and then <c>FUN_004e0e00</c>, exactly as it hands
/// a guest to their needs and then their behaviours. <b>The whole of that tick is built now</b>, script
/// and all - this said only the first half's tail could be built honestly, which was true until the
/// script binding landed.
/// </para>
/// <para>
/// <b>Ride operation is script-driven, and that is why only this much is here.</b> The object holds a
/// script handle at <c>+0x24</c>, and the engine talks to the ride through its script's variables -
/// <b>0</b> admit, <b>1</b> dismiss, <b>4</b> breakdown, <b>6</b> closed, <b>7</b> out of service,
/// <b>8</b> dirty. <b>Both of the cautions this paragraph used to carry are now settled.</b>
/// <see cref="ParkRides"/> binds a script to every placed object and pushes the save's own capacity and
/// duration into it; and the condition once called "worse than unbound" - <c>FUN_004e0450</c> admitting
/// when variable 0 <i>differs</i> from the head of the queue - is the <c>VAR_LETMEON</c> handshake:
/// the engine fills that slot and the script zeroes it to acknowledge, so an empty slot is the script
/// saying it is ready. See <see cref="CompleteAdmission"/>, which reads it the right way round.
/// </para>
/// </summary>
public sealed class ParkRideOperation
{
	private readonly ParkState _state;
	private readonly IReadOnlyDictionary<int, Peep> _guests;
	private readonly ParkAdmission? _admission;

	/// <param name="state">The park as it is being played, which owns the queues.</param>
	/// <param name="guests">Every guest by thing id, for asking what the head of a queue is doing.</param>
	/// <param name="admission">
	/// The park's own mood constants, for the settle-up alone - <c>PeepInfo.MediumHappinessChange</c>, which
	/// is <b>both</b> the penalty for getting nothing out of a visit and the multiplier on what winning is
	/// worth. <b>One balance value with two uses, not two settings.</b> Null leaves both arms alone rather
	/// than moving happiness by a number nobody read, which is what a test asking about the money means.
	/// </param>
	public ParkRideOperation( ParkState state, IReadOnlyDictionary<int, Peep> guests,
		ParkAdmission? admission = null )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( guests );

		_state = state;
		_guests = guests;
		_admission = admission;
	}

	/// <summary>
	/// Whether this visit gave the guest what they came for - the original's <c>FUN_004e2670</c>, rolled as
	/// a guest enters the thing and written into their <c>mQueuePos</c> by <c>FUN_00501db0</c>'s case
	/// <c>0xe</c>.
	///
	/// <para>
	/// <b>It is <c>rand() % 100 &lt;= chance</c>, and the chance lives on the OBJECT at <c>+0x190</c></b>,
	/// built as <c>100 - UsageInfo.InitChanceOfLoosing</c> when the thing is made. This project's notes
	/// recorded that byte as possibly the person's and left it unsettled;
	/// <c>FUN_004e2670</c> settles it, because the same pointer it reads is the one it takes the catalogue
	/// id and the script handle from, and both of those are object fields.
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
	/// <c>TOUR 4</c> (TourRide). This paragraph named only the first pair until the scripts were listed,
	/// which made a claim true of one ride read as a claim about all of them.
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
	/// <b>A guest is PUT DOWN at the exit and then aimed one cell PAST it, and this used to walk them to
	/// the exit instead - which the map refuses.</b> <c>FUN_005014e0</c> reads the exit point, calls
	/// <c>FUN_004fa930</c> to set the person's position outright, and only then sets a destination: the
	/// neighbour of the exit cell in the direction that cell faces, flipped to the opposite when
	/// <c>mExitPos</c> equals <c>mEntryPos</c> (which is true of ten of this park's eleven objects). Walking
	/// a guest TO the exit cannot work and never did: <see cref="CellEdge"/> only opens a ride end along
	/// the way it faces, so the route fails and the guest gives up where they stand. Alexah asked for a
	/// test that the guest's position becomes the exit, and that test is what found it.
	/// </para>
	/// <para>
	/// <b>The failure arm is deliberately NOT reproduced, and it is drastic rather than quiet.</b> When the
	/// destination will not route the original refuses the dismissal and calls <c>FUN_004df150</c>, which
	/// <i>closes the ride</i>: it clears <c>mCanLoad</c> and <c>mPersonBeingLoaded</c>, logs "Object %d:
	/// Closing..." and sets the script's <c>VAR_CLOSED</c>. Nothing here writes either field, so building
	/// half of that would leave a ride that shut itself over a routing failure and never reopened - a
	/// worse fault than the one this fixes. A guest whose neighbour will not route is dismissed anyway and
	/// drops to <see cref="PeepState.Deciding"/> standing on the exit, which is where they are.
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
		Func<int, PeepWalk?>? walkFor = null, ParkWorld? park = null, ParkItemCatalogue? catalogue = null )
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

		SettleUp( peep, ride, catalogue );

		peep.SetState( PeepState.LeavingRide, tick, random );

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
	/// <b>What cannot be shown is the ? itself.</b> It is the stranded thought bubble, the same one
	/// <c>FUN_004f9490</c> raises with "Peep %d: stranded at time %d", and this project has no thought
	/// system at all - see <see cref="PeepBehaviour.SetRandomDest"/>, which records the same absence from
	/// the other side. So a guest who cannot leave the exit stands there silently instead of asking.
	/// </para>
	/// </summary>
	private static void PutDownAtTheExit( Peep peep, PeepWalk walk, ParkWorld.CatalogueObject ride,
		ParkWorld? park )
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

		if ( CellEdge.DirectionFor( facing ) is not { } towards )
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
	/// <b>The effects are gated, and the gate is the roll's byte.</b> The original
	/// splits on the guest's <c>+0x1f1</c>: nought logs "Person lost this sideshow..." and docks
	/// happiness, and anything else runs the effects. That byte is <c>mQueuePos</c> by the save reader's
	/// own naming, but <c>FUN_00501db0</c>'s case <c>0xe</c> <i>overwrites</i> it for a sideshow with
	/// <c>FUN_004e2670</c>'s roll - so the two meanings share one field. <b>Do not gloss it as "did they
	/// win"</b>: the sideshow's win is computed inside the effects, after this has already been tested.
	/// </para>
	/// <para>
	/// The byte is written on admission by <see cref="PeepBehaviour"/>'s roll through <see cref="Succeeds"/>,
	/// and the "lost" arm docks <c>PeepInfo.MediumHappinessChange</c> (<c>DAT_0078505c</c>), both built.
	/// </para>
	/// <para>
	/// <b>What is NOT built, and why.</b> Three more happiness changes in <c>FUN_004fe1e0</c> each read the
	/// object's byte <c>+0x198</c>, which is not decoded. For the hunger effect (<c>+0x148</c>) and then the
	/// thirst effect (<c>+0x144</c>), whichever is non-zero, the original docks
	/// <c>PeepInfo.SmallHappinessChange</c> (<c>DAT_00785058</c>) when <c>(rand &amp; 7)</c> plus that byte
	/// plus the effect is under 30 (<c>0x004fe453</c>, <c>0x004fe4a5</c>); then it adds the byte times the
	/// happiness effect over a hundred (<c>0x004fe4cf</c>..<c>0x004fe525</c>).
	/// </para>
	/// <para>
	/// <b>Also absent, and each with a consumer that does not exist yet:</b> the guest's recent-things
	/// history (<c>mPreviousRides</c>, four entries shifted by three at <c>+0x1e0</c>, which the ride
	/// scorer divides a candidate down by) and the three visit counters at <c>+0x1c4</c>/<c>+0x1c8</c>/
	/// <c>+0x1cc</c> chosen by the descriptor's <c>+0x4ac</c>. Both would be written and never read.
	/// </para>
	/// </summary>
	private void SettleUp( Peep peep, ParkWorld.CatalogueObject ride, ParkItemCatalogue? catalogue )
	{
		Charge( peep, ride );

		// No catalogue is a test asking about the money rather than about the visit, and an item the
		// catalogue does not know cannot say what it does to anybody.
		if ( catalogue == null || !catalogue.TryGet( ride.CatalogueId, out var item ) )
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

			return;
		}

		ApplyEffects( peep, item );

		// Everything past here is a sideshow's alone. A shop stops at the effects, which is where its
		// thirst, its litter and its five points of happiness come from.
		if ( item.UiType != SideshowUiType )
			return;

		// <b>A sideshow PAYS OUT, and it pays the cost of goods rather than the price.</b> FUN_004fe1e0
		// adds FUN_004e1a10 - the object's +0x188, built from UsageInfo.InitCostOfGoods - straight onto the
		// guest's cash. Five, for the Jungle Spray, against the twenty they were just charged.
		peep.Cash += item.CostOfGoods;

		peep.Happiness = Peep.Change( peep.Happiness, WinningIsWorth( item, ride ) );
	}

	/// <summary>
	/// Which <c>Info.WhichUIType</c> a sideshow is - the file's own comment reads "0=rides, 1=shops,
	/// 2=sideshows, 3=features".
	/// </summary>
	/// <remarks>
	/// <b>Only the sideshow value is used, and that is deliberate.</b> The original splits on the
	/// descriptor's <c>+0x4ac</c> and every reading of this project's agrees that <b>2</b> is a sideshow,
	/// while what <b>1</b> means is recorded as unsettled - the field table says "ride" and the economy feed
	/// says "shops". Nothing here needs to know, so nothing here decides it.
	/// </remarks>
	public const int SideshowUiType = 2;

	/// <summary>
	/// What winning at a sideshow does to a guest's mood -
	/// <c>log2( costOfGoods / pricePerUse ) * MediumHappinessChange</c>, the tail of <c>FUN_004fe1e0</c>.
	///
	/// <para>
	/// <b>It is a RISE for the shipped sideshow, and the first reading of it here was wrong.</b> The Jungle
	/// Spray's own file sets a cost of goods of <b>50</b> against a price of 20 - so the ratio is two and a
	/// half, its log is about 1.32, and fifteen of those is <b>+19</b>. A guest pays twenty, wins fifty and
	/// cheers up, which is what makes the engine's own "Sideshow won - happiness up %d points" an honest
	/// line rather than a perverse one.
	/// </para>
	/// <para>
	/// <b>That number was predicted as 5 and measured as 50, and the test is what caught it</b> - a
	/// mis-transcription of the item's own <c>.sam</c> that had reached three comments before the
	/// arithmetic refused it. The sign of this whole arm turns on it: a prize SMALLER than the price would
	/// make the log negative and the winner unhappy, which is what the wrong number implied.
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
	/// <b>A guest pays on LEAVING, not on boarding</b>, and the whole of the charge is three steps: read
	/// the price from the object (<c>mPricePerUse</c>, <c>+0x194</c>), credit the object, and subtract it
	/// from the guest's cash at <c>+0x1a0</c>. A price of nought skips all of it - which is this park's one
	/// ride, priced free; the Jungle Spray charges 20 and the Drinks Shop 30.
	/// </para>
	/// <para>
	/// <b>There is no affordability test and no clamp, and both are the original's.</b> It subtracts
	/// whatever the price is, so a guest can be left short; what stops that in practice is
	/// <c>FUN_004fde50</c>, which decides whether a thing is worth its price BEFORE a guest is sent to it -
	/// a gate on choosing, never on paying. Adding a check here would be inventing a refusal the engine
	/// does not make.
	/// </para>
	/// </summary>
	private void Charge( Peep peep, ParkWorld.CatalogueObject ride )
	{
		var price = ride.PricePerUse;

		if ( price == 0 )
			return;

		_state.TakeAt( ride.ThingId, price );

		peep.Cash -= price;
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
