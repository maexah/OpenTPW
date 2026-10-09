namespace OpenTPW;

/// <summary>
/// The park's people as a park file's writer takes them (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the
/// people").
/// </summary>
public sealed partial class ParkPeople
{
	/// <summary>The sprite kind and bank each member of staff wears: the save's sprite's, or the hire's.</summary>
	private readonly Dictionary<int, (int Kind, int Bank)> _staffLooks = [];

	/// <summary>
	/// Every guest and member of staff, each as their record and their sprite are written.
	///
	/// <para>
	/// <b>A guest on a thing is written as they are</b>: queueing, called forward, walking on or off or riding,
	/// with their thing, their place and their links in its queue. The thing's half, its queue head, the guest it
	/// is loading and its script's tables, is written with the things (<c>ParkRides.Written</c>). A rider on a
	/// thing that does not keep its riders' sprites (<see cref="ParkWorld.CatalogueObject.KeepsRidersSpriteFlag"/>)
	/// is written with none, as admission leaves them.
	/// </para>
	/// <para>
	/// <b>A deviation: a handle to a thing the file does not hold is written as nought, and counted</b>
	/// (<c>SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING</c>): what was bought here is not written yet, so a guest bound
	/// for it or on it is written deciding where they stand (the second counted too,
	/// <c>SAVE_PARK_GUEST_ON_A_THING</c>) and a member of staff resting in or cleaning it idle.
	/// </para>
	/// <para>
	/// A held balloon and a thought bubble go as sprites of their own (<see cref="BalloonOf"/>,
	/// <see cref="BubbleOf"/>). <b>A balloon let go and still bursting is not written, and counted</b>
	/// (<c>SAVE_PARK_BALLOON_LET_GO</c>): nobody's record names it.
	/// </para>
	/// <para>
	/// A member of staff in the hand is written idle where they were picked up: the original puts the hand's thing
	/// down before it writes (<c>FUN_00516c80</c>, step 1).
	/// </para>
	/// </summary>
	/// <param name="written">Whether a thing that is no person is in the file being written.</param>
	internal List<ParkWorld.WrittenPerson> Written( Func<int, bool> written )
	{
		var people = new List<ParkWorld.WrittenPerson>( _peeps.Count + _staff.Count );

		int Handle( int thing )
		{
			if ( thing == 0 || written( thing ) )
				return thing;

			Unimplemented.Report( "SAVE_PARK_HANDLE_TO_AN_UNWRITTEN_THING" );
			return 0;
		}

		foreach ( var peep in _peeps )
		{
			var id = peep.ThingId;
			var walk = _walks.GetValueOrDefault( id );
			var heading = walk?.Heading ?? 0;
			var onAThing = peep.State is PeepState.InQueue or PeepState.SteppingUpQueue or PeepState.BeingAdmitted
				or PeepState.EnteringRide or PeepState.LeavingRide or PeepState.Riding;

			var major = Handle( peep.MajorDest );
			var bound = peep.State is PeepState.GoingToRide or PeepState.GoingToMinorDestination;
			var decides = (onAThing || bound) && major == 0;

			if ( onAThing && decides )
				Unimplemented.Report( "SAVE_PARK_GUEST_ON_A_THING" );

			// A rider's sprite is the thing's to keep: admission destroys it on a thing without the bit (0x00502147).
			var drawn = decides || peep.State != PeepState.Riding
				|| (State.TryObject( major, out var ridden ) && (ridden.Flags & ParkWorld.CatalogueObject.KeepsRidersSpriteFlag) != 0);

			var guest = new ParkWorld.GuestState(
				State: (int)(decides ? PeepState.Deciding : peep.State),
				SavedState: (int)(decides ? PeepState.Deciding : peep.SavedState),
				PersonType: peep.PersonType, Cash: peep.Cash, ExitLevel: peep.ExitLevel,
				Happiness: peep.Happiness, Thirst: peep.Thirst, Hunger: peep.Hunger, Toilet: peep.Toilet,
				Vomit: peep.Vomit, Litter: peep.Litter,
				MajorDest: decides ? 0 : major, QueuePos: decides ? 0 : peep.QueuePos, PrankeryIndex: peep.PrankeryIndex,
				PaidAdmission: peep.PaidAdmission ? 1 : 0, ParkOpeningWait: peep.ParkOpeningWait,
				QNext: decides ? 0 : State.NextInQueue( id ), QPrev: decides ? 0 : State.PreviousInQueue( id ),
				BeenAdmitted: !decides && peep.BeenAdmitted ? 1 : 0, QueueMoveDelay: decides ? 0 : peep.QueueMoveDelay,
				PreviousRides: [.. peep.PreviousRides.Select( Handle )],
				PreviousTemporaryRides: [.. peep.PreviousTemporaryRides.Select( Handle )],
				SavedMajorDest: decides ? 0 : Handle( peep.SavedMajorDest ), WalkingTurns: decides ? 0 : peep.WalkingTurns,
				NumRides: peep.NumRides, NumShops: peep.NumShops, NumSideshows: peep.NumSideshows,
				NumSideshowsWon: peep.NumSideshowsWon,
				BalloonScript: 0, RemainingBalloonLife: peep.BalloonLife, LastPosX: peep.BalloonLastX, LastPosY: peep.BalloonLastY,
				ArrivalDate: peep.ArrivalDate, TimeOfLastSpotAnim: peep.TimeOfLastSpotAnim,
				TimeStartedIdling: peep.TimeStartedIdling, ArrivalIndex: peep.VisitorNumber );

			people.Add( Person( id, ParkWorld.GuestModel, peep.Navigator, heading, decides, guest, null,
				new ParkWorld.PaceState( peep.AdjustorSpeed, peep.BaseSpeed, peep.PreviousSpeed, peep.PurposeSpeed ),
				(peep.SpriteKind, peep.SpriteBank), drawn, peep.NextAnimation, peep.NextInterval,
				peep.SetDestSuccessfully, peep.Thoughts, peep.StrandedTime ) with { Balloon = BalloonOf( peep ) } );
		}

		foreach ( var _ in _bursting )
			Unimplemented.Report( "SAVE_PARK_BALLOON_LET_GO" );

		foreach ( var member in _staff )
		{
			var id = member.ThingId;
			var heading = _staffWalks.GetValueOrDefault( id )?.Heading ?? 0;
			var rest = Handle( member.RestArea );
			var toilet = Handle( member.ToiletToClean );

			var stands = member.Activity == StaffActivity.Held
				|| (member.Activity is StaffActivity.GoingToRest or StaffActivity.Resting && rest == 0)
				|| (member.Activity is StaffActivity.GoingToLoo or StaffActivity.Cleaning && toilet == 0);

			var staff = new ParkWorld.StaffState(
				State: (int)(stands ? StaffActivity.Idle : member.Activity), PayGrade: member.PayGrade,
				Happiness: member.Happiness, Tiredness: member.Tiredness, JobsDone: member.JobsDone,
				PatrolBottomLeft: member.PatrolBottomLeft, PatrolTopRight: member.PatrolTopRight,
				RestArea: stands ? 0 : rest, PercentageThroughGrade: member.PercentageThroughGrade,
				TimeStartedIdling: member.TimeStartedIdling, Name: member.Name,
				ToiletToClean: stands ? 0 : toilet, TimeStartedCleaning: member.TimeStartedCleaning,
				TimeStartedEntertaining: member.TimeStartedEntertaining,
				TimeStartedResearching: member.TimeStartedResearching, TimeHired: member.TimeHired );

			// One resting inside a rest area has no sprite, in the files as here.
			var drawn = stands || member.Activity != StaffActivity.Resting;

			people.Add( Person( id, member.Model, member.Navigator, heading, stands, null, staff,
				member.Paced ? new ParkWorld.PaceState( member.AdjustorSpeed, member.BaseSpeed, member.PreviousSpeed, member.PurposeSpeed ) : null,
				_staffLooks.GetValueOrDefault( id ), drawn, member.NextAnimation, member.NextInterval,
				setDest: stands ? false : null, member.Thoughts, 0 ) );
		}

		return people;
	}

