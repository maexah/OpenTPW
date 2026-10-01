using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Giving the things standing in a park the scripts they run. These read real game files and are skipped
/// where there is no installation - see <see cref="GameData"/>.
///
/// <para>
/// These join the reader, the interpreter, the scheduler and the catalogue: a script here belongs to
/// something standing in a park rather than to a harness.
/// </para>
/// </summary>
[TestClass]
public class ParkRidesTests
{
	private BaseFileSystem data = null!;

	/// <summary>
	/// Whatever entities a test made, so that they leave <see cref="Entity.All"/> with the test that made
	/// them rather than piling up for whatever runs next - see <c>EntityLifetimeTests</c>, which is where
	/// this shape comes from.
	/// </summary>
	private readonly List<Entity> made = [];

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
	}

	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Through a stream of this test's own bytes rather than the global file system, which belongs to a
	/// running game and which no test should need to have been set - the rule every other park test
	/// follows, and the reason <see cref="ParkItemCatalogue"/> and <see cref="ParkRides"/> both take one.
	/// </summary>
	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue Catalogue() => new( Theme, data );

	private ParkRides Bind( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	/// <summary>Whether the file system can offer this at all, treating "not there" and "will not open" alike.</summary>
	private bool Has( string path )
	{
		try
		{
			using var stream = data.OpenRead( path );

			return stream != null;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>
	/// <b>A bound script starts where the park file left it, not at its own first instruction.</b> This is
	/// what stops a loaded park playing everything in it being built again: the Belly Bounce's script
	/// opens with <c>WAITANIM 0 0</c>, which starts its construction clip, and the save has it parked
	/// twenty instructions past that - so binding has to put it at word 46 rather than at nought.
	///
	/// <para>
	/// The name is asserted beside the counter because resuming steps over the <c>NAME</c> the script
	/// opens with, and a nameless script cannot be found by <c>FINDSCRIPTRAND</c> - so a resume that
	/// gained the right counter and lost the name would have traded one fault for another.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ABoundScriptResumesWhereTheSaveLeftIt()
	{
		var world = World();
		var rides = Bind( world, Catalogue() );

		var id = rides.ScriptFor( BellyBounceThing );

		Assert.AreNotEqual( 0, id, "the Belly Bounce should have been given a script" );

		var script = rides.Scheduler.Find( id );

		Assert.IsNotNull( script, "and that script should be in the scheduler" );
		Assert.AreEqual( 46, script!.Position, "where its script was saved, and so where it must start" );
		Assert.AreNotEqual( 0, script.Position, "starting at nought is what replayed the construction clip" );

		Assert.IsTrue( script.IsNamed, "a resumed script still has to know its own name" );
		Assert.AreNotEqual( string.Empty, script.Name, "and to have taken it" );

		// <b>NotResumed is the load-bearing one</b>: it says every counter that was offered landed on an
		// instruction boundary, across all the scripts this park binds rather than just this one.
		//
		// What is deliberately NOT asserted is `Bound == Resumed`. That equality holds only because this
		// fixture passes no ParkObjects: with one, the second binding pass spawns things the save never
		// named - the ferry and the seaplane - which have no saved state and correctly do not resume, so
		// the running game has Bound > Resumed. Asserting it here would have pinned a property of the
		// fixture that production breaks.
		Assert.AreEqual( 0, rides.NotResumed, "no bound script was left at its beginning" );
		Assert.AreNotEqual( 0, rides.Resumed, "and something was actually resumed" );
		Assert.IsTrue( rides.Resumed <= rides.Bound,
			$"{rides.Resumed} resumed of {rides.Bound} bound" );

		// <b>And the other half.</b> Resuming a script past its prologue means it never runs the LOOPANIM in
		// that prologue again, so a thing whose channels are not also put back stands frozen for the
		// whole session. These are named things rather than a count, because a count here would be a
		// property of this fixture - it binds no ParkObjects, so three of the fourteen never arrive.
		Assert.AreNotEqual( 0, rides.ChannelsRestored, "some channel was put back" );

		int RoleOn( int thing, int channel = 0 )
		{
			var script = rides.Scheduler.Find( rides.ScriptFor( thing ) );

			Assert.IsNotNull( script, $"thing {thing} should be running a script" );
			Assert.IsNotNull( script!.Animations, $"and thing {thing} should have animation players" );

			return script.Animations!.Channel( channel )?.AnimID ?? -1;
		}

		// The Fountain is the case that needs the restore: its script resumes into a two-instruction
		// loop that can never reach the LOOPANIM that starts this clip, so only the restore puts it on.
		Assert.AreEqual( 5, RoleOn( FountainThing ), "the Fountain's saved role" );

		// All three of the sideshow's lanes, which is what needs the per-item channel count to be right.
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 0 ), "the Jungle Spray's first lane" );
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 1 ), "its second" );
		Assert.AreEqual( 2, RoleOn( JungleSprayThing, 2 ), "its third" );

		// And the one that must NOT be given a clip: its record saves the sentinel, so a blanket restore
		// would show up here and nowhere else.
		Assert.AreEqual( RideAnimations.NoRole, RoleOn( StaffRoomThing ),
			"the Staff Room is saved running nothing, and must stay that way" );

		// <b>And the channel's STATE, not just which clip it holds.</b> Every assertion above reads only
		// AnimID, which Start sets from the role whatever the flags say - so handing the saved flag word
		// straight in as a caller flag, or dropping the held re-entry entirely, would pass all of them while
		// eleven of the park's fifteen channels silently restart their clip from frame nought.
		AnimTimeControl ChannelOn( int thing, int channel = 0 )
		{
			var script = rides.Scheduler.Find( rides.ScriptFor( thing ) );
			var player = script?.Animations?.Channel( channel );

			Assert.IsNotNull( player, $"thing {thing} channel {channel} should exist" );

			return player!;
		}

		// A Small Toilet is saved HELD on its last frame - the engine's own way of saying the clip has
		// finished - so it must come back parked at the end rather than playing from the beginning.
		var held = ChannelOn( ToiletThing );

		Assert.IsTrue( held.TotalAnimFrames > 0f, "the toilet's clip should have a length" );
		// Within a frame of it: the restore's own arithmetic works the frame out from the saved stamps, whole
		// milliseconds apart, and the next advance puts it on the total exactly.
		Assert.AreEqual( held.TotalAnimFrames, held.AnimFrame, 0.1f,
			"a thing saved held belongs at the end of its clip, not at frame nought" );
		Assert.AreNotEqual( 0, held.Flags & HeldFlag, "and must carry the engine's own held mark" );

		// The Fountain is saved LOOPING, which is the other arm: it goes on from the frame its save had
		// reached, 1,625 ms into its clip, and repeats.
		var looping = ChannelOn( FountainThing );

		Assert.AreNotEqual( 0, looping.Flags & LoopFlag, "the Fountain is saved looping" );
		Assert.AreEqual( 1625 * 0.03f, looping.AnimFrame, 0.01f, "so it goes on from where its save left it" );
		Assert.AreEqual( rides.LoadedAt - 1625, looping.StartAnimTime, "its start that far before the load" );

		// And the speed the save carries, which is not always one: this ride is saved at 1.1, and passing
		// a literal 1f would run it at the wrong rate for the whole session.
		Assert.AreEqual( 1.1f, ChannelOn( BellyBounceThing ).Speed, 0.001f,
			"the Belly Bounce's saved playback speed" );
	}

	/// <summary>
	/// <b>A bound script resumes with its stack, both its indices and its result register</b>, which the
	/// save reader restores with the rest of the struct. The Fountain is saved with 3033 in its register
	/// (the Gates, with 1, is not bound by this fixture), and the Belly Bounce is the one with a stack: saved
	/// with nothing pushed, its top slot holding the tagged return address of a call that had returned.
	/// </summary>
	[TestMethod]
	public void ABoundScriptResumesWithItsStackAndResultRegister()
	{
		var rides = Bind( World(), Catalogue() );

		RideScript ScriptOf( int thing ) => rides.Scheduler.Find( rides.ScriptFor( thing ) )!;

		Assert.AreEqual( 3033, ScriptOf( FountainThing ).Result, "the Fountain's saved register" );

		var bouncy = ScriptOf( BellyBounceThing );

		Assert.AreEqual( 3, bouncy.Stack.Count, "the Belly Bounce's saved stack" );
		Assert.AreEqual( 0x20000018, bouncy.Stack[2], "its top slot, a return address with its tag" );
		Assert.AreEqual( 2, bouncy.CallIndex, "saved with no frame open" );
		Assert.AreEqual( 0, bouncy.HeapIndex, "and nothing hushed" );
	}

	/// <summary>
	/// <b>A loaded script waits out what its save had left of a wait, not the whole of it again.</b> Both security
	/// cameras are saved on their <c>WAIT 5000</c> at word 14, with 2,341 and 2,329 ms left, and after it comes
	/// round to <c>WAITANIM 4, 0</c> at word 5, which takes channel 0 off the role 6 it is saved holding. A script
	/// takes a turn one tick in eight (<see cref="RideScriptScheduler.RunsOn"/>), so each passes its wait on its
	/// first turn from the 76th tick (2,356 ms) on: both are on role 6 at the 75th and on role 4 by the 83rd, where
	/// waiting the whole 5,000 again from their first turn would keep them on role 6 to the 169th at the soonest.
	/// </summary>
	[TestMethod]
	public void ALoadedCameraWaitsOutOnlyWhatItsSaveHadLeft()
	{
		var world = World();
		var rides = Bind( world, Catalogue() );

		var cameras = world.Objects.Where( o => o.CatalogueId == SecurityCamera )
			.Select( o => rides.Scheduler.Find( rides.ScriptFor( o.ThingId ) )! ).ToArray();

		Assert.AreEqual( 2, cameras.Length, "the shipped park's two security cameras" );

		// A saved nought is the engine's empty slot and comes back as none: of every script bound, only the cameras
		// and the Belly Bounce are saved on a wait, and only the sideshow keeps a trigger's deadline (long past).
		var bound = rides.Scheduler.Scripts.ToArray();

		Assert.AreEqual( 3, bound.Count( script => script.Waiting ), "three scripts saved on a wait, and no more" );
		Assert.AreEqual( 1, bound.Count( script => script.WaitingForAnimation ), "one trigger deadline, and no more" );

		foreach ( var camera in cameras )
		{
			Assert.AreEqual( 14, camera.Position, "each saved on its WAIT 5000" );
			Assert.IsTrue( camera.Waiting, "and waiting on it" );
		}

		for ( var tick = 1; tick <= 75; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		foreach ( var camera in cameras )
			Assert.AreEqual( 6, camera.Animations!.Channel( 0 )!.AnimID, "still on the role it was saved holding at 2,325 ms" );

		for ( var tick = 76; tick <= 83; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		foreach ( var camera in cameras )
			Assert.AreEqual( 4, camera.Animations!.Channel( 0 )!.AnimID, "and on to WAITANIM 4, 0 by 2,573 ms" );
	}

	/// <summary>
	/// <b>A loaded park's scripts take their turns on the save's ticks.</b> Each keeps its saved handle and the tick
	/// counts on from the saved 6,055, so the camera on handle 8 is due when <c>6055 + n</c> is a multiple of eight
	/// and the one on handle 9 a tick later. Both waits are past by the 76th tick, so they pass on the 81st and the
	/// 82nd, one each; numbered afresh from tick nought, they passed on the 79th and 80th.
	/// </summary>
	[TestMethod]
	public void ALoadedParksScriptsTakeTheirTurnsOnTheSavesTicks()
	{
		var world = World();
		var rides = Bind( world, Catalogue() );

		Assert.AreEqual( 6055, rides.Scheduler.Tick, "the saved tick" );

		// The module holds them from 15 down, each put at the head as it is read, so the turns go from the lowest up.
		// The gates (15) and the lights (2, 1) stand nowhere a test draws, so they are not bound here.
		CollectionAssert.AreEqual( new[] { 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14 },
			rides.Scheduler.NewestFirst().Select( script => script.Id ).ToArray(), "the order of turns within a tick" );

		foreach ( var placed in world.Objects.Where( o => rides.ScriptFor( o.ThingId ) != 0 ) )
			Assert.AreEqual( placed.RideScript, rides.ScriptFor( placed.ThingId ), $"thing {placed.ThingId}'s saved handle" );

		var cameras = world.Objects.Where( o => o.CatalogueId == SecurityCamera )
			.ToDictionary( o => o.RideScript, o => rides.Scheduler.Find( rides.ScriptFor( o.ThingId ) )! );

		CollectionAssert.AreEquivalent( new[] { 8, 9 }, cameras.Keys.ToArray(), "the cameras' saved handles" );

		int RoleOf( int handle ) => cameras[handle].Animations!.Channel( 0 )!.AnimID;

		for ( var tick = 1; tick <= 80; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		Assert.AreEqual( 6, RoleOf( 8 ), "handle 8 still waiting at the 80th" );
		Assert.AreEqual( 6, RoleOf( 9 ), "handle 9 still waiting at the 80th" );

		rides.Scheduler.Advance( rides.LoadedAt + (81 * 31f) );

		Assert.AreEqual( 4, RoleOf( 8 ), "handle 8 passes on the 81st" );
		Assert.AreEqual( 6, RoleOf( 9 ), "handle 9 not yet" );

		rides.Scheduler.Advance( rides.LoadedAt + (82 * 31f) );

		Assert.AreEqual( 4, RoleOf( 9 ), "handle 9 passes on the 82nd" );
	}

	/// <summary>A thing bought after a load is numbered past every saved script, from the saved next handle.</summary>
	[TestMethod]
	public void AThingBoughtAfterALoadTakesTheSavedNextHandle()
	{
		var world = World();
		var catalogue = Catalogue();
		var rides = Bind( world, catalogue );

		Assert.IsTrue( catalogue.TryGet( SecurityCamera, out var item ), "the jungle offers a camera" );

		var placed = new ParkWorld.CatalogueObject( ThingId: 900, CatalogueId: SecurityCamera,
			RawX: 20 << 8, RawY: 12 << 8, Angle: 0 );

		Assert.IsTrue( rides.BindNew( placed, item ), "a camera has a script" );
		Assert.AreEqual( 16, rides.ScriptFor( 900 ), "the save's next handle" );
	}

	/// <summary>
	/// <b>The load's moment is the clock the park's ticks run on</b>, the last tick's instant as the load finds it,
	/// which is where the save's own moment is put; every saved deadline keeps its distance from it. With the game
	/// clock 64 ticks further on, the cameras' waits end 2,329 and 2,341 ms after that instant, not after nought.
	/// </summary>
	[TestMethod]
	public void TheLoadsMomentIsTheClockTheParksTicksRunOn()
	{
		// From a clean re-base, spending the frame it throws away, so what the four frames add does not hang on
		// what an earlier test left owed.
		Time.Paused = false;
		GameClock.Rebase();
		Time.Update( 0f );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );

		var before = GameClock.Ticks;

		for ( var frame = 0; frame < 4; ++frame )
		{
			Time.Update( 0.5f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
		}

		var world = World();
		var rides = Bind( world, Catalogue() );

		Assert.AreEqual( 64, GameClock.Ticks - before, "four half-second frames are 64 ticks" );
		Assert.AreEqual( GameClock.Ticks * 31, rides.LoadedAt, "the load's moment is the last tick's instant" );

		var waits = world.Objects.Where( o => o.CatalogueId == SecurityCamera )
			.Select( o => rides.Scheduler.Find( rides.ScriptFor( o.ThingId ) )!.WaitDeadline!.Value - rides.LoadedAt )
			.OrderBy( left => left ).ToArray();

		CollectionAssert.AreEqual( new[] { 2329f, 2341f }, waits, "each camera's wait that far after it" );
	}

	/// <summary>
	/// <b>A loaded script keeps the loop its save was running.</b> The Belly Bounce is saved with its looping key 2
	/// and channel 0 looping role 2 at 1.1, and with nobody aboard its turn comes round to <c>LOOPANIM 2, 0</c> at
	/// word 43 on every pass. With the key restored that instruction does nothing, as the engine's does, so nothing
	/// is queued behind the saved loop; with the key lost, the first pass would queue the loop again behind itself.
	/// Its <c>WAIT 500</c>, saved with 63 ms left, passes on its first turn from the third tick (93 ms) on - a
	/// turn comes one tick in eight - and runs to the <c>CRIT_UNLOCK</c> at word 99, which ends the turn; the next
	/// turn, 8 ticks on, comes round through word 43 and arms the next <c>WAIT 500</c>.
	/// </summary>
	[TestMethod]
	public void ALoadedBellyBounceKeepsTheLoopItsSaveWasRunning()
	{
		var rides = Bind( World(), Catalogue() );
		var bouncy = rides.Scheduler.Find( rides.ScriptFor( BellyBounceThing ) )!;
		var channel = bouncy.Animations!.Channel( 0 )!;

		Assert.AreEqual( 2, bouncy.LoopingKey, "the key its save holds" );
		Assert.AreEqual( rides.LoadedAt + 63f, bouncy.WaitDeadline, "and what was left of its WAIT 500" );

		for ( var tick = 1; tick <= 24; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		Assert.IsNotNull( bouncy.WaitDeadline, "back on its WAIT 500" );

		var armed = (bouncy.WaitDeadline!.Value - rides.LoadedAt - 500f) / 31f;

		Assert.IsTrue( armed >= 11f && armed <= 18f && armed == MathF.Floor( armed ),
			$"round through word 43 to the next WAIT 500, armed on a turn from the 11th tick to the 18th, not {armed}" );
		Assert.IsFalse( channel.HasQueued, "and the LOOPANIM 2, 0 on the way queued nothing" );
		Assert.AreEqual( 2, channel.AnimID, "the saved loop still playing" );
		Assert.AreEqual( 1.1f, channel.Speed, 0.001f, "at its saved speed" );
		Assert.AreEqual( 2, bouncy.LoopingKey, "and the key unchanged" );
	}

	/// <summary>
	/// <b>Every saved field the shipped park leaves empty comes back too</b>, from a copy of that park with the Belly
	/// Bounce's record given a trigger deadline 5,000 ms ahead of the save, a <c>TRIGWAITANIM</c> mark for role 2 and a
	/// timer 7,000 ms ahead, and its channel a clip queued behind the loop with flags and a speed of its own. Each
	/// deadline lands the same distance after the load's moment, and the queued clip is queued again as it was.
	/// </summary>
	[TestMethod]
	public void EverySavedWaitMarkTimerAndQueueComesBack()
	{
		var payload = Payload();
		var clock = new ParkClock( payload ).Reading!.Value;

		// The Belly Bounce's struct, found by its WAIT deadline (63 ms after the save) with its handle and counter
		// beside it, and its channel by its start stamp (1,376 ms before it) with role 2 beside it.
		var script = Single( payload, clock + 63, at => at >= 0xa0
			&& BitConverter.ToInt32( payload, at - 0xa0 + 0x08 ) == 3 && BitConverter.ToInt32( payload, at - 0xa0 + 0x3c ) == 46 ) - 0xa0;
		var channel = Single( payload, clock - 1376, at => at >= 12 && BitConverter.ToInt32( payload, at - 12 + 4 ) == 2 ) - 12;

		BitConverter.GetBytes( clock + 5000 ).CopyTo( payload, script + 0xa4 );
		BitConverter.GetBytes( 3 ).CopyTo( payload, script + 0xbc );
		BitConverter.GetBytes( clock + 7000 ).CopyTo( payload, script + 0xc4 );
		BitConverter.GetBytes( 5 ).CopyTo( payload, channel + 28 );
		BitConverter.GetBytes( 1 ).CopyTo( payload, channel + 32 );
		BitConverter.GetBytes( 0x9 ).CopyTo( payload, channel + 36 );
		BitConverter.GetBytes( 0.5f ).CopyTo( payload, channel + 40 );

		var rides = Bind( new ParkWorld( payload ), Catalogue() );
		var bouncy = rides.Scheduler.Find( rides.ScriptFor( BellyBounceThing ) )!;
		var player = bouncy.Animations!.Channel( 0 )!;

		Assert.AreEqual( rides.LoadedAt + 63f, bouncy.WaitDeadline, "the wait" );
		Assert.AreEqual( rides.LoadedAt + 5000f, bouncy.AnimationDeadline, "the trigger's deadline" );
		Assert.AreEqual( 2, bouncy.LoopingKey, "the looping key" );
		Assert.AreEqual( 3, bouncy.AnimationMark, "the mark" );
		Assert.AreEqual( rides.LoadedAt + 7000f, bouncy.TimerDeadline, "the timer" );

		Assert.IsTrue( player.HasQueued, "and the clip queued behind the loop" );
		Assert.AreEqual( 5, player.DeferredAnimID, "role 5" );
		Assert.AreEqual( 1, player.DeferredSubAnim, "entry 1" );
		Assert.AreEqual( 0x9, player.DeferredFlags, "with the flags it was queued with" );
		Assert.AreEqual( 0.5f, player.DeferredSpeed, "and its own speed" );
		Assert.AreEqual( 2, player.AnimID, "behind the saved loop, still playing" );
	}

	/// <summary>
	/// <b>A load whose model states will not read keeps its scripts' loops fresh.</b> With the channel module's
	/// leading tag spoiled, no channel is put back, so every model starts idle; the Belly Bounce's waits still come
	/// back, but its looping key does not, so its <c>LOOPANIM 2, 0</c> at word 43 starts the loop again rather than
	/// doing nothing over a channel playing none.
	/// </summary>
	[TestMethod]
	public void ALoadWhoseChannelsWillNotReadKeepsItsLoopsFresh()
	{
		var payload = Payload();
		var tag = System.Text.Encoding.ASCII.GetBytes( "SYSG" );
		var at = Single( payload, BitConverter.ToUInt32( tag ), _ => true );

		payload[at] = (byte)'X';

		var rides = Bind( new ParkWorld( payload ), Catalogue() );
		var bouncy = rides.Scheduler.Find( rides.ScriptFor( BellyBounceThing ) )!;

		Assert.AreEqual( 0, rides.ChannelsRestored, "no channel put back" );
		Assert.AreEqual( rides.LoadedAt + 63f, bouncy.WaitDeadline, "its wait still comes back" );
		Assert.AreEqual( 0, bouncy.LoopingKey, "but not the key of a loop its channel is not playing" );

		for ( var tick = 1; tick <= 24; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		Assert.AreEqual( 2, bouncy.Animations!.Channel( 0 )!.AnimID, "so the LOOPANIM 2, 0 at word 43 starts it" );
		Assert.AreEqual( 2, bouncy.LoopingKey, "and keys it" );
	}

	private byte[] Payload()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new SaveReader( stream ).ReadFile();
	}

	/// <summary>Where a dword occurs in the payload with its neighbours as expected, asserting exactly once.</summary>
	private static int Single( byte[] payload, uint value, Func<int, bool> beside )
	{
		var bytes = BitConverter.GetBytes( value );
		var found = -1;

		for ( var at = 0; at + 4 <= payload.Length; ++at )
		{
			if ( !payload.AsSpan( at, 4 ).SequenceEqual( bytes ) || !beside( at ) )
				continue;

			Assert.AreEqual( -1, found, $"0x{value:x8} occurs more than once" );
			found = at;
		}

		Assert.AreNotEqual( -1, found, $"0x{value:x8} does not occur" );

		return found;
	}

	/// <summary>The Security Camera's catalogue id; the shipped park places two.</summary>
	private const int SecurityCamera = 1413;

	/// <summary>The Belly Bounce - thing 13, the shipped park's only ride.</summary>
	private const int BellyBounceThing = 13;

	/// <summary>
	/// The Fountain Feature - thing 24, and the case where restoring the script alone is a loss.
	/// Its script resumes into a two-instruction loop that can never reach the <c>LOOPANIM</c> starting
	/// the only clip it has, so nothing but the channel restore ever puts that clip on.
	/// </summary>
	private const int FountainThing = 24;

	/// <summary>The Jungle Spray - thing 14, the one thing here that runs three animation channels.</summary>
	private const int JungleSprayThing = 14;

	/// <summary>The Staff Room - thing 20, whose record saves the sentinel rather than a role.</summary>
	private const int StaffRoomThing = 20;

	/// <summary>One of the three Small Toilets - thing 21, saved HELD on its clip's last frame.</summary>
	private const int ToiletThing = 21;

	/// <summary>The engine's own mark for a channel holding its last frame - <c>AnimTimeControl</c>'s 0x4.</summary>
	private const int HeldFlag = 0x4;

	/// <summary>And for one that loops.</summary>
	private const int LoopFlag = 0x1;

	/// <summary>
	/// Every placed thing whose archive holds a script is running one, and every one whose archive does
	/// not is counted as having none.
	///
	/// <para>
	/// <b>The number is arrived at twice, by two different routes.</b> This test opens the files itself;
	/// <see cref="ParkRides"/> loads them through the scheduler. A binding that quietly skipped a whole
	/// folder - or one that counted an item it never actually loaded - would still look tidy in its own
	/// log, and disagreeing counts are what catches it. Asserting the total is non-zero first stops the
	/// whole thing passing vacuously if the park or the catalogue ever came back empty.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryPlacedThingWhoseArchiveHoldsAScriptIsRunningIt()
	{
		var world = World();
		var catalogue = Catalogue();

		var withScript = 0;
		var without = 0;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			if ( Has( ParkRides.ScriptPathFor( item ) ) )
				++withScript;
			else
				++without;
		}

		Assert.IsTrue( withScript > 0,
			"nothing placed in Lost Kingdom ships a script, so this test would prove nothing" );

		var rides = Bind( world, catalogue );

		Assert.AreEqual( withScript, rides.Bound, "things given a script" );
		Assert.AreEqual( without, rides.Scriptless, "things with none to give" );
	}

	/// <summary>
	/// Each bound script has an id of its own, the registry really holds it, and it knows the directory it
	/// came from.
	///
	/// <para>
	/// The directory is not decoration: a script names whatever it spawns relative to its own folder, and
	/// the names are far from unique across the game - so a script that does not know where it came from
	/// can only spawn the wrong file or nothing at all. Nothing Lost Kingdom places happens to spawn
	/// anything, which is exactly why this pins the prefix here rather than relying on the park to
	/// exercise it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EachBoundScriptHasItsOwnIdAndKnowsWhereItCameFrom()
	{
		var world = World();
		var catalogue = Catalogue();
		var rides = Bind( world, catalogue );

		var seen = new HashSet<int>();

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var id = rides.ScriptFor( placed.ThingId );

			if ( id == 0 )
				continue;

			Assert.IsTrue( seen.Add( id ), $"script id {id} was handed out twice" );

			var script = rides.Scheduler.Find( id );

			Assert.IsNotNull( script, $"thing {placed.ThingId} names script {id}, which the registry has not got" );
			Assert.AreEqual( item.Directory, script!.Directory,
				"a script has to know the folder it came from, or nothing it spawns can be found" );
		}

		Assert.AreEqual( rides.Bound, seen.Count, "every bound script should be reachable from its own thing" );
	}

	/// <summary>
	/// An item shipping no script at all is ordinary data rather than a fault, and the game really does
	/// ship one: <c>mystery</c>, in all four themes, catalogued like any other item and holding no
	/// <c>.RSE</c> whatever.
	///
	/// <para>
	/// The original's answer to this case is to log "Ride script not located for %s - using placeholder"
	/// and load <c>Data\TestScript\test.rse</c> instead (0x004dcf90) - and <b>that file does not ship
	/// either</b>, so its fallback fails to open as well and its loader simply answers nought. Which is
	/// why nothing here treats a missing script as an error to report.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AnItemCanShipNoScriptAtAll()
	{
		var mystery = new ParkItemCatalogue.Item( 100, "Mystery", $"levels/{Theme}/rides/mystery", "mystery",
			1, 1, null );

		Assert.IsTrue( Has( $"{mystery.Directory}/{mystery.Stem}.sam" ),
			"the item itself should be there, or this is testing a typo" );

		Assert.IsFalse( Has( ParkRides.ScriptPathFor( mystery ) ), "and it should ship no script" );
	}

	/// <summary>
	/// The spelling belongs to the archives and not to us. The jungle alone holds <c>Bouncy.RSE</c> and
	/// <c>Toilet.rse</c> - the stem and the extension each vary - so the lookup has to fold case across
	/// the whole path, archives included, or most of a park would silently run nothing.
	///
	/// <para>
	/// This is the trap that would break a park on Linux and on nothing else, since Windows folds case
	/// itself: see <c>FileSystemCaseTests</c>, which pins the behaviour this depends on.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AScriptIsFoundHoweverItsArchiveSpellsIt()
	{
		Assert.IsTrue( Has( "levels/jungle/rides/bouncy/bouncy.rse" ), "asked for all in lower case" );
		Assert.IsTrue( Has( "levels/jungle/rides/bouncy/BOUNCY.RSE" ), "and all in upper" );
		Assert.IsTrue( Has( "levels/jungle/features/toilet/toilet.RSE" ),
			"an item whose script is the one spelled .rse" );
	}

	/// <summary>
	/// The park's scripts run when they are given turns, and none of them stops.
	///
	/// <para>
	/// Sixty-four ticks, so that every script has had several turns under the engine's one-in-eight rule
	/// rather than only the ones whose ids happen to fall early. Nothing should finish: a shipped script
	/// is written as an endless loop, so one that stops is a machine fault rather than a script ending.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheParksScriptsRunWhenTheyAreGivenTurns()
	{
		var rides = Bind( World(), Catalogue() );

		// The milliseconds the interpreter counts in, at the 31ms beat a park ticks on, from the load's own moment,
		// which the saved waits are measured from.
		for ( int tick = 1; tick <= 64; ++tick )
			rides.Scheduler.Advance( rides.LoadedAt + (tick * 31f) );

		Assert.IsTrue( rides.Scheduler.TurnsGiven > 0, "nobody was given a turn at all" );
		Assert.AreEqual( 0, rides.Scheduler.Finished, "a shipped script stopped, which none of them should" );
		Assert.IsTrue( rides.Scheduler.Count >= rides.Bound, "a script went missing from the registry" );
	}

	/// <summary>
	/// Every bound script knows which thing it belongs to, and can see that thing's own animations.
	///
	/// <para>
	/// Both are seeded once, at load, and never again: the engine's loader takes the thing as its second
	/// argument and keeps it at <c>+0xac</c>, then fills the model handle at <c>+0xc8</c> from that
	/// thing's own entry in the world's table. Nothing writes either afterwards, so a script that did not
	/// get them at that moment never will - which is why this checks the binding rather than some later
	/// state.
	/// </para>
	///
	/// <para>
	/// <b>The clips are counted twice by separate routes.</b> This test loads each item's roles itself
	/// where <see cref="ParkRides"/> loads them through the binding, because handing every script the same
	/// empty table would still look perfectly tidy in its own log.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryBoundScriptKnowsItsThingAndCanSeeItsAnimations()
	{
		var world = World();
		var catalogue = Catalogue();
		var rides = Bind( world, catalogue );

		var withClips = 0;

		foreach ( var placed in world.Objects )
		{
			if ( !placed.IsPlaced || !catalogue.TryGet( placed.CatalogueId, out var item ) )
				continue;

			var id = rides.ScriptFor( placed.ThingId );

			if ( id == 0 )
				continue;

			var script = rides.Scheduler.Find( id );

			Assert.IsNotNull( script, $"thing {placed.ThingId} names a script the registry has not got" );
			Assert.AreEqual( placed.ThingId, script!.ThingId, $"'{item.Name}' does not know which thing it drives" );
			Assert.IsNotNull( script.Animations, $"'{item.Name}' was given no model at all" );

			var ours = RideAnimations.Load( item.Directory, item.Stem, data );

			Assert.AreEqual( ours.Loaded, script.Animations!.Loaded, $"'{item.Name}': clips" );
			Assert.AreEqual( ours.Roles, script.Animations.Roles, $"'{item.Name}': roles holding anything" );

			if ( ours.Loaded > 0 )
				++withClips;
		}

		Assert.IsTrue( withClips > 0,
			"nothing placed in Lost Kingdom ships a clip, so this test would prove nothing" );

		Assert.AreEqual( withClips, rides.Animated, "things that can see their own animations" );
	}

	/// <summary>
	/// <b>A bound script walks between its own thing's nodes, where the thing stands.</b> The stock park's Jungle Spray,
	/// bound from the save, finds its <c>entrance</c> at the world x and z the original held it at live in the same
	/// park (docs/exe/ride-operation.md, "How long a leg lasts, and where its ends are"); the Drinks Shop, whose script
	/// walks nobody, is given no nodes.
	/// </summary>
	[TestMethod]
	public void TheJungleSprayWalksBetweenItsOwnNodesWhereItStands()
	{
		var world = World();
		var rides = Bind( world, Catalogue() );

		var spray = rides.Scheduler.Find( rides.ScriptFor( 14 ) );

		Assert.IsNotNull( spray?.Nodes, "the Jungle Spray, thing 14, should be bound with its model's nodes" );
		Assert.AreEqual( NodeEnd.Posed, spray!.Nodes!.Find( 4, RideNodes.WalkSpace, out var entrance ) );
		Assert.AreEqual( 525.127f, entrance.X, 0.001f, "the entrance's x, as the original held it" );
		Assert.AreEqual( 300.844f, entrance.Z, 0.001f, "and its z" );

		var shop = rides.Scheduler.Find( rides.ScriptFor( 16 ) );

		Assert.IsNotNull( shop, "the Drinks Shop, thing 16, should be bound" );
		Assert.AreEqual( 0, shop!.WalkSlots, "its script declares no walk slot" );
		Assert.IsNull( shop.Nodes, "so it reads no model for one" );
	}
}
