using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <b>A bought thing stands on its construction clip's first frame from the moment it is built</b> (Q623): the build
/// path's two triggers, role nought and then the freeze (<c>FUN_00463060</c>, <c>0x00463205</c> and <c>0x0046321f</c>),
/// so the Round Fountain is drawn as its bare floor rather than finished until its script's first tick plays the clip. Needs the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkBuildClipTests
{
	/// <summary>The jungle Round Fountain, whose <c>fountainc.md2</c> hides meshes 1 to 4 on its frame nought.</summary>
	private const int Fountain = 1403;

	/// <summary>The jungle bus, which ships no role nought clip.</summary>
	private const int Bus = 1600;

	private static (ParkItemCatalogue Catalogue, ParkRides Rides) Park()
	{
		var data = GameData.Required();
		FileSystem = data;
		Log ??= new();
		var world = new ParkWorld( new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", data );

		return (catalogue, new ParkRides( "jungle", world, catalogue, data ));
	}

	/// <summary>What a pose asks of a model, in order.</summary>
	private sealed class Posed : IChannelPosed
	{
		public readonly List<(AnimationFile Clip, float Frame)> Poses = [];

		public void Rest( AnimationFile outgoing, AnimationFile? incoming ) { }

		public void Pose( AnimationFile clip, float frame ) => Poses.Add( (clip, frame) );
	}

	/// <summary>
	/// The fountain bound as a purchase: channel 0 is frozen on role nought entry nought at frame nought before its
	/// script has run a word, and stays there however long the script waits; posed, it is the construction clip at frame
	/// nought, which hides the fountain, the fall, the water and the wall and leaves the floor. The script's own trigger
	/// then plays the clip.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the call in <c>BindNew</c> taken out (idle: posed as nothing); the freeze not triggered (the
	/// frame moves on the advance); role nought not triggered (the freeze holds nothing: idle); the role test inverted.
	/// The drawn model, posed in the frame a click builds it (<c>ParkObjects.PoseNow</c>), is
	/// <see cref="TheDrawnFountainIsPosedInTheFrameItIsBuilt"/>.
	/// </remarks>
	[TestMethod]
	public void ABoughtFountainStandsOnItsBuildClipsFirstFrame()
	{
		var (catalogue, rides) = Park();
		var placed = new ParkWorld.CatalogueObject( 43, Fountain, 47 << 8, 33 << 8, 0 );
		Assert.IsTrue( catalogue.TryGet( Fountain, out var item ) );

		Assert.IsTrue( rides.BindNew( placed, item ) );

		var script = rides.Scheduler.Find( rides.ScriptFor( 43 ) )!;
		Assert.AreEqual( 0, script.Position, "nothing of the script has run" );

		var animations = script.Animations!;
		var channel = animations.Channel( 0 )!;
		Assert.AreEqual( (0, 0, 0f, 0x2), (channel.AnimID, channel.SubAnim, channel.AnimFrame, channel.Flags & 0x6), "role 0 entry 0, frozen" );

		var posed = new Posed();
		animations.PoseOnto( posed, new RideAnimations.Posed[animations.ChannelCount] );
		Assert.AreEqual( 1, posed.Poses.Count );
		var (clip, frame) = posed.Poses[0];
		Assert.AreSame( animations.Clip( 0, 0 ), clip, "fountainc" );
		Assert.AreEqual( 0f, frame );
		CollectionAssert.AreEqual( new[] { (1, false), (2, false), (3, false), (4, false) },
			clip.VisibilityTracks.Select( track => (track.TargetIndex, track.VisibleAt( frame ) ?? true) ).OrderBy( t => t.TargetIndex ).ToArray(),
			"wf_fountain, wf_fall, wf_water and wf_wall hidden on frame nought: the floor alone" );

		var now = ParkRides.NowMilliseconds;
		animations.Advance( now + 2000 );
		Assert.AreEqual( 0f, channel.AnimFrame, "held on frame nought while the script has not run" );

		animations.Trigger( 0, 0, 0, 1f, now + 2000 );
		animations.Advance( now + 2000 + 1000 );
		Assert.AreEqual( 0, channel.Flags & 0x6, "the script's TRIGANIM 0 plays it" );
		Assert.IsTrue( channel.AnimFrame > 25f && channel.AnimFrame < 35f, $"a second in, frame {channel.AnimFrame}" );
	}

	/// <summary>
	/// The drawn fountain, stood by the park's objects, is posed as its construction clip holds it in the very call that
	/// builds it, with no sweep between: its meshes 1 to 4 are put away (opacity nought) and its floor is not. The model is
	/// made without its graphics (<see cref="RuntimeHelpers.GetUninitializedObject"/>), with only the fields a pose reads.
	/// </summary>
	[TestMethod]
	public void TheDrawnFountainIsPosedInTheFrameItIsBuilt()
	{
		var data = GameData.Required();
		FileSystem = data;
		Log ??= new();
		var world = new ParkWorld( new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", data );
		Assert.IsTrue( catalogue.TryGet( Fountain, out var item ) );

		using var stream = new MemoryStream( data.ReadAllBytes( $"{item.Directory}/{item.Stem}.md2" ) );
		var file = new ModelFile( stream );
		var entities = Enumerable.Range( 0, file.Meshes.Count ).Select( _ => new ModelEntity() ).ToArray();

		try
		{
			var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
			const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

			void Set( string field, object value ) => typeof( LobbyModel ).GetField( field, any )!.SetValue( model, value );

			Set( "_file", file );
			Set( "<Entities>k__BackingField", entities );
			Set( "<Offsets>k__BackingField", new OpenTPW.Vector3[file.Meshes.Count] );
			Set( "<Animators>k__BackingField", new MeshAnimator[0] );
			Set( "_nodeParents", file.Nodes.Select( node => node.ParentIndex ).ToArray() );
			Set( "_placedRotation", System.Numerics.Quaternion.Identity );

			var objects = new ParkObjects( "jungle", null, null );
			var rides = new ParkRides( "jungle", world, catalogue, data, objects );
			var animations = RideAnimations.Load( item.Directory, item.Stem, data, item.AnimationChannels );
			objects.Stand( 43, model, animations );

			Assert.IsTrue( entities.All( entity => entity.Opacity == 1f ), "drawn whole before the build" );

			Assert.IsTrue( rides.BindNew( new ParkWorld.CatalogueObject( 43, Fountain, 47 << 8, 33 << 8, 0 ), item ) );

			CollectionAssert.AreEqual( new[] { 1f, 0f, 0f, 0f, 0f }, entities.Take( 5 ).Select( entity => entity.Opacity ).ToArray(),
				"the floor stands, the fountain, the fall, the water and the wall are put away, before any sweep" );
		}
		finally
		{
			foreach ( var entity in entities )
				entity.Delete();
		}
	}

	/// <summary>A model with no role nought clip is left idle at rest, and the build path answers that it stood nothing.</summary>
	[TestMethod]
	public void AModelWithNoBuildClipIsLeftIdle()
	{
		var (catalogue, _) = Park();
		Assert.IsTrue( catalogue.TryGet( Bus, out var item ) );
		var animations = RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels );
		Assert.IsTrue( animations.Loaded > 0, "the bus has clips, none of them role nought" );
		Assert.AreEqual( 0, animations.EntryCount( 0 ) );

		Assert.IsFalse( animations.StandOnBuildClip( 0 ) );
		Assert.IsTrue( animations.Channel( 0 )!.IsIdle );
	}
}
