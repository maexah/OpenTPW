using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using static OpenTPW.Common.GlobalNamespace;

namespace OpenTPW.Tests;

/// <summary>
/// A model record's two tables worked out from the model's file and its clips (<see cref="ParkModelTables"/>),
/// against the records the original wrote.
/// </summary>
[TestClass]
public class ParkModelTablesTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";
	private const int CrazyApe = 1101, JungleSpray = 1303;

	private BaseFileSystem data = null!;
	private ParkWorld shipped = null!;
	private ParkItemCatalogue catalogue = null!;

	[TestInitialize]
	public void ReadTheShippedPark()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		var reader = new SaveReader( stream );

		shipped = new ParkWorld( reader.ReadFile(), reader.Preamble );
		catalogue = new ParkItemCatalogue( Theme, data );
	}

	private int ChannelsFor( int id ) => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1;

	private (ModelFile Model, RideAnimations Clips, ParkItemCatalogue.Item Item) Load( int id )
	{
		Assert.IsTrue( catalogue.TryGet( id, out var item ) );

		using var stream = data.OpenRead( $"{item.Directory}/{item.Stem}.MD2" );

		return (new ModelFile( stream! ), RideAnimations.Load( item.Directory, item.Stem, data, item.AnimationChannels ), item);
	}

	private const float Ended = float.MaxValue;

	/// <summary>
	/// Every record of the shipped park whose item the catalogue holds reads as the rule gives it, the stale-normals
	/// bit apart: its node words from the model and each channel's clip (an idle channel's being its build clip),
	/// its lookup records and their shared flags from the model alone.
	/// </summary>
	[TestMethod]
	public void TheShippedParksRecordsAreTheRule()
	{
		var states = shipped.ThingStates( ChannelsFor );
		var checkedRecords = 0;

		foreach ( var thing in states.Things )
		{
			if ( !catalogue.TryGet( thing.CatalogueId, out _ ) )
				continue;

			var (model, clips, item) = Load( thing.CatalogueId );
			var running = new ParkModelTables.Running( model, thing.Channels.Length );
			var frames = new float[thing.Channels.Length];

			for ( var index = 0; index < frames.Length; ++index )
			{
				var channel = thing.Channels[index];
				var idle = channel.Role == ParkThingStates.NoRole;

				frames[index] = idle ? Ended : (channel.Time - channel.StartTime) * channel.Speed * 0.03f;

				if ( (idle ? clips.Clip( 0, 0 ) : clips.Clip( channel.Role, channel.Entry )) is { } clip )
					running.Started( index, clip, 0f );
			}

			var lookups = states.LookupsOf( thing.Slot )!.Value;

			CollectionAssert.AreEqual( thing.NodeWords!.Select( word => word & ~ParkModelTables.StaleNormals ).ToArray(),
				running.Words( frames ), $"{item.Stem}'s node words, slot {thing.Slot}" );
			CollectionAssert.AreEqual( lookups.Records, ParkModelTables.Lookups( model, item.DoHeadProcessing ), $"{item.Stem}'s lookup records" );

			// An arrival vehicle's model is loaded without the head pass; nothing a player buys is.
			if ( thing.CatalogueId < 1600 )
				Assert.AreEqual( lookups.Shared, ParkModelTables.SharedFlags( model ), $"{item.Stem}'s shared flags" );

			Assert.AreEqual( 0, lookups.Attached );
			++checkedRecords;
		}

		Assert.AreEqual( 14, checkedRecords, "the fourteen placed things" );
	}

	/// <summary>
	/// The Crazy Ape held on the end of its build clip, the words of the original's own record of one just bought
	/// (<c>q253/orig/bought-by-the-original.TPWS</c>, slot 90), the stale-normals bit apart: the boxes and the sign
	/// morphed, the crate and the shards hidden by their visibility tracks, the two arms holding only markers, the
	/// twenty-four markers hidden and placed, and the dummy the arms hang from shown. And on its ride clip after it,
	/// the words of the original's two running apes: the build clip's marks gone, the crate and shards hidden by
	/// the ride clip's list, and the dummy still shown, as the build clip's track left it.
	/// </summary>
	[TestMethod]
	public void ABoughtApeReadsAsTheOriginalsOnItsBuildClipAndOnItsRide()
	{
		var (model, clips, item) = Load( CrazyApe );
		var running = new ParkModelTables.Running( model, 1 );

		running.Started( 0, clips.Clip( 0, 0 )!, Ended );

		uint[] built = [0x0, 0xa0, 0xa0, 0x20, 0xa0, 0xa2, 0xa2, 0xa1, 0xa1, .. Enumerable.Repeat( 0x601u, 24 ), 0x420];

		CollectionAssert.AreEqual( built, running.Words( [Ended] ) );
		Assert.AreEqual( 34, running.Count );

		running.Started( 0, clips.Clip( 3, 0 )!, 215f );

		uint[] riding = [0, 0, 0, 0, 0xa0, 0xa2, 0xa2, 1, 1, .. Enumerable.Repeat( 0x601u, 24 ), 0xc20];

		CollectionAssert.AreEqual( riding, running.Words( [12f] ) );

		var lookups = ParkModelTables.Lookups( model, item.DoHeadProcessing );

		Assert.AreEqual( 24, lookups.Length );
		Assert.AreEqual( 17, lookups.Count( lookup => lookup == (0x21, -1) ), "the seventeen heads: placed, childless" );
		Assert.AreEqual( 7, lookups.Count( lookup => lookup == (0x29, -1) ), "the noses and the destroy marker: posed all the same" );
		Assert.AreEqual( ParkModelTables.SharedHasPosition | ParkModelTables.SharedHeadsHidden, ParkModelTables.SharedFlags( model ) );
	}

	/// <summary>A clip taken off leaves its nodes unmarked and shown, a transform-only node's hidden bit apart.</summary>
	[TestMethod]
	public void UnbindingAClipTakesItsMarksOff()
	{
		var (model, clips, _) = Load( CrazyApe );
		var ride = clips.Clip( 3, 0 )!;
		var words = ParkModelTables.NodeWordsAtRest( model );

		ParkModelTables.Bind( words, ride, hide: true );
		words[4] |= ParkModelTables.Hidden | ParkModelTables.StaleNormals;
		words[1] = ParkModelTables.Tracked;
		ParkModelTables.Unbind( words, ride );

		Assert.AreEqual( (0x8u, 2u, 2u, 0u, 0u), (words[4], words[5], words[6], words[7], words[8]), "tracks and list unmarked and shown; what no clip decides kept" );
		Assert.AreEqual( (0x401u, 0x601u, ParkModelTables.Tracked), (words[33], words[9], words[1]), "a transform-only node stays hidden; another clip's mark stays" );
	}

	/// <summary>At rest: a transform-only node is marked and hidden, a lookup record with a position marks its node, and a node whose children are all childless markers is marked.</summary>
	[TestMethod]
	public void AModelAtRestHoldsWhatItsFileSays()
	{
		var (model, _, _) = Load( CrazyApe );
		var words = ParkModelTables.NodeWordsAtRest( model );

		Assert.AreEqual( 34, words.Length );
		CollectionAssert.AreEqual( new uint[] { 0, 0, 0, 0, 0, 2, 2, 0, 0 }, words[..9], "the nine meshes: the two arms hold only markers; the body holds the dummy too" );
		Assert.IsTrue( words[9..33].All( word => word == 0x601 ) );
		Assert.AreEqual( 0x401u, words[33], "the dummy: no lookup record, so not placed; hidden until a clip shows it" );
	}

	/// <summary>A clip's tracks mark their nodes by what they carry, its hide list hides a mesh and spares a transform-only node, and the caller's keep-shown leaves the list unapplied.</summary>
	[TestMethod]
	public void BindingAClipMarksItsNodes()
	{
		var (model, clips, _) = Load( CrazyApe );
		var ride = clips.Clip( 3, 0 )!;

		Assert.AreEqual( 4, ride.Tracks.Count );
		CollectionAssert.AreEqual( Enumerable.Range( 7, 26 ).Select( node => (ushort)node ).ToArray(), ride.HideList );

		var words = new uint[model.Nodes.Count];

		words[9] = ParkModelTables.TransformOnly;
		ParkModelTables.Bind( words, ride, hide: true );

		Assert.AreEqual( (0xa0u, 0xa0u, 0xa0u, 0x820u), (words[4], words[5], words[6], words[33]), "three morphs and a position" );
		Assert.AreEqual( (1u, 1u, 0x400u), (words[7], words[8], words[9]), "the crate and shards hidden, a transform-only node spared" );
		Assert.AreEqual( 0u, words[1] );

		var shown = new uint[model.Nodes.Count];

		ParkModelTables.Bind( shown, ride, hide: false );
		Assert.AreEqual( (0u, 0xa0u), (shown[7], shown[4]) );
	}

	/// <summary>A visibility track hides and shows its node by the frame, a transform-only node too, and leaves it alone before its first entry.</summary>
	[TestMethod]
	public void AVisibilityTrackFollowsTheFrame()
	{
		var (model, clips, _) = Load( CrazyApe );
		var build = clips.Clip( 0, 0 )!;
		var crate = build.VisibilityTracks.Single( track => track.TargetIndex == 7 );
		var hides = crate.Entries.First( entry => entry <= 0 );

		var words = new uint[model.Nodes.Count];

		ParkModelTables.Show( words, build, Ended );
		Assert.AreEqual( ParkModelTables.Hidden, words[7] & ParkModelTables.Hidden, "hidden as the clip ends" );

		words[33] = ParkModelTables.TransformOnly | ParkModelTables.Hidden;
		ParkModelTables.Show( words, build, Ended );
		Assert.AreEqual( ParkModelTables.TransformOnly, words[33], "the dummy shown, though transform-only" );

		var before = new uint[model.Nodes.Count];

		before[7] = ParkModelTables.Hidden;
		ParkModelTables.Show( before, build, -hides - 0.5f );
		Assert.AreEqual( crate.VisibleAt( -hides - 0.5f ) == false ? 1u : 0u, before[7] & 1 );
	}

	/// <summary>A kept model with nothing started keeps the file's words, the stale-normals bit among them, a running clip's visibility tracks laid over them.</summary>
	[TestMethod]
	public void AKeptModelLeftAloneKeepsTheFilesWords()
	{
		var (model, clips, _) = Load( CrazyApe );
		var build = clips.Clip( 0, 0 )!;
		var file = ParkModelTables.NodeWordsAtRest( model );

		ParkModelTables.Bind( file, build, hide: true );
		file[4] |= ParkModelTables.StaleNormals;

		var kept = new ParkModelTables.Running( file, [build] );

		CollectionAssert.AreEqual( file, kept.Words( [-1f] ), "before any entry of a visibility track" );

		var ended = kept.Words( [Ended] );

		Assert.AreEqual( (0xa1u, 0xa1u, 0x420u, 0xa8u), (ended[7], ended[8], ended[33], ended[4]), "the crate and shards hidden and the dummy shown, as the clip ends" );
		CollectionAssert.AreEqual( file, kept.Words( [-1f] ), "and the kept words are not changed by the reading" );
	}

	/// <summary>A clip started on a kept model's one channel takes the file's clip's marks off and puts its own on; what no channel decides stays.</summary>
	[TestMethod]
	public void AClipStartedOnAKeptModelReplacesTheFilesMarks()
	{
		var (model, clips, _) = Load( CrazyApe );
		var build = clips.Clip( 0, 0 )!;
		var ride = clips.Clip( 3, 0 )!;
		var fresh = new ParkModelTables.Running( model, 1 );

		fresh.Started( 0, build, 0f );

		var file = fresh.Words( [Ended] );

		file[1] |= ParkModelTables.StaleNormals;

		var kept = new ParkModelTables.Running( file, [build] );

		kept.Started( 0, ride, Ended );

		uint[] riding = [0, 0x8, 0, 0, 0xa0, 0xa2, 0xa2, 1, 1, .. Enumerable.Repeat( 0x601u, 24 ), 0xc20];

		CollectionAssert.AreEqual( riding, kept.Words( [5f] ) );
	}

	/// <summary>
	/// An idle channel's marks are its last clip's, which the file does not name: they stand while nothing is started
	/// on it, and go when something is, a transform-only node's hidden bit apart.
	/// </summary>
	[TestMethod]
	public void AnIdleChannelsMarksStandUntilItIsStarted()
	{
		var (model, clips, _) = Load( CrazyApe );
		var fresh = new ParkModelTables.Running( model, 1 );

		fresh.Started( 0, clips.Clip( 0, 0 )!, 0f );

		var file = fresh.Words( [Ended] );
		var kept = new ParkModelTables.Running( file, [null] );

		CollectionAssert.AreEqual( file, kept.Words( [Ended] ) );

		kept.Started( 0, clips.Clip( 3, 0 )!, Ended );

		uint[] riding = [0, 0, 0, 0, 0xa0, 0xa2, 0xa2, 1, 1, .. Enumerable.Repeat( 0x601u, 24 ), 0xc20];

		CollectionAssert.AreEqual( riding, kept.Words( [5f] ), "the build clip's marks, which no clip of the file's accounts for, gone; the dummy shown as the file has it" );

		// And a mesh the unnamed clip left hidden is shown again: the build clip, started afresh, has not yet hidden the crate.
		var again = new ParkModelTables.Running( file, [null] );

		again.Started( 0, clips.Clip( 0, 0 )!, Ended );
		Assert.AreEqual( (0xa1u, 0xa0u), (file[7], again.Words( [-1f] )[7]) );
	}

	/// <summary>Of a model's several channels, a clip started on one leaves the marks the others' clips account for, whether its own last clip is named or not.</summary>
	[TestMethod]
	public void AClipStartedOnOneLaneLeavesTheOthers()
	{
		var (model, clips, _) = Load( JungleSpray );
		var first = clips.Clip( 5, 0 )!;
		var second = clips.Clip( 5, 1 )!;
		var onlyFirst = first.Tracks.Select( track => track.Node ).Except( second.Tracks.Select( track => track.Node ) ).ToArray();
		var onlySecond = second.Tracks.Select( track => track.Node ).Except( first.Tracks.Select( track => track.Node ) ).ToArray();

		Assert.IsTrue( onlyFirst.Length > 0 && onlySecond.Length > 0, "the two lanes move different nodes" );

		var fresh = new ParkModelTables.Running( model, 3 );

		fresh.Started( 0, first, 0f );
		fresh.Started( 1, second, 0f );

		var file = fresh.Words( [3f, 3f, Ended] );

		// The second lane starts the idle clip of role 2, which has no track; the first is left alone.
		foreach ( var named in new[] { true, false } )
		{
			var kept = new ParkModelTables.Running( file, [first, named ? second : null, null] );

			kept.Started( 1, clips.Clip( 2, 0 )!, 3f );

			var words = kept.Words( [9f, 1f, Ended] );

			Assert.IsTrue( onlyFirst.All( node => (words[node] & ParkModelTables.Tracked) != 0 ), $"named {named}" );
			Assert.IsTrue( onlySecond.All( node => (words[node] & ParkModelTables.TrackBits) == 0 ), $"named {named}" );
		}

		// Two lanes on one clip: the one taken off leaves the other's marks standing.
		var shared = new ParkModelTables.Running( model, 3 );

		shared.Started( 0, first, 0f );
		shared.Started( 1, first, 0f );
		shared.Started( 1, clips.Clip( 2, 0 )!, 3f );
		Assert.IsTrue( first.Tracks.All( track => (shared.Words( [3f, 3f, Ended] )[track.Node] & ParkModelTables.Tracked) != 0 ) );

		// A channel past the model's own is nobody's.
		var before = fresh.Words( [3f, 3f, Ended] );

		fresh.Started( 3, second, 0f );
		fresh.Started( -1, second, 0f );
		CollectionAssert.AreEqual( before, fresh.Words( [3f, 3f, Ended] ) );
	}

	/// <summary>A clip's raw tracks and hide list are read whether or not this reader reads a channel of them.</summary>
	[TestMethod]
	public void AClipGivesItsTracksAndItsHideList()
	{
		var (_, clips, _) = Load( CrazyApe );
		var build = clips.Clip( 0, 0 )!;

		Assert.AreEqual( 9, build.Tracks.Count );
		Assert.IsTrue( build.Tracks.Contains( (3, 0x20000u) ), "the second sign: a visibility track alone" );
		CollectionAssert.AreEqual( Enumerable.Range( 9, 24 ).Select( node => (ushort)node ).ToArray(), build.HideList );

		var (_, spray, _) = Load( JungleSpray );

		Assert.AreEqual( (0, 0), (spray.Clip( 2, 0 )!.Tracks.Count, spray.Clip( 2, 0 )!.HideList.Length), "a clip with no track and no list" );
	}

	/// <summary>An item that sets DoHeadProcessing has every lookup record posed whatever its flags, as the played parks' TV simulators and Aztec Mayhems read.</summary>
	[TestMethod]
	public void DoHeadProcessingPosesEveryLookupRecord()
	{
		var (model, _, item) = Load( 1104 );

		Assert.IsTrue( item.DoHeadProcessing );

		var posed = ParkModelTables.Lookups( model, true );
		var plain = ParkModelTables.Lookups( model, false );

		Assert.IsTrue( posed.All( lookup => (lookup.Flags & 0x8) != 0 ) );
		Assert.IsTrue( plain.Any( lookup => (lookup.Flags & 0x8) == 0 ) );
		Assert.IsTrue( posed.Zip( plain ).All( pair => (pair.First.Flags & ~0x8) == (pair.Second.Flags & ~0x8) && pair.First.Handle == pair.Second.Handle ) );
	}

	/// <summary>A model's lookup range is its header's.</summary>
	[TestMethod]
	public void AModelGivesItsLookupRange()
	{
		var (model, _, _) = Load( CrazyApe );

		Assert.AreEqual( (9, 24, 34), (model.LookupFirst, model.LookupCount, model.Nodes.Count) );
	}

	/// <summary>
	/// A model's players tell its node words of each clip bound: a start binds, a start with the caller's keep-shown
	/// does not, a loop's wrap does not, a clip queued is bound as it is promoted, and a pseudo-role is no clip.
	/// </summary>
	[TestMethod]
	public void ThePlayersTellTheNodeWordsOfEachClipBound()
	{
		var (model, clips, _) = Load( CrazyApe );

		clips.Nodes = new ParkModelTables.Running( model, clips.ChannelCount );

		uint[] rest = [.. ParkModelTables.NodeWordsAtRest( model )];
		uint[] built = [0x0, 0xa0, 0xa0, 0x20, 0xa0, 0xa2, 0xa2, 0xa1, 0xa1, .. Enumerable.Repeat( 0x601u, 24 ), 0x420];
		uint[] riding = [0, 0, 0, 0, 0xa0, 0xa2, 0xa2, 1, 1, .. Enumerable.Repeat( 0x601u, 24 ), 0xc20];

		clips.Trigger( 0, 0, AnimTimeControl.StartAtOnceFlag | AnimTimeControl.KeepShownFlag, 1f, 0 );
		CollectionAssert.AreEqual( rest, clips.Nodes.Words( [Ended] ), "started without being bound" );

		clips.Trigger( 0, 0, AnimTimeControl.StartAtOnceFlag, 1f, 0 );
		CollectionAssert.AreEqual( built, clips.Nodes.Words( [Ended] ) );
		CollectionAssert.AreEqual( new[] { 0f }, clips.NodeFrames() );

		clips.Trigger( AnimTimeControl.HoldAtEnd, 0, 0, 1f, 0 );
		CollectionAssert.AreEqual( built, clips.Nodes.Words( clips.NodeFrames() ), "held on its last frame, and no clip started" );

		// Started over the held build clip, whose visibility tracks are laid down as they stood at its end: the dummy shown.
		clips.Trigger( 3, 0, AnimTimeControl.StartAtOnceFlag, 1f, 0 );
		CollectionAssert.AreEqual( riding, clips.Nodes.Words( clips.NodeFrames() ) );

		// Queued behind a running clip, bound as the advance promotes it.
		clips.Trigger( 0, 0, AnimTimeControl.StartAtOnceFlag, 1f, 0 );
		clips.Trigger( 3, 0, AnimTimeControl.LoopFlag, 1f, 10 );
		CollectionAssert.AreEqual( built, clips.Nodes.Words( [Ended] ), "queued, not started" );

		clips.Advance( clips.DurationMilliseconds( 0, 0 ) + 40 );
		Assert.AreEqual( 3, clips.Channel( 0 )!.AnimID );
		CollectionAssert.AreEqual( riding, clips.Nodes.Words( clips.NodeFrames() ) );

		// The loop's wrap starts the clip again and binds nothing: the words stand.
		clips.Advance( clips.DurationMilliseconds( 0, 0 ) + clips.DurationMilliseconds( 3, 0 ) + 80 );
		CollectionAssert.AreEqual( riding, clips.Nodes.Words( clips.NodeFrames() ) );

		var idle = RideAnimations.None();

		CollectionAssert.AreEqual( new[] { float.MaxValue }, idle.NodeFrames(), "an idle channel reads past any clip's end" );
	}
}
