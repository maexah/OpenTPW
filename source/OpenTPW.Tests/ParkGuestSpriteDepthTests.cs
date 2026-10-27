using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A person's body is tested nearer the eye than it is drawn: <c>Sprite_BuildQuad</c> moves each corner's clip z to
/// <c>z - 0.2 / (z + 1)</c> through a draw matrix of near 0.1 and far 1000 (<c>docs/exe/park-engine.md</c>, "What hides a
/// person below their feet"), and the people's quads go to the buffer furthest first, as they write depth.
/// </summary>
[TestClass]
public class ParkGuestSpriteDepthTests
{
	[TestCleanup]
	public void LetThePoolsGo() => TestRun.DeleteEvery<ParkGuestSprites>();

	/// <summary>
	/// <b>The pull is the original's at every depth</b>: at the locator's 110 Mike is tested at 106.185, 3.81 nearer, and
	/// at 20 at 16.840, 3.16 nearer. A pull of a fixed 3.81 puts 20 at 16.19, and the decode's own
	/// <c>1/e + 3.95 / e²</c> shorthand at 16.70.
	/// </summary>
	[TestMethod]
	public void ThePullIsTheOriginalsAtEveryDepth()
	{
		Assert.AreEqual( 106.185f, ParkGuestSprites.PulledDepth( 110f ), 0.005f, "the locator's zoom" );
		Assert.AreEqual( 16.840f, ParkGuestSprites.PulledDepth( 20f ), 0.005f, "close up" );
		Assert.AreEqual( 3.061f, ParkGuestSprites.PulledDepth( 5f ), 0.005f, "closer still" );
	}

	/// <summary>
	/// <b>A pulled corner lands on the same pixel</b>: it moves along its own line from the eye, so through any lens it
	/// projects where it did, and its depth from the eye is the pulled one.
	/// </summary>
	[TestMethod]
	public void APulledCornerLandsOnTheSamePixel()
	{
		var eye = new OpenTPW.Vector3( 10f, -20f, 30f );
		var forward = new OpenTPW.Vector3( 0f, 0.6f, -0.8f );
		var corner = eye + (forward * 110f) + new OpenTPW.Vector3( 7f, 0.8f, 0.6f );

		var corners = new[] { new Vertex( corner, new OpenTPW.Vector2( 0.25f, 0.5f ) ) { MatFlags = 0xff123456u } };
		ParkGuestSprites.Pull( corners, eye, forward );

		var before = corner - eye;
		var after = corners[0].Position - eye;

		Assert.AreEqual( 106.185f, after.Dot( forward ), 0.005f, "tested at the pulled depth" );
		Assert.AreEqual( 0f, before.Normal.Cross( after.Normal ).Length, 1e-5f, "on the same line from the eye" );
		Assert.IsTrue( after.Dot( before ) > 0f, "on the eye's side it was on" );
		Assert.AreEqual( (0.25f, 0.5f, 0xff123456u), (corners[0].TexCoords.X, corners[0].TexCoords.Y, corners[0].MatFlags), "the picture and colour kept" );
	}

	/// <summary>
	/// <b>The people go to the buffer furthest first</b>, each quad's four corners and its depth together: drawn at
	/// 5, 50 and 20, they are written 50, 20, 5, and a fourth past the count stays where it is.
	/// </summary>
	[TestMethod]
	public void ThePeopleGoFurthestFirst()
	{
		var depths = new[] { 5f, 50f, 20f, 99f };
		var vertices = new Vertex[16];

		for ( var i = 0; i < vertices.Length; ++i )
			vertices[i] = new Vertex( new OpenTPW.Vector3( i / 4, i % 4, 0f ), new OpenTPW.Vector2( 0f, 0f ) );

		ParkGuestSprites.FurthestFirst( vertices, depths, 3 );

		CollectionAssert.AreEqual( new[] { 50f, 20f, 5f, 99f }, depths );

		for ( var i = 0; i < vertices.Length; ++i )
			Assert.AreEqual( (new[] { 1, 2, 0, 3 }[i / 4], i % 4), ((int)vertices[i].Position.X, (int)vertices[i].Position.Y), $"vertex {i}" );
	}

