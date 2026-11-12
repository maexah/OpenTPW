using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a clip's start does to the drawn model (<see cref="RideAnimations.PoseChannel"/>), as the engine's start does it
/// (<c>FUN_00472f60</c>): the outgoing clip put back unless the caller passed <c>0x4</c> or <c>0x8</c>, then the incoming
/// clip's hide list hidden unless it passed <c>0x8</c>, on an idle channel too (<c>docs/exe/park.md</c>, "The per-clip hide
/// list"). The first two need no game; the rest read the Fries Shop and are skipped without it (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ClipBindTests
{
	/// <summary>Every call a pose makes, in order, each clip by its name here.</summary>
	private sealed class Calls( Dictionary<AnimationFile, string> names ) : IChannelPosed
	{
		public readonly List<string> Made = [];

		public void Rest( AnimationFile outgoing, AnimationFile? incoming ) => Made.Add( $"rest {names[outgoing]} {(incoming is null ? "-" : names[incoming])}" );

		public void Hide( AnimationFile incoming ) => Made.Add( $"hide {names[incoming]}" );

		public void Pose( AnimationFile clip, float frame ) => Made.Add( $"pose {names[clip]}" );

		public string[] Take()
		{
			var made = Made.ToArray();
			Made.Clear();
			return made;
		}
	}

	private static AnimationFile Blank() => (AnimationFile)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( AnimationFile ) );

	/// <summary>
	/// A first start on a channel hides the clip's hide list and puts nothing back, there being no clip before it
	/// (<c>0x00473117</c>; the restore wants an outgoing role other than 12).
	/// </summary>
	/// <remarks><b>Mutation:</b> the hide made only at a change of clip, as before Q658 (no <c>hide A</c>).</remarks>
	[TestMethod]
	public void AStartOnAnIdleChannelHidesItsHideList()
	{
		var a = Blank();
		var calls = new Calls( new() { [a] = "A" } );
		RideAnimations.Posed posed = default;

		RideAnimations.PoseChannel( calls, ref posed, a, 0, new( 1, 0 ) );

		CollectionAssert.AreEqual( new[] { "hide A", "pose A" }, calls.Take() );
	}

	/// <summary>
	/// The start's own flags decide: <c>0x4</c> puts nothing back and still hides; <c>0x8</c> does neither; a start of the
	/// same clip again, unflagged, puts it back over itself and hides its list again; <c>0x1</c> stops neither; a pose with
	/// no new start does nothing but pose.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>0x4</c> left out of the restore's test (a <c>rest</c> at the 0x4 start); the hide's test widened
	/// to <c>0xc</c> (no <c>hide B</c> there); the start count not compared (no <c>rest A A</c>).
	/// </remarks>
	[TestMethod]
	public void TheStartsFlagsSayWhatIsPutBackAndHidden()
	{
		var a = Blank();
		var b = Blank();
		var calls = new Calls( new() { [a] = "A", [b] = "B" } );
		RideAnimations.Posed posed = default;

		RideAnimations.PoseChannel( calls, ref posed, a, 0, new( 1, 0 ) );
		calls.Take();

		RideAnimations.PoseChannel( calls, ref posed, b, 0, new( 2, AnimTimeControl.KeepPoseFlag ) );
		CollectionAssert.AreEqual( new[] { "hide B", "pose B" }, calls.Take(), "0x4" );

		RideAnimations.PoseChannel( calls, ref posed, a, 0, new( 3, AnimTimeControl.KeepShownFlag ) );
		CollectionAssert.AreEqual( new[] { "pose A" }, calls.Take(), "0x8" );

		RideAnimations.PoseChannel( calls, ref posed, a, 0, new( 4, 0 ) );
		CollectionAssert.AreEqual( new[] { "rest A A", "hide A", "pose A" }, calls.Take(), "the same clip started again" );

		RideAnimations.PoseChannel( calls, ref posed, a, 5, new( 4, 0 ) );
		CollectionAssert.AreEqual( new[] { "pose A" }, calls.Take(), "no new start" );

		RideAnimations.PoseChannel( calls, ref posed, b, 0, new( 5, AnimTimeControl.LoopFlag ) );
		CollectionAssert.AreEqual( new[] { "rest A B", "hide B", "pose B" }, calls.Take(), "0x1" );
	}

	private const string Fries = "levels/jungle/shops/fries";

	/// <summary>The Fries Shop's players and which of its nodes are transform-only.</summary>
	private static (RideAnimations Players, ModelFile Model) LoadFries()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( $"{Fries}/fries.md2" ) );

		return (RideAnimations.Load( Fries, "fries", data ), new ModelFile( stream ));
	}

	/// <summary>Which nodes are hidden, as the drawn model's restore and bind leave them.</summary>
	private sealed class Shown( ModelFile model ) : IChannelPosed
	{
		public readonly SortedSet<int> Hidden = [];
		public int Rests;

		public void Rest( AnimationFile outgoing, AnimationFile? incoming )
		{
			++Rests;

			foreach ( var (node, _) in LobbyModel.ShownAtRest( outgoing, node => LobbyModel.IsTransformOnly( model, node ) ) )
				Hidden.Remove( node );
		}

		public void Hide( AnimationFile incoming )
		{
			foreach ( var (node, _) in LobbyModel.HiddenAtBind( incoming, node => LobbyModel.IsTransformOnly( model, node ) ) )
				Hidden.Add( node );
		}

		public void Pose( AnimationFile clip, float frame ) { }
	}

	/// <summary>
	/// A bought Fries Shop stands on its construction clip, the only clip it ships, and that start on an idle channel puts
	/// its mesh 10, <c>chip08</c>, away; its script's own play of the clip puts it back and away again, and the hold at the
	/// clip's end leaves it away, as nothing of the shop starts another clip.
	/// </summary>
	/// <remarks><b>Mutation:</b> the hide made only at a change of clip (nothing hidden after the build stands).</remarks>
	[TestMethod]
	public void ABoughtFriesShopPutsItsChip08Away()
	{
		var (players, model) = LoadFries();
		var shown = new Shown( model );
		var posed = new RideAnimations.Posed[players.ChannelCount];

		Assert.AreEqual( 1, players.Loaded, "the construction clip alone" );
		Assert.AreEqual( "chip08", model.Nodes[10].Name );
		Assert.IsTrue( 10 < model.Meshes.Count, "node 10 is a mesh" );

		Assert.IsTrue( players.StandOnBuildClip( 0 ) );
		players.PoseOnto( shown, posed );
		CollectionAssert.AreEqual( new[] { 10 }, shown.Hidden.ToArray(), "stood on its build" );
		Assert.AreEqual( 0, shown.Rests );

		players.Trigger( 0, 0, 0, 1f, 100 );
		players.Advance( 100 );
		players.PoseOnto( shown, posed );
		CollectionAssert.AreEqual( new[] { 10 }, shown.Hidden.ToArray(), "played by its script" );
		Assert.AreEqual( 1, shown.Rests, "the same clip again is put back over itself" );

		players.Advance( 100 + 3000 );
		players.PoseOnto( shown, posed );
		CollectionAssert.AreEqual( new[] { 10 }, shown.Hidden.ToArray(), "held at its end" );
		Assert.AreEqual( 1, shown.Rests );
	}

	/// <summary>
	/// A queued clip is bound by the flags it was queued with (<c>0x00473874</c>, in view), not with <c>0x8</c>, so its
	/// promotion puts back and hides as a trigger's start does; a loop's restart binds with <c>0x1</c> alone, the start
	/// the engine makes there in view (<c>0x00473840</c>..<c>0x00473848</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the promotion's start given <c>0x8</c>, as before Q658 (bound <c>(2, 0x8)</c>); the restart given
	/// <c>0x8</c>, as before Q661 (bound <c>(4, 0x9)</c>).
	/// </remarks>
	[TestMethod]
	public void AQueuedClipIsBoundByTheFlagsItWasQueuedWith()
	{
		var (players, _) = LoadFries();

		Assert.AreEqual( default, players.BoundOn( 0 ) );

		players.Trigger( 0, 0, 0, 1f, 0 );
		players.Trigger( 0, 0, 0, 1f, 10 );
		Assert.AreEqual( new RideAnimations.Bind( 1, 0 ), players.BoundOn( 0 ), "the second is queued" );

		players.Advance( 2000 );
		Assert.AreEqual( new RideAnimations.Bind( 2, 0 ), players.BoundOn( 0 ), "promoted" );

		players.Trigger( 0, 0, AnimTimeControl.LoopFlag | AnimTimeControl.StartAtOnceFlag, 1f, 2000 );
		Assert.AreEqual( new RideAnimations.Bind( 3, AnimTimeControl.LoopFlag | AnimTimeControl.StartAtOnceFlag ), players.BoundOn( 0 ), "started at once" );

		players.Advance( 4000 );
		Assert.AreEqual( new RideAnimations.Bind( 4, AnimTimeControl.LoopFlag ), players.BoundOn( 0 ), "looped" );
	}

	/// <summary>
	/// A loop's restart is a start of the same clip, so the drawn side puts it back over itself and hides its list again
	/// at every restart, as at a trigger's start of it (<c>FUN_00472f60</c> with <c>0x1</c>; <see cref="RideAnimations.PoseChannel"/>).
	/// </summary>
	/// <remarks><b>Mutation:</b> the restart given <c>0x8</c>, as before Q661 (the third pose only <c>pose C</c>).</remarks>
	[TestMethod]
	public void ALoopsRestartPutsBackAndHidesAsAStartDoes()
	{
		var (players, _) = LoadFries();
		var clip = players.Clip( 0, 0 )!;
		var calls = new Calls( new() { [clip] = "C" } );
		var posed = new RideAnimations.Posed[players.ChannelCount];
		var length = players.DurationMilliseconds( 0, 0 );

		players.Trigger( 0, 0, AnimTimeControl.LoopFlag, 1f, 0 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "hide C", "pose C" }, calls.Take(), "the first start" );

		players.Advance( length / 2 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "pose C" }, calls.Take(), "half way through" );

		players.Advance( length + 50 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "rest C C", "hide C", "pose C" }, calls.Take(), "restarted" );

		players.Advance( length + 100 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "pose C" }, calls.Take(), "and running on" );
	}

	/// <summary>Which nodes are hidden as the drawn model leaves them: the restore and bind as <see cref="Shown"/>, and a
	/// pose's visibility keys as <see cref="LobbyModel.Pose"/> writes them, nothing before a track's first key.</summary>
	private sealed class Drawn( ModelFile model ) : IChannelPosed
	{
		private readonly Shown _shown = new( model );

		public SortedSet<int> Hidden => _shown.Hidden;

		public void Rest( AnimationFile outgoing, AnimationFile? incoming ) => _shown.Rest( outgoing, incoming );

		public void Hide( AnimationFile incoming ) => _shown.Hide( incoming );

		public void Pose( AnimationFile clip, float frame )
		{
			foreach ( var track in clip.VisibilityTracks )
			{
				if ( track.VisibleAt( frame ) is not bool visible )
					continue;

				if ( visible )
					Hidden.Remove( track.TargetIndex );
				else
					Hidden.Add( track.TargetIndex );
			}
		}
	}

	/// <summary>
	/// The seaplane's take-off (role 5 entry 2, <c>seaplanem3</c>) hides its five meshes from frame 396 of 400 and has no
	/// visibility key before that, so looped, its restart shows them again (the restore clears <c>0x10</c> on every node
	/// the clip tracks, <c>0x00472486</c>) and they stay shown until frame 396 comes round. Nothing in Lost Kingdom loops
	/// this clip (its script plays it once a visit); it is the shipped clip that tells the restart's flags apart.
	/// </summary>
	/// <remarks><b>Mutation:</b> the restart given <c>0x8</c>, as before Q661 (the five stay hidden after the restart).</remarks>
	[TestMethod]
	public void ALoopedTakeOffShowsItsSeaplaneAgainAtTheRestart()
	{
		var data = GameData.Required();
		const string at = "levels/jungle/features/seaplane";
		var players = RideAnimations.Load( at, "seaplane", data );
		using var stream = new MemoryStream( data.ReadAllBytes( $"{at}/seaplane.md2" ) );
		var drawn = new Drawn( new ModelFile( stream ) );
		var posed = new RideAnimations.Posed[players.ChannelCount];
		var takeOff = players.Clip( 5, 2 )!;
		int[] five = [0, 1, 2, 3, 4];

		Assert.IsNotNull( takeOff );
		CollectionAssert.AreEquivalent( five, takeOff.VisibilityTracks.Where( track => track.VisibleAt( 0 ) is null && track.VisibleAt( takeOff.LastFrame ) == false )
			.Select( track => track.TargetIndex ).ToArray(), "five meshes hidden at the end, keyed after frame 0" );

		var length = players.DurationMilliseconds( 5, 2 );

		players.Trigger( 5, 2, AnimTimeControl.LoopFlag, 1f, 0 );
		players.Advance( length * 399 / 400 );
		players.PoseOnto( drawn, posed );
		CollectionAssert.IsSubsetOf( five, drawn.Hidden.ToArray(), "hidden near the end" );

		players.Advance( length + length / 4 );
		players.PoseOnto( drawn, posed );
		Assert.AreEqual( 5, players.Channel( 0 )!.AnimID, "still the take-off, a quarter into its second run" );
		CollectionAssert.AreEqual( new int[0], drawn.Hidden.Intersect( five ).ToArray(), "shown again by the restart" );
	}

	/// <summary>
	/// Every channel is posed by its own start (<see cref="RideAnimations.PoseOnto"/>): the Jungle Spray's three lanes, each
	/// started with other flags, hide and put back by their own and not by channel 0's, and a second start on one lane
	/// is seen by that lane alone.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>BoundOn( 0 )</c> for every channel in <c>PoseOnto</c> (lane 1 would hide <c>m2</c>, and its restart would go unseen).</remarks>
	[TestMethod]
	public void EachLanePosesByItsOwnStart()
	{
		var data = GameData.Required();
		var players = RideAnimations.Load( "levels/jungle/sideshow/junspray", "junspray", data, 3 );
		var first = players.Clip( 5, 0 )!;
		var second = players.Clip( 5, 1 )!;

		Assert.AreEqual( 3, players.ChannelCount );
		Assert.IsNotNull( first, "the Spray's first clip" );
		Assert.IsNotNull( second, "the Spray's second clip" );

		var calls = new Calls( new() { [first] = "m1", [second] = "m2" } );
		var posed = new RideAnimations.Posed[3];

		players.Trigger( 5, 0, 0, 1f, 0, 0 );
		players.Trigger( 5, 1, AnimTimeControl.KeepShownFlag, 1f, 0, 1 );
		players.Trigger( 5, 0, AnimTimeControl.KeepPoseFlag, 1f, 0, 2 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "hide m1", "pose m1", "pose m2", "hide m1", "pose m1" }, calls.Take(), "three lanes, three starts" );

		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "pose m1", "pose m2", "pose m1" }, calls.Take(), "no new start" );

		players.Trigger( 5, 0, AnimTimeControl.StartAtOnceFlag, 1f, 0, 1 );
		players.PoseOnto( calls, posed );
		CollectionAssert.AreEqual( new[] { "pose m1", "rest m2 m1", "hide m1", "pose m1", "pose m1" }, calls.Take(), "lane 1 started again" );
	}
}
