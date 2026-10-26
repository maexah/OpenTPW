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
	/// promotion puts back and hides as a trigger's start does; a loop's restart binds with <c>0x8</c> here (Q661).
	/// </summary>
	/// <remarks><b>Mutation:</b> the promotion's start given <c>0x8</c>, as before Q658 (bound <c>(2, 0x8)</c>).</remarks>
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
		Assert.AreEqual( new RideAnimations.Bind( 4, AnimTimeControl.LoopFlag | AnimTimeControl.KeepShownFlag ), players.BoundOn( 0 ), "looped" );
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