	/// <summary>
	/// <b>The sort keeps its working arrays between frames and puts equal depths in the order it always did.</b> Frames of
	/// 2, 40, 7, 64, 3, 64, 1, 0 and 33 quads on six depths, so nearly every key has a twin, go through one pool and
	/// through the sort as it was written before the arrays were kept (a comparison over a fresh copy of the keys); both
	/// leave the same quads in the same places, ties and all, and a smaller frame after a larger one reads none of
	/// the larger one's leftovers.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> a stable sort (LINQ's <c>OrderByDescending</c>) in place of <c>Array.Sort</c> reorders the ties.
	/// </remarks>
	[TestMethod]
	public void EqualDepthsKeepTheOrderTheSortAlwaysGaveThemAcrossFrames()
	{
		static void Before( Vertex[] vertices, float[] depths, int used )
		{
			used = Math.Min( used, Math.Min( vertices.Length / 4, depths.Length ) );

			if ( used < 2 )
				return;

			var order = new int[used];

			for ( var i = 0; i < used; ++i )
				order[i] = i;

			var keys = depths.AsSpan( 0, used ).ToArray();
			Array.Sort( order, ( a, b ) => keys[b].CompareTo( keys[a] ) );

			var quads = vertices.AsSpan( 0, used * 4 ).ToArray();

			for ( var i = 0; i < used; ++i )
			{
				quads.AsSpan( order[i] * 4, 4 ).CopyTo( vertices.AsSpan( i * 4, 4 ) );
				depths[i] = keys[order[i]];
			}
		}

		var random = new Random( 744 );
		var pool = new ParkGuestSprites( "jungle", null );
		pool.Resize( 64 );

		foreach ( var used in new[] { 2, 40, 7, 64, 3, 64, 1, 0, 33 } )
		{
			var (vertices, depths) = pool.Written;

			for ( var i = 0; i < used; ++i )
				depths[i] = random.Next( 6 ) * 10f;

			for ( var v = 0; v < used * 4; ++v )
				vertices[v] = new Vertex( new OpenTPW.Vector3( v / 4, v % 4, 0f ), new OpenTPW.Vector2( 0f, 0f ) );

			var (expectedVertices, expectedDepths) = (vertices.ToArray(), depths.ToArray());
			Before( expectedVertices, expectedDepths, used );

			pool.Finish( used );

			CollectionAssert.AreEqual( expectedDepths[..used], depths[..used], $"{used} quads: the depths" );
			CollectionAssert.AreEqual(
				expectedVertices[..( used * 4 )].Select( v => (v.Position.X, v.Position.Y) ).ToArray(),
				vertices[..( used * 4 )].Select( v => (v.Position.X, v.Position.Y) ).ToArray(),
				$"{used} quads: the quads" );
		}
	}

	/// <summary>
	/// <b>A frame's quads are sorted before they go up</b>: written at 5, 50 and 20, the frame ends with them 50, 20, 5,
	/// and the upload still reaches the three.
	/// </summary>
	[TestMethod]
	public void AFrameEndsFurthestFirst()
	{
		var pool = new ParkGuestSprites( "jungle", null );
		pool.Resize( 4 );

		var (vertices, depths) = pool.Written;
		var drawn = new[] { 5f, 50f, 20f };

		for ( var i = 0; i < 12; ++i )
			vertices[i] = new Vertex( new OpenTPW.Vector3( i / 4, 0f, 0f ), new OpenTPW.Vector2( 0f, 0f ) );

		drawn.CopyTo( depths, 0 );

		Assert.AreEqual( 3, pool.Finish( 3 ) );
		CollectionAssert.AreEqual( new[] { 50f, 20f, 5f }, depths[..3] );
		Assert.AreEqual( (1, 2, 0), ((int)vertices[0].Position.X, (int)vertices[4].Position.X, (int)vertices[8].Position.X) );
	}

