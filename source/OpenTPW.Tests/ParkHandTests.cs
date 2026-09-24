using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The hand's ways out, and that it holds one thing at a time: <see cref="ParkHand"/>, the right button
/// (<see cref="Level.RightButton"/>), Escape (<c>ParkFrontEnd.MenuKey</c>) and what taking something into
/// the hand does to what was there. These read the shipped Lost Kingdom and are skipped where there is no
/// installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>Each drives the code a player's press reaches.</b> The right button is <see cref="Level.RightButton"/>
/// on a level made without its constructor, handed the button and the frame clock as a frame would; Escape is
/// the park front end's own handler, reached by reflection on one made the same way. Neither needs a window:
/// a way out that lets go returns before it touches one, and one that did not would open the game menu on a
/// stack that is not there and throw.
/// </para>
/// </summary>
[TestClass]
public class ParkHandTests
{
	private BaseFileSystem data = null!;

	private readonly List<Entity> made = [];

	private bool rmbCancelBefore;
	private float nowBefore;

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();
		rmbCancelBefore = GameOptions.Current.RmbCancel;
		nowBefore = Time.Now;
	}

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		ParkHand.LetGo();
		ParkBuildMode.Forget();
		GameOptions.Current.RmbCancel = rmbCancelBefore;
		Time.Now = nowBefore;

		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
	}

	private const string Theme = "jungle";

	/// <summary>The shipped park's Belly Bounce, anchored (51,23).</summary>
	private const int BellyBounceThing = 13;

	/// <summary>The Belly Bounce's catalogue item, which costs 500.</summary>
	private const int BellyBounceItem = 1100;

	/// <summary>The park and what it sells, as <see cref="MoveTheBellyBounceIntoTheHand"/> last read them.</summary>
	private ParkWorld world = null!;
	private ParkItemCatalogue catalogue = null!;

	/// <summary>
	/// Where the pointer is for every right click here, in the interface's units - anywhere will do; only how far
	/// it moves counts.
	/// </summary>
	private static readonly Vector2 Pointer = new( 400, 300 );

	/// <summary>
	/// <b>With RMB cancel off, no right click lets go</b> - neither the press nor a quick release - because every
	/// carrying mode's right-button slots are bare <c>RET 8</c>. A moved thing is in the hand already sold, so a
	/// wrong let-go is a sale.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> emptying the hand on the press, or letting a quick click go whatever the option says, each
	/// empty it here.
	/// </remarks>
	[TestMethod]
	public void WithRmbCancelOffNoRightClickLetsGo()
	{
		var (state, item) = MoveTheBellyBounceIntoTheHand();
		var level = ALevel();

		GameOptions.Current.RmbCancel = false;

		QuickRightClick( level );

		Assert.AreEqual( item, ParkBuilding.Carrying, "still in the hand" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and still sold, as it was at the pickup" );
	}

	/// <summary>
	/// <b>With RMB cancel on, only a QUICK click lets go</b>: held no more than 200 ms and moved no more than 8
	/// units either way. A held click and a dragged one leave the hand as it is, and so does the press on its own.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> emptying the hand on the press; dropping the time test; dropping the distance test; or
	/// testing either on the press rather than the release, each empty the hand before the quick click.
	/// </remarks>
	[TestMethod]
	public void WithRmbCancelOnOnlyAQuickClickLetsGo()
	{
		var (state, item) = MoveTheBellyBounceIntoTheHand();
		var level = ALevel();
		var balance = state.Balance;

		GameOptions.Current.RmbCancel = true;

		Time.Now = 10f;
		Assert.IsNull( level.RightButton( true, Pointer ), "the press does nothing" );
		Assert.AreEqual( item, ParkBuilding.Carrying, "the press leaves the hand full" );

		Time.Now = 10.3f;
		Assert.IsNull( level.RightButton( false, Pointer ), "a release 300 ms on does nothing" );
		Assert.AreEqual( item, ParkBuilding.Carrying, "a held click leaves the hand full" );

		Time.Now = 20f;
		level.RightButton( true, Pointer );
		Time.Now = 20.05f;
		Assert.IsNull( level.RightButton( false, Pointer + new Vector2( 9, 0 ) ), "a release 9 units away does nothing" );
		Assert.AreEqual( item, ParkBuilding.Carrying, "a dragged click leaves the hand full" );

		var letGo = QuickRightClick( level );

		Assert.IsNotNull( letGo, "a quick click answers what it let go of" );
		StringAssert.Contains( letGo, $"let go of item {item}" );
		Assert.AreEqual( 0, ParkBuilding.Carrying, "a quick click empties the hand" );
		Assert.AreEqual( balance, state.Balance, "refunding nothing and charging nothing" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and the moved thing stays sold" );
	}

	/// <summary>
	/// <b>A press that has moved more than 8 units is no longer a click, even if it comes back</b>: the original
	/// lets the click go on the move itself, across or down, and a release does not bring it back. Exactly 8
	/// each way is still a click.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> testing the distance only at the release lets the press that came back go; measuring it
	/// as one straight-line distance, or letting 8 itself go, keeps the diagonal click from letting go.
	/// </remarks>
	[TestMethod]
	public void APressThatMovedAwayAndBackIsNoLongerAClick()
	{
		var (_, item) = MoveTheBellyBounceIntoTheHand();
		var level = ALevel();

		GameOptions.Current.RmbCancel = true;

		Time.Now = 30f;
		level.RightButton( true, Pointer );
		level.RightButton( true, Pointer + new Vector2( 0, 9 ) );
		Time.Now = 30.05f;

		Assert.IsNull( level.RightButton( false, Pointer ), "moved 9 down and back, so not a click" );
		Assert.AreEqual( item, ParkBuilding.Carrying, "and the hand is still full" );

		Time.Now = 40f;
		level.RightButton( true, Pointer );
		Time.Now = 40.05f;

		Assert.IsNotNull( level.RightButton( false, Pointer + new Vector2( 8, 8 ) ), "8 across and 8 down is still a click" );
		Assert.AreEqual( 0, ParkBuilding.Carrying, "so the hand is empty" );
	}

	/// <summary>
	/// <b>A click is timed and measured from the press</b>, however many frames it is held for: held still for
	/// 250 ms, or dragged 10 units in steps of 5, is not a click. And the 200 ms is 200: 210 ms is not a click,
	/// 190 ms is.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> re-stamping the press on every held frame, or measuring the move from the frame before
	/// rather than from the press, lets the held and the dragged clicks go; a threshold 20 ms either side of 200
	/// fails one of the two timings.
	/// </remarks>
	[TestMethod]
	public void AClickIsTimedAndMeasuredFromThePress()
	{
		var (_, item) = MoveTheBellyBounceIntoTheHand();
		var level = ALevel();

		GameOptions.Current.RmbCancel = true;

		Time.Now = 60f;
		level.RightButton( true, Pointer );

		foreach ( var at in new[] { 60.05f, 60.1f, 60.15f, 60.2f } )
		{
			Time.Now = at;
			level.RightButton( true, Pointer );
		}

		Time.Now = 60.25f;
		Assert.IsNull( level.RightButton( false, Pointer ), "held still for 250 ms over five frames" );

		Time.Now = 70f;
		level.RightButton( true, Pointer );
		level.RightButton( true, Pointer + new Vector2( 5, 0 ) );
		level.RightButton( true, Pointer + new Vector2( 10, 0 ) );
		Time.Now = 70.05f;
		Assert.IsNull( level.RightButton( false, Pointer + new Vector2( 10, 0 ) ), "dragged 10 in steps of 5" );

		Time.Now = 80f;
		level.RightButton( true, Pointer );
		Time.Now = 80.21f;
		Assert.IsNull( level.RightButton( false, Pointer ), "210 ms is not a click" );
		Assert.AreEqual( item, ParkBuilding.Carrying, "and none of those let go" );

		Time.Now = 90f;
		level.RightButton( true, Pointer );
		Time.Now = 90.19f;
		Assert.IsNotNull( level.RightButton( false, Pointer ), "190 ms is a click" );
		Assert.AreEqual( 0, ParkBuilding.Carrying, "which lets go" );
	}

	/// <summary>
	/// <b>A quick right click lets a candidate go back to the pool too</b>, and puts a build tool away: the idle
	/// mode goes in over whatever is current (<c>0x0048842b</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a quick click that answers only a build tool keeps the candidate.
	/// </remarks>
	[TestMethod]
	public void AQuickRightClickPutsACandidateBack()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];
		var waiting = pool.Candidates.ToList();
		var level = ALevel();

		GameOptions.Current.RmbCancel = true;
		ParkStaffPool.Carry( candidate.Id );

		var letGo = QuickRightClick( level );

		StringAssert.Contains( letGo, $"put candidate {candidate.Id} back" );
		Assert.AreEqual( 0, ParkStaffPool.Carrying, "nobody on the cursor" );
		CollectionAssert.AreEqual( waiting, pool.Candidates.ToList(), "and the pool as it was" );
	}

	/// <summary>
	/// <b>Escape over a full hand lets go and opens no menu</b> - the original's game-table row 0 installs the idle
	/// mode over anything but idle and answers 1, which stops the menu (<c>0x0040c35f</c>, <c>0x0040c368</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a handler that puts away only a build tool goes on to open the menu -
	/// which here throws, having no stack to open it on.
	/// </remarks>
	[TestMethod]
	public void EscapeOverAMovedThingLetsGoAndOpensNoMenu()
	{
		var (state, _) = MoveTheBellyBounceIntoTheHand();
		var balance = state.Balance;

		Escape();

		Assert.AreEqual( 0, ParkBuilding.Carrying, "the hand is empty" );
		Assert.AreEqual( balance, state.Balance, "nothing refunded" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and it stays sold" );
	}

	/// <summary><b>Escape puts a carried candidate back</b>, and opens no menu.</summary>
	/// <remarks><b>Mutation:</b> as for the moved thing.</remarks>
	[TestMethod]
	public void EscapeOverACandidatePutsThemBack()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];

		ParkStaffPool.Carry( candidate.Id );

		Escape();

		Assert.AreEqual( 0, ParkStaffPool.Carrying, "nobody on the cursor" );
		Assert.AreEqual( candidate, pool.Find( candidate.Id ), "and they are still in the pool" );
	}

	/// <summary>
	/// <b>Picking a thing up lets a carried candidate go back</b>, and <b>taking a candidate lets go of the moved
	/// thing</b>, which stays sold: the original's setter runs the outgoing mode's uninstall before it installs
	/// the next.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> an item taken without letting go of the candidate, or a candidate taken without letting go
	/// of the item, leaves both hands full.
	/// </remarks>
	[TestMethod]
	public void TakingOneThingIntoTheHandLetsGoOfTheOther()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var (first, second) = (pool.Candidates[0], pool.Candidates[1]);
		var waiting = pool.Candidates.ToList();

		ParkStaffPool.Carry( first.Id );

		var (state, item) = MoveTheBellyBounceIntoTheHand();

		Assert.AreEqual( 0, ParkStaffPool.Carrying, "the pickup let the candidate go" );
		CollectionAssert.AreEqual( waiting, pool.Candidates.ToList(), "back in the pool, in their place" );

		ParkStaffPool.Carry( second.Id );

		Assert.AreEqual( second.Id, ParkStaffPool.Carrying, "the second candidate is on the cursor" );
		Assert.AreEqual( 0, ParkBuilding.Carrying, $"and item {item} was let go of" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and stays sold" );
	}

	/// <summary>
	/// <b>Taking something into the hand puts an armed build tool away</b>, as installing any carrying mode
	/// does.
	/// </summary>
	/// <remarks><b>Mutation:</b> a pickup that leaves the tool armed keeps the path tool here.</remarks>
	[TestMethod]
	public void TakingSomethingIntoTheHandPutsTheToolAway()
	{
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );

		ParkBuildMode.Arm( ParkBuildMode.Path );
		ParkStaffPool.Carry( pool.Candidates[0].Id );

		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "the path tool is put away" );
	}

	/// <summary>
	/// <b>A worker picked up is put back down where they stood</b> by a quick right click, and by Escape: the
	/// place-worker mode's uninstall (<c>0x0046cdc0</c>) puts them in the centre of the cell they are in, with
	/// the drop's own body, and sets them to work again.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a way out that lets go only of items and candidates leaves the worker
	/// held; putting them back without clearing the hand leaves them carried; putting them back without the
	/// put-down's stamp, or stamping before the move, leaves them drawn sliding from where they were picked up.
	/// </remarks>
	[TestMethod]
	public void AWorkerLetGoOfIsPutBackWhereTheyStood()
	{
		var people = APark();
		var level = ALevel();

		GameOptions.Current.RmbCancel = true;

		foreach ( var wayOut in new (string Name, Action Run)[] { ("a quick right click", () => QuickRightClick( level )), ("Escape", Escape) } )
		{
			var member = people.Staff[0];
			var (cellX, cellY) = member.Navigator.Position.Cell;

			Assert.IsTrue( people.PickUp( member.ThingId ), wayOut.Name );
			Assert.AreEqual( StaffActivity.Held, member.Activity, $"held before {wayOut.Name}" );

			wayOut.Run();

			Assert.AreEqual( 0, people.CarriedStaff, $"{wayOut.Name} empties the hand" );
			Assert.AreEqual( StaffActivity.Idle, member.Activity, $"{wayOut.Name} sets them to work" );
			Assert.AreEqual( (cellX, cellY), member.Navigator.Position.Cell, $"{wayOut.Name} leaves them in their cell" );
			Assert.AreEqual( new FixedVector( PeepNavigator.WaypointCentre( cellX ), PeepNavigator.WaypointCentre( cellY ) ),
				member.Navigator.Position, $"{wayOut.Name} puts them in its centre" );
			Assert.AreEqual( member.Navigator.Position, member.Navigator.Previous,
				$"{wayOut.Name} stamps them where they were put, so they are not drawn sliding there" );
		}
	}

	/// <summary>
	/// <b>Picking a worker up lets a carried candidate go back</b>, and <b>taking a candidate puts a carried worker
	/// down</b> - the same setter, the same uninstalls.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a pickup that only puts a build tool away keeps the candidate; a candidate taken without
	/// putting the worker down leaves them held.
	/// </remarks>
	[TestMethod]
	public void AWorkerAndACandidateAreNeverHeldTogether()
	{
		var people = APark();
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var member = people.Staff[0];

		ParkStaffPool.Carry( pool.Candidates[0].Id );
		people.PickUp( member.ThingId );

		Assert.AreEqual( 0, ParkStaffPool.Carrying, "the pickup put the candidate back" );
		Assert.AreEqual( member.ThingId, people.CarriedStaff, "and holds the worker" );

		ParkStaffPool.Carry( pool.Candidates[1].Id );

		Assert.AreEqual( 0, people.CarriedStaff, "the candidate put the worker down" );
		Assert.AreEqual( StaffActivity.Idle, member.Activity, "who is back at work" );
		Assert.AreEqual( pool.Candidates[1].Id, ParkStaffPool.Carrying, "and the candidate is on the cursor" );
	}

	/// <summary>
	/// <b>The ride window's queue button lets go of the hand</b>, since mode <c>0x14</c> is a type-3 shell installed
	/// through the setter (<c>FUN_004af200</c>), and arms the queue tool.
	/// </summary>
	/// <remarks><b>Mutation:</b> arming the queue tool without letting go keeps the candidate on the cursor under it.</remarks>
	[TestMethod]
	public void TheQueueButtonLetsGoOfTheHand()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var current = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;
		var (levelBefore, stateBefore) = (Level.Current, ParkState.Current);

		try
		{
			var level = ALevel();

			typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, new ParkState( world ) );
			Level.Current = level;

			ParkStaffPool.Carry( pool.Candidates[0].Id );

			var reply = ParkPathBuilding.EditQueue( BellyBounceThing );

			Assert.AreEqual( 0, ParkStaffPool.Carrying, $"the candidate went back: {reply}" );
			Assert.AreEqual( ParkBuildMode.Queue, ParkBuildMode.Current, "and the queue tool is armed" );
			Assert.AreEqual( BellyBounceThing, ParkBuildMode.Serves, "for the Belly Bounce" );
		}
		finally
		{
			Level.Current = levelBefore;
			current.SetValue( null, stateBefore );
		}
	}

	/// <summary>
	/// <b>Going into the camcorder lets go of the hand</b>: the camcorder is an interaction mode of its own, and
	/// its button installs it through the setter (<c>FUN_00481a10</c>, <c>0x00481ad0</c>).
	/// </summary>
	/// <remarks><b>Mutation:</b> entering without letting go keeps the moved thing in the hand in first person.</remarks>
	[TestMethod]
	public void GoingIntoTheCamcorderLetsGoOfTheHand()
	{
		var (state, _) = MoveTheBellyBounceIntoTheHand();

		try
		{
			ParkCamcorderCameraMode.Enter();

			Assert.AreEqual( 0, ParkBuilding.Carrying, "the hand is empty" );
			Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and the moved thing stays sold" );
		}
		finally
		{
			ParkCamcorderCameraMode.Forget();
		}
	}

	/// <summary>
	/// <b>Buying lets a carried candidate go back</b>: the buy row installs the carry shell through the setter
	/// (<c>FUN_004ac270</c>), with no sale before it to have let go already.
	/// </summary>
	/// <remarks><b>Mutation:</b> a purchase that only puts a build tool away keeps the candidate on the cursor.</remarks>
	[TestMethod]
	public void BuyingLetsACandidateGo()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];
		var current = typeof( ParkState ).GetProperty( nameof( ParkState.Current ) )!;
		var (levelBefore, stateBefore) = (Level.Current, ParkState.Current);

		try
		{
			var level = ALevel();

			typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, new ParkState( park ) );
			typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, new ParkItemCatalogue( Theme, data ) );
			Level.Current = level;

			ParkStaffPool.Carry( candidate.Id );

			var bought = ParkBuilding.Carry( BellyBounceItem );

			Assert.AreEqual( BellyBounceItem, ParkBuilding.Carrying, $"the item is in the hand: {bought}" );
			Assert.AreEqual( 0, ParkStaffPool.Carrying, "and the candidate was let go of" );
			Assert.AreEqual( candidate, pool.Find( candidate.Id ), "back to the pool" );
		}
		finally
		{
			Level.Current = levelBefore;
			current.SetValue( null, stateBefore );
		}
	}

	/// <summary>
	/// <b>The Delete key lets go of the hand</b>: it installs Clear Land through the setter (<c>FUN_0040c5e0</c>),
	/// and Clear Land itself is not built, so the letting go is all it does.
	/// </summary>
	/// <remarks><b>Mutation:</b> a Delete that only puts a build tool away keeps the moved thing and the candidate.</remarks>
	[TestMethod]
	public void TheDeleteKeyLetsGoOfTheHand()
	{
		var (state, item) = MoveTheBellyBounceIntoTheHand();

		StringAssert.Contains( Level.ClearKey(), $"let go of item {item}" );
		Assert.AreEqual( 0, ParkBuilding.Carrying, "the moved thing is let go of" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "and stays sold" );

		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		ParkStaffPool.Carry( pool.Candidates[0].Id );

		Level.ClearKey();

		Assert.AreEqual( 0, ParkStaffPool.Carrying, "a candidate goes back too" );
	}

	/// <summary>
	/// <b>Selling a thing lets a carried candidate go back, and leaves a carried item in the hand</b>: the demolisher
	/// puts back the tool it was called under (<c>0x0052818d</c>), which with no tool is the idle mode, and with an
	/// item in the hand is that item's tool.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a sale that never lets go keeps the candidate; one that lets go whatever is held drops the
	/// moved thing, which is then a second sale.
	/// </remarks>
	[TestMethod]
	public void SellingLetsACandidateGoAndKeepsAnItem()
	{
		var (state, item) = MoveTheBellyBounceIntoTheHand();
		var others = state.Objects.Where( placed => catalogue.TryGet( placed.CatalogueId, out var what )
			&& what.UiType is >= 0 and <= ItemDescriptionFile.Feature ).Select( placed => placed.ThingId ).ToList();

		ParkBuilding.Sell( state, world, catalogue, null, null, others[0] );

		Assert.AreEqual( item, ParkBuilding.Carrying, $"a sale leaves the moved thing in the hand" );

		ParkHand.LetGo();

		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ) );
		var candidate = pool.Candidates[0];

		ParkStaffPool.Carry( candidate.Id );
		var sold = ParkBuilding.Sell( state, world, catalogue, null, null, others[1] );

		Assert.AreEqual( 0, ParkStaffPool.Carrying, $"a sale lets the candidate go: {sold}" );
		Assert.AreEqual( candidate, pool.Find( candidate.Id ), "back to the pool" );
	}

	/// <summary>The shipped park's people - five staff among them - made current.</summary>
	private ParkPeople APark()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		return new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), null, new ParkState( world ) );
	}

	/// <summary>A press and a release in the same instant, at the same place: the quickest click there is.</summary>
	private static string? QuickRightClick( Level level )
	{
		Time.Now = 50f;
		level.RightButton( true, Pointer );
		Time.Now = 50.05f;

		return level.RightButton( false, Pointer );
	}

	/// <summary>A level made without its constructor - the right button needs nothing a scene builds.</summary>
	private static Level ALevel() => (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

	/// <summary>Escape with no box to type into and no window in front, through the park front end's own handler.</summary>
	private static void Escape()
	{
		var frontEnd = RuntimeHelpers.GetUninitializedObject( typeof( UI.ParkFrontEnd ) );
		var menuKey = typeof( UI.ParkFrontEnd ).GetMethod( "MenuKey", BindingFlags.Instance | BindingFlags.NonPublic )!;

		menuKey.Invoke( frontEnd, [null] );
	}

	/// <summary>
	/// The shipped park with its Belly Bounce picked up by a move onto the Staff Room's anchor, which refuses it:
	/// sold where it stood, and in the hand.
	/// </summary>
	private (ParkState State, int Item) MoveTheBellyBounceIntoTheHand()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		Assert.IsTrue( state.TryObject( BellyBounceThing, out var bounce ) );

		var reply = ParkBuilding.Move( state, world, catalogue, null, rides, BellyBounceThing, 58, 16 );

		Assert.AreEqual( bounce.CatalogueId, ParkBuilding.Carrying, $"in the hand: {reply}" );

		return (state, bounce.CatalogueId);
	}
}