	/// <summary>
	/// A guest's held balloon as its own sprite: kind 10 of the one balloon bank, on the script, the set and the
	/// frame it is on and where it was last placed. Its <c>+0xbc</c> is its own set's frames a direction, the set
	/// it was made on (<c>FUN_00475a10( 0x0074f480, 10, bank, set, ... )</c>).
	/// </summary>
	private ParkWorld.WrittenSprite? BalloonOf( Peep peep )
	{
		if ( peep.Balloon is not { } balloon )
			return null;

		var sprite = balloon.Sprite;

		return new ParkWorld.WrittenSprite(
			new ParkWorld.Sprite( Slot: 0, Type: Balloon.SpriteKind, Bank: 0, SpriteNumber: sprite.SpriteNumber,
				X: balloon.X, Height: balloon.Height, Y: balloon.Y, Facing: 0, Frame: sprite.Frame, Alpha: sprite.Alpha,
				State: 0, Script: sprite.Script, Pc: sprite.Pc ),
			sprite.Interval, SetByteOf( Balloon.SpriteKind, 0, sprite.Set ) );
	}

	/// <summary>
	/// The bubble over a person as its own sprite: kind 9, bank 0, on its picture's script where one that has
	/// shown its frame rests, over the person at <see cref="Thoughts.Lift"/>. The original makes every bubble on
	/// bank 0's set 0 (<c>FUN_00475a10( script, 9, 0, 0, ... )</c>, <c>0x0050c062</c>) and the script sets the
	/// picture, so its <c>+0xbc</c> is that set's frames a direction whatever the picture. The bubble here runs no
	/// script (<see cref="Thoughts"/>); it is written as one that has run its first two instructions.
	/// </summary>
	private ParkWorld.WrittenSprite? BubbleOf( Thoughts thoughts, FixedVector position )
	{
		if ( thoughts.Bubble is not { } bubble )
			return null;

		var script = Thoughts.ScriptOf( bubble.Bank, bubble.Set );

		return new ParkWorld.WrittenSprite(
			new ParkWorld.Sprite( Slot: 0, Type: Thoughts.SpriteKind, Bank: 0, SpriteNumber: (bubble.Bank << 4) | bubble.Set,
				X: position.X * 10f / FixedVector.One, Height: Thoughts.Lift, Y: position.Y * 10f / FixedVector.One,
				Facing: 0, Frame: 0, Alpha: 0xff, State: 0, Script: script, Pc: script + Thoughts.ShownAt ),
			MadeSetByte: SetByteOf( Thoughts.SpriteKind, 0, 0 ) );
	}