	/// <summary>
	/// <b>The frame the people are drawn in ends furthest first</b>, through the code that writes it: three people stood
	/// 5, 50 and 20 along the camera's line of sight from a camera that is not at the origin, each with the facing dash
	/// the debug console can switch on, are written six quads whose keys are those depths twice, and the frame hands the
	/// upload the quads 50, 50, 20, 20, 5, 5.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the key of a picture (<c>WriteQuad</c>) or of a dash (<c>WriteGroundDash</c>) negated, taken from
	/// the point itself rather than from the camera, or the frame ending in <c>Fold</c> and not <c>Finish</c> all leave the
	/// depths out of order or different from the stood-at ones.
	/// </remarks>
	[TestMethod]
	public void AFrameOfPeopleIsWrittenKeyedByDepthAndEndsFurthestFirst()
	{
		var data = GameData.Required();
		var bankFile = ParkGuestSprites.BanksIn( data, ParkGuestSprites.FolderFor( 0, "jungle" )!, 0 )[0];
		var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( bankFile ) ) );
		var pictures = Enumerable.Repeat( new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 ), 1024 ).ToArray();

		var all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		var cameraField = typeof( Camera ).GetField( "CameraMode", all )!;
		var cameraBefore = cameraField.GetValue( null );
		var statics = new[] { typeof( ParkPeople ), typeof( ParkBumperBoats ), typeof( ParkGround ), typeof( ParkObjects ) }
			.Select( type => type.GetProperty( "Current", all )! ).ToArray();
		var staticsBefore = statics.Select( property => property.GetValue( null ) ).ToArray();
		var dashBefore = ParkGuestSprites.DebugFacing;

		try
		{
			foreach ( var property in statics )
				property.SetValue( null, null );

			Camera.SetCameraMode<CameraMode>();
			var mode = (CameraMode)cameraField.GetValue( null )!;
			mode.Position = new OpenTPW.Vector3( 100f, -40f, 7f );

			var forward = Camera.Rotation.Forward;
			Assert.IsTrue( Math.Abs( OpenTPW.Vector3.Dot( mode.Position, forward ) ) > 1f, "the control: the camera is not at the origin's depth" );

			var pool = new ParkGuestSprites( "jungle", null );
			pool.Resize( 6 );

			var loaded = typeof( ParkGuestSprites ).GetNestedType( "Loaded", all )!;
			((IDictionary)typeof( ParkGuestSprites ).GetField( "_banks", all )!.GetValue( pool )!)[(0, 0)] = Activator.CreateInstance( loaded, bank, pictures );
			typeof( ParkGuestSprites ).GetField( "_plain", all )!.SetValue( pool, pictures[0] );

			var people = (List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>)typeof( ParkGuestSprites ).GetField( "_people", all )!.GetValue( pool )!;

			foreach ( var (thing, depth) in new[] { (1, 5f), (2, 50f), (3, 20f) } )
			{
				var at = mode.Position + (forward * depth);
				people.Add( (new ParkWorld.Person( thing, 0, 0, 0, thing, 0, default!, null ),
					new ParkWorld.Sprite( thing, 0, 0, 0, at.X, at.Z, at.Y, 0, 0, 255, 0, 0, 0 )) );
			}

			ParkGuestSprites.DebugFacing = true;

			var (used, reach) = pool.WriteTheFrame();
			var (_, depths) = pool.Written;

			Assert.AreEqual( (6, 6), (used, reach), "a picture and a dash each" );
			CollectionAssert.AreEqual( new[] { 50f, 50f, 20f, 20f, 5f, 5f }, depths.Select( d => MathF.Round( d, 2 ) ).ToArray(),
				"each keyed by its depth from the camera along its line of sight, furthest first" );
		}
		finally
		{
			ParkGuestSprites.DebugFacing = dashBefore;
			cameraField.SetValue( null, cameraBefore );

			for ( var i = 0; i < statics.Length; ++i )
				statics[i].SetValue( null, staticsBefore[i] );
		}
	}

	/// <summary>
	/// <b>The people write depth through the original's alpha test</b>: their pipeline leaves depth writing on, and
	/// their shader drops a fragment under 16 of 255 (ALPHAFUNC GREATEREQUAL, ALPHAREF 16; <c>render-states.md</c>), not
	/// only a clear one as the particles' shader does. <b>This checks the constants</b> <see cref="ParkGuestSprites.PeopleMaterial"/>
	/// and <see cref="ParkGuestSprites.PeopleShader"/> and the shader's text, not the material <c>Build</c> makes from them
	/// (that needs a device); that <c>Build</c> uses them rests on the game run.
	/// </summary>
	[TestMethod]
	public void ThePeopleWriteDepthThroughTheAlphaTest()
	{
		Assert.IsFalse( ParkGuestSprites.PeopleMaterial.HasFlag( MaterialFlags.DisableDepthWrite ), "depth written" );
		Assert.IsFalse( ParkGuestSprites.PeopleMaterial.HasFlag( MaterialFlags.DisableDepthTest ), "depth tested" );
		Assert.IsTrue( ParkGuestSprites.PeopleMaterial.HasFlag( MaterialFlags.DisableCulling ), "both faces" );

		var text = File.ReadAllText( Path.Combine( AppContext.BaseDirectory, ParkGuestSprites.PeopleShader ) );
		StringAssert.Contains( text, "if ( fragColor.a < 16.0 / 255.0 )\n          discard;" );
	}
}