	/// <summary>
	/// The frames a direction of a bank's set - the byte the original keeps in a sprite's <c>+0xbc</c>
	/// (<c>FUN_00540c60</c>) - or null when the bank is not to hand.
	/// </summary>
	private int? SetByteOf( int kind, int bank, int set )
		=> BankAt( kind, bank ) is { } file && set >= 0 && set < file.Sets.Length ? file.Sets[set].FramesPerDirection : null;

	/// <summary>
	/// One person for the writer. <paramref name="stands"/> writes them standing where they are, on no route and
	/// on the standing program, whatever the walk and the sprite hold.
	/// </summary>
	private ParkWorld.WrittenPerson Person( int id, int model, PeepNavigator navigator, int heading, bool stands,
		ParkWorld.GuestState? guest, ParkWorld.StaffState? staff, ParkWorld.PaceState? pace, (int Kind, int Bank) look,
		bool drawn, int nextAnimation, int nextInterval, bool? setDest, Thoughts thoughts, uint strandedTime )
	{
		var position = navigator.Position;

		var state = stands
			? new ParkWorld.NavigatorState(
				X: position.X, Y: position.Y, VelocityX: 0, VelocityY: 0, TargetX: position.X, TargetY: position.Y,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: navigator.Radius,
				MaxForce: navigator.MaxForce, MaxSpeed: navigator.MaxSpeed,
				NavMode: 0, CantReachDest: 0, PathFinished: true,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 )
			: new ParkWorld.NavigatorState(
				X: position.X, Y: position.Y, VelocityX: navigator.Velocity.X, VelocityY: navigator.Velocity.Y,
				TargetX: navigator.Target.X, TargetY: navigator.Target.Y,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: navigator.Radius,
				MaxForce: navigator.MaxForce, MaxSpeed: navigator.MaxSpeed,
				NavMode: 0, CantReachDest: navigator.CannotReach ? 1 : 0, PathFinished: navigator.Finished,
				PathCount: navigator.Cursor, PathTotalCount: navigator.TotalWaypoints,
				PathBufferCount: navigator.BufferedWaypoints,
				BufferedDistance: navigator.BufferedDistance, TailDistance: navigator.TailDistance,
				TotalDistance: navigator.TotalDistance, StuckBits: navigator.StuckBits );

		var person = new ParkWorld.Person(
			ThingId: id, Model: model, RawX: state.RawX, RawY: state.RawY, SpriteSlot: 0, Angle: heading,
			Navigator: state, Guest: guest, Staff: staff, Pace: pace, SpriteKind: look.Kind, SpriteBank: look.Bank );

		ParkWorld.Sprite? picture = null;
		var interval = SpriteScript.DefaultInterval;
		int? stateSetByte = null;

		if ( drawn )
		{
			var script = _sprites.GetValueOrDefault( id );

			// One written standing keeps a sprite that stands already, and is put on the standing program otherwise.
			if ( script == null || script.Freed || script.Script == SpriteScript.None
				|| (stands && !script.IsOn( SpriteScript.Standing )) )
			{
				script = new SpriteScript( SpriteScript.None, 0, spriteNumber: 0, frame: 0 );
				script.Start( SpriteScript.Standing );
			}

			interval = script.Interval;
			stateSetByte = script.FramesPerDirection > 0 ? script.FramesPerDirection : null;
			picture = new ParkWorld.Sprite(
				Slot: 0, Type: look.Kind, Bank: look.Bank, SpriteNumber: script.SpriteNumber,
				X: 0f, Height: 0f, Y: 0f, Facing: person.Facing, Frame: script.Frame, Alpha: script.Alpha, State: 0,
				Script: script.Script, Pc: script.Pc );
		}

		// A person who has walked no new route since the load holds no waypoints here: the file's are left.
		var routed = !stands && navigator.Waypoints.Count > 0;

		return new ParkWorld.WrittenPerson( person, picture,
			Waypoints: stands ? [] : routed ? [.. navigator.Waypoints.Select( point => (point.X, point.Y) )] : null,
			LegLengths: routed ? [.. navigator.LegLengths] : null,
			PreviousX: stands ? position.X : navigator.Previous.X, PreviousY: stands ? position.Y : navigator.Previous.Y,
			NextAnim: stands ? 0 : nextAnimation, NextServiceInterval: nextInterval,
			SetDestSuccessfully: stands ? false : setDest, LastThought: thoughts.Last, TimeBubbleShown: thoughts.TimeBubbleShown,
			StrandedTime: strandedTime, SpriteInterval: interval,
			// A person's sprite is made on set 0 of their bank, and a state's animation writes its own set's over it.
			MadeSetByte: SetByteOf( look.Kind, look.Bank, 0 ), StateSetByte: stateSetByte,
			Bubble: BubbleOf( thoughts, position ) );
	}
}
