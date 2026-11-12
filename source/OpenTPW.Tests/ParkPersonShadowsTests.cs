using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A person's shadow, as the original lays it (<c>ShadowLights_Add</c> (<c>0x00584be0</c>), <c>SpriteShadow_Draw</c>, <c>SpriteShadow_BuildQuad</c>;
/// <c>docs/exe/park-engine.md</c>, "A person's shadow"): the jungle's sun (0.4, -0.8, 0.4) gives darkness 103, the
/// colour 0xff989898, and a picture's height laid half a unit across and half along for each unit stood up.
/// </summary>
[TestClass]
public class ParkPersonShadowsTests
{
	[TestCleanup]
	public void LetThePoolsGo() => TestRun.DeleteEvery<ParkGuestSprites>();

	/// <summary>The jungle's <c>LightNormal</c> as <c>Level</c> swaps it into this engine's Z up, normalised.</summary>
	private static readonly Vector3 Sun = new Vector3( 0.4f, 0.4f, -0.8f ).Normal;

	/// <summary>
	/// <b>Darkness 103 and the colour 0xff989898</b>, as the running original held them (0x67000000 at <c>0x008c1808</c>):
	/// 0.8165 x 127 = 103.69, truncated. A light travelling up casts none; one barely down casts a shadow of darkness 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> rounding (104), the scale 128 (104), the colour's channels not taken from 255 (0xff676767), or a
	/// light up let through, each fails.</remarks>
	[TestMethod]
	public void TheJungleSunGivesDarkness103()
	{
		Assert.AreEqual( 103, ParkPersonShadows.Darkness( Sun ) );
		Assert.AreEqual( 0xff989898u, ParkPersonShadows.Colour( 103 ) );
		Assert.AreEqual( 0xffffffffu, ParkPersonShadows.Colour( 0 ) );
		Assert.AreEqual( 0xff808080u, ParkPersonShadows.Colour( 127 ) );
		Assert.IsNull( ParkPersonShadows.Darkness( new Vector3( 0f, 0.6f, 0.8f ) ), "a light travelling up" );
		Assert.AreEqual( 0, ParkPersonShadows.Darkness( new Vector3( 0.999f, 0f, -0.001f ) ), "barely down" );
	}

	/// <summary>
	/// <b>Mike's 41 x 52 picture, origin 21, under the jungle sun</b>, stood at (100, 200) on a slope: the foot
	/// 21 x 20/128 x 0.75 = 2.461 left and 2.344 right along the unnormalised across (0.408, -0.408), the far end
	/// 52 x 20/128 = 8.125 up laid (4.0625, 4.0625) on, every corner 0.6 over the slope under it, the silhouette's top at
	/// the far end.
	/// </summary>
	/// <remarks><b>Mutations:</b> the across normalised (1.74 a side), the span over 1.414 as the people's, no 0.75, the
	/// height laid by |z| the wrong way, the ground read at the place not each corner, no lift, or the far end's v 1, each
	/// fails.</remarks>
	[TestMethod]
	public void MikesShadowFallsUpTheSunsWay()
	{
		var corners = new Vertex[4];
		Func<float, float, float> slope = ( x, y ) => (0.1f * x) + (0.2f * y);

		Assert.IsTrue( ParkPersonShadows.Corners( corners, new Vector2( 100f, 200f ), 41, 52, 21, false, Sun, slope,
			ParkPersonShadows.Lift, 0xff989898u ) );

		var want = new[] { (103.05784f, 205.06716f), (105.01933f, 203.10567f), (100.95683f, 199.04317f), (98.99534f, 201.00466f) };
		var uv = new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) };

		for ( var i = 0; i < 4; ++i )
		{
			Assert.AreEqual( want[i].Item1, corners[i].Position.X, 0.001f, $"corner {i} x" );
			Assert.AreEqual( want[i].Item2, corners[i].Position.Y, 0.001f, $"corner {i} y" );
			Assert.AreEqual( (0.1f * want[i].Item1) + (0.2f * want[i].Item2) + 0.6f, corners[i].Position.Z, 0.001f, $"corner {i} on the slope" );
			Assert.AreEqual( uv[i], (corners[i].TexCoords.X, corners[i].TexCoords.Y), $"corner {i} uv" );
			Assert.AreEqual( 0xff989898u, corners[i].MatFlags, $"corner {i} colour" );
		}
	}

	/// <summary><b>Reflected, both sides turn about the place</b> and the silhouette is not flipped: the foot 2.461 on the right, 2.344 on the left.</summary>
	/// <remarks><b>Mutation:</b> the reflection dropped, or only the u swapped, fails.</remarks>
	[TestMethod]
	public void AReflectedPictureTurnsAboutItsPlace()
	{
		var corners = new Vertex[4];

		Assert.IsTrue( ParkPersonShadows.Corners( corners, new Vector2( 100f, 200f ), 41, 52, 21, true, Sun, ( _, _ ) => 0f,
			ParkPersonShadows.Lift, 0xff989898u ) );

		Assert.AreEqual( 101.00466f, corners[3].Position.X, 0.001f );
		Assert.AreEqual( 198.99534f, corners[3].Position.Y, 0.001f );
		Assert.AreEqual( 99.04317f, corners[2].Position.X, 0.001f );
		Assert.AreEqual( 200.95683f, corners[2].Position.Y, 0.001f );
		Assert.AreEqual( (0f, 1f), (corners[3].TexCoords.X, corners[3].TexCoords.Y) );
	}

	/// <summary><b>A light travelling up writes nothing.</b></summary>
	[TestMethod]
	public void ALightTravellingUpWritesNothing()
	{
		var corners = new Vertex[4];
		corners[0] = new Vertex( new Vector3( 7f, 7f, 7f ) );

		Assert.IsFalse( ParkPersonShadows.Corners( corners, new Vector2( 100f, 200f ), 41, 52, 21, false, new Vector3( 0f, 0.6f, 0.8f ),
			( _, _ ) => 0f, ParkPersonShadows.Lift, 0xff989898u ) );
		Assert.AreEqual( 7f, corners[0].Position.X );
	}

	/// <summary>The detail files' <c>SPRITESHADOW</c>: 0 at low, 1 (the silhouette) at medium, 2 (the own picture) at high.</summary>
	/// <remarks><b>Mutation:</b> the key misnamed (medium's 1 everywhere) fails low and high.</remarks>
	[TestMethod]
	public void TheDetailFilesPickTheTier()
	{
		FileSystem = GameData.Required();
		Log = new();

		Assert.AreEqual( 0, ParkPersonShadows.Tier( "low.sam" ) );
		Assert.AreEqual( 1, ParkPersonShadows.Tier( "med.sam" ) );
		Assert.AreEqual( 2, ParkPersonShadows.Tier( "high.sam" ) );
		Assert.IsTrue( FileSystem.FileExists( ParkPersonShadows.Silhouette ), "the shipped silhouette" );
	}

	/// <summary>
	/// <b>A shadow stands on the plane through its cell's near corner</b>, not the bilinear land: on the jungle's first cell
	/// whose four corners are not one plane, three quarters of the way across it the two differ, and the plane is the near
	/// corner plus its two neighbours' rises.
	/// </summary>
	/// <remarks><b>Mutation:</b> the far corner's height taken in (bilinear), or the cell found by rounding, fails.</remarks>
	[TestMethod]
	public void AShadowStandsOnTheNearCornersPlane()
	{
		var data = GameData.Required();
		HeightfieldFile field;

		using ( var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) ) )
			field = new HeightfieldFile( stream );

		var (cx, cy) = (from y in Enumerable.Range( 0, field.CellsY )
						from x in Enumerable.Range( 0, field.CellsX )
						let bend = field.HeightAt( x + 1, y + 1 ) - field.HeightAt( x + 1, y ) - field.HeightAt( x, y + 1 ) + field.HeightAt( x, y )
						where MathF.Abs( bend ) > 1f
						select (x, y)).First();

		var (wx, wy) = ((cx + 0.75f) * field.CellSizeX, (cy + 0.75f) * field.CellSizeY);
		var near = field.HeightAt( cx, cy );
		var plane = near + ((field.HeightAt( cx + 1, cy ) - near) * 0.75f) + ((field.HeightAt( cx, cy + 1 ) - near) * 0.75f);

		Assert.AreEqual( plane, field.CornerPlaneAt( wx, wy ), 0.001f );
		Assert.IsTrue( MathF.Abs( field.CornerPlaneAt( wx, wy ) - field.HeightAtWorld( wx, wy ) ) > 0.5f, "the bilinear land differs there" );
	}

	/// <summary>
	/// <b>Every body drawn casts one</b>, through the code that writes the frame: three people stood about a camera, the
	/// jungle sun given, each gets the shadow <see cref="ParkPersonShadows.Corners"/> lays at the place their picture was
	/// drawn, 0.6 over no ground, in the frame's shadow quads.
	/// </summary>
	/// <remarks><b>Mutations:</b> the shadow placed at the picture's centre lifted by its height, no shadow written, or the
	/// picture's width and height swapped, each fails.</remarks>
	[TestMethod]
	public void EveryBodyDrawnCastsOne()
	{
		var picture = new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 );

		WithThreeBodies( 1, false, picture, pool =>
		{
			Assert.AreEqual( 3, pool.ShadowsDrawn.Count, "one a body" );

			foreach ( var drawn in pool.Drawn )
			{
				var index = pool.ShadowsDrawn.ToList().FindIndex( shadow => shadow.Thing == drawn.Thing );
				Assert.IsTrue( index >= 0, $"thing {drawn.Thing} casts one" );

				var want = new Vertex[4];
				ParkPersonShadows.Corners( want, new Vector2( drawn.Origin.X, drawn.Origin.Y ), drawn.Width, drawn.Height, drawn.OriginX,
					drawn.Mirrored, Sun, ( _, _ ) => 0f, ParkPersonShadows.Lift, 0xff989898u );

				for ( var i = 0; i < 4; ++i )
				{
					var got = pool.ShadowsWritten[(index * 4) + i];
					Assert.AreEqual( want[i].Position.X, got.Position.X, 0.0001f, $"thing {drawn.Thing} corner {i} x" );
					Assert.AreEqual( want[i].Position.Y, got.Position.Y, 0.0001f, $"thing {drawn.Thing} corner {i} y" );
					Assert.AreEqual( 0.6f, got.Position.Z, 0.0001f, $"thing {drawn.Thing} corner {i} over no ground" );
					Assert.AreEqual( 0xff989898u, got.MatFlags );
					Assert.AreEqual( want[i].TexCoords, got.TexCoords, $"thing {drawn.Thing} corner {i} the whole silhouette" );
				}
			}
		} );
	}

	/// <summary>
	/// <b>First person casts the body's own picture</b> (<c>FUN_00589770</c>, <c>0x0058a800</c>), through the code that writes
	/// the frame: at tier 1 and tier 2 alike, each corner carries the atlas region's own uv (never the silhouette's 0 to 1), the
	/// colour is black at the darkness alpha, 0x67000000, and the lift is 0.25 (<c>0x2000</c>). At tier 0 none is cast.
	/// </summary>
	/// <remarks><b>Mutations:</b> the silhouette's uv or its 0xff989898 colour kept in first person, the orbit view's 0.6 lift,
	/// tier 0 let through, or the choice keyed on tier 1 alone, each fails.</remarks>
	[TestMethod]
	public void FirstPersonCastsTheBodysOwnPicture()
	{
		var picture = new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 );

		foreach ( var tier in new[] { 1, 2 } )
		{
			WithThreeBodies( tier, true, picture, pool =>
			{
				Assert.AreEqual( 3, pool.ShadowsDrawn.Count, $"tier {tier}: one a body in first person" );

				foreach ( var drawn in pool.Drawn )
				{
					var index = pool.ShadowsDrawn.ToList().FindIndex( shadow => shadow.Thing == drawn.Thing );
					Assert.IsTrue( index >= 0, $"thing {drawn.Thing} casts one" );

					var want = new Vertex[4];
					ParkPersonShadows.Corners( want, new Vector2( drawn.Origin.X, drawn.Origin.Y ), drawn.Width, drawn.Height, drawn.OriginX,
						drawn.Mirrored, Sun, ( _, _ ) => 0f, ParkPersonShadows.FirstPersonLift, 0x67000000u, (0.25f, 0.1f, 0.5f, 0.3f) );

					var uv = new[] { (0.25f, 0.1f), (0.5f, 0.1f), (0.5f, 0.3f), (0.25f, 0.3f) };

					for ( var i = 0; i < 4; ++i )
					{
						var got = pool.ShadowsWritten[(index * 4) + i];
						Assert.AreEqual( want[i].Position.X, got.Position.X, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} x" );
						Assert.AreEqual( want[i].Position.Y, got.Position.Y, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} y" );
						Assert.AreEqual( 0.25f, got.Position.Z, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} lifted 0.25" );
						Assert.AreEqual( 0x67000000u, got.MatFlags, $"tier {tier} corner {i} black at the darkness alpha" );
						Assert.AreEqual( uv[i], (got.TexCoords.X, got.TexCoords.Y), $"tier {tier} corner {i} the picture's own uv" );
					}
				}
			} );
		}

		WithThreeBodies( 0, true, picture, pool => Assert.AreEqual( 0, pool.ShadowsDrawn.Count, "tier 0 casts none in first person" ) );
	}

	/// <summary>
	/// <b>High detail in the orbit view casts the body's own picture</b> (<c>SpriteShadow_Draw</c>'s non-<c>0x40000</c> arm,
	/// Q521), through the code that writes the frame: at tiers 2 and 3 each corner carries the atlas region's own uv, the
	/// colour is the darkness word 0x67000000, and the lift is the orbit view's 0.6, not first person's 0.25. At tier 1 the
	/// same view keeps the silhouette's grey and whole texture. Unfogged: the people's draw is handed no fog band outside
	/// first person, however the park's band is set.
	/// </summary>
	/// <remarks><b>Mutations:</b> tier 2 or 3 left uncast in the orbit view, the silhouette's colour or uv kept there, first
	/// person's 0.25 lift used, the own picture put on tier 1, or the band handed to the orbit view, each fails.</remarks>
	[TestMethod]
	public void TheOrbitViewAtHighCastsTheBodysOwnPictureUnfogged()
	{
		var picture = new ParkGuestSprites.Region( 0.25f, 0.1f, 0.5f, 0.3f, 38, 50, 22, 42 );
		var uv = new[] { (0.25f, 0.1f), (0.5f, 0.1f), (0.5f, 0.3f), (0.25f, 0.3f) };

		foreach ( var tier in new[] { 2, 3 } )
		{
			WithThreeBodies( tier, false, picture, pool =>
			{
				Assert.AreEqual( 3, pool.ShadowsDrawn.Count, $"tier {tier}: one a body in the orbit view" );

				foreach ( var drawn in pool.Drawn )
				{
					var index = pool.ShadowsDrawn.ToList().FindIndex( shadow => shadow.Thing == drawn.Thing );
					Assert.IsTrue( index >= 0, $"thing {drawn.Thing} casts one" );

					var want = new Vertex[4];
					ParkPersonShadows.Corners( want, new Vector2( drawn.Origin.X, drawn.Origin.Y ), drawn.Width, drawn.Height, drawn.OriginX,
						drawn.Mirrored, Sun, ( _, _ ) => 0f, ParkPersonShadows.Lift, 0x67000000u, (0.25f, 0.1f, 0.5f, 0.3f) );

					for ( var i = 0; i < 4; ++i )
					{
						var got = pool.ShadowsWritten[(index * 4) + i];
						Assert.AreEqual( want[i].Position.X, got.Position.X, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} x" );
						Assert.AreEqual( want[i].Position.Y, got.Position.Y, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} y" );
						Assert.AreEqual( 0.6f, got.Position.Z, 0.0001f, $"tier {tier} thing {drawn.Thing} corner {i} lifted 0.6" );
						Assert.AreEqual( 0x67000000u, got.MatFlags, $"tier {tier} corner {i} black at the darkness alpha" );
						Assert.AreEqual( uv[i], (got.TexCoords.X, got.TexCoords.Y), $"tier {tier} corner {i} the picture's own uv" );
					}
				}
			} );
		}

		WithThreeBodies( 1, false, picture, pool =>
		{
			Assert.AreEqual( 3, pool.ShadowsDrawn.Count );
			Assert.AreEqual( 0xff989898u, pool.ShadowsWritten[0].MatFlags, "tier 1 keeps the silhouette's grey" );
			Assert.AreEqual( (0f, 0f), (pool.ShadowsWritten[0].TexCoords.X, pool.ShadowsWritten[0].TexCoords.Y), "and its whole texture" );
		} );

		var band = FirstPersonFog.ForViewDistance( 1 );
		Assert.AreEqual( 0f, Level.LinearFogFor( Level.Scene.Park, false, band ).Z, "the orbit view's draw is handed no fog" );
		Assert.AreEqual( 1f, Level.LinearFogFor( Level.Scene.Park, true, band ).Z, "first person's is" );
	}

	/// <summary>
	/// Three people stood about a camera, the jungle sun given, the frame written at the shadow <paramref name="tier"/> in the
	/// orbit view or first person (<see cref="ParkGuestSprites.FirstPerson"/>), every picture the given <paramref name="picture"/>;
	/// <paramref name="check"/> sees the pool.
	/// </summary>
	private static void WithThreeBodies( int tier, bool firstPerson, ParkGuestSprites.Region picture, Action<ParkGuestSprites> check )
	{
		var data = GameData.Required();
		var bankFile = ParkGuestSprites.BanksIn( data, ParkGuestSprites.FolderFor( 0, "jungle" )!, 0 )[0];
		var bank = new SpriteBankFile( new MemoryStream( data.ReadAllBytes( bankFile ) ) );
		var pictures = Enumerable.Repeat( picture, 1024 ).ToArray();

		var all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		var cameraField = typeof( Camera ).GetField( "CameraMode", all )!;
		var cameraBefore = cameraField.GetValue( null );
		var statics = new[] { typeof( ParkPeople ), typeof( ParkBumperBoats ), typeof( ParkGround ), typeof( ParkObjects ) }
			.Select( type => type.GetProperty( "Current", all )! ).ToArray();
		var staticsBefore = statics.Select( property => property.GetValue( null ) ).ToArray();

		try
		{
			foreach ( var property in statics )
				property.SetValue( null, null );

			Camera.SetCameraMode<CameraMode>();
			var mode = (CameraMode)cameraField.GetValue( null )!;
			mode.Position = new Vector3( 100f, -40f, 7f );

			var pool = new ParkGuestSprites( "jungle", null ) { ShadowLight = Sun };
			typeof( ParkGuestSprites ).GetField( "_shadowTier", all )!.SetValue( pool, tier );
			typeof( ParkGuestSprites ).GetProperty( "FirstPerson", all )!.SetValue( pool, firstPerson );
			var loaded = typeof( ParkGuestSprites ).GetNestedType( "Loaded", all )!;
			((IDictionary)typeof( ParkGuestSprites ).GetField( "_banks", all )!.GetValue( pool )!)[(0, 0)] = Activator.CreateInstance( loaded, bank, pictures );

			var people = (List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>)typeof( ParkGuestSprites ).GetField( "_people", all )!.GetValue( pool )!;

			foreach ( var (thing, depth) in new[] { (1, 5f), (2, 50f), (3, 20f) } )
			{
				var at = mode.Position + (Camera.Rotation.Forward * depth);
				people.Add( (new ParkWorld.Person( thing, 0, 0, 0, thing, 0, default!, null ),
					new ParkWorld.Sprite( thing, 0, 0, 0, at.X, at.Z, at.Y, 0, 0, 255, 0, 0, 0 )) );
			}

			pool.Resize( 12 );
			pool.WriteTheFrame();

			check( pool );
		}
		finally
		{
			cameraField.SetValue( null, cameraBefore );

			for ( var i = 0; i < statics.Length; ++i )
				statics[i].SetValue( null, staticsBefore[i] );
		}
	}

	/// <summary>
	/// <b>The shadow multiplies what lies under it</b>: its pipeline is the people's with depth writing off and the multiply
	/// blend (SRCBLEND ZERO, DESTBLEND SRCCOLOR). <b>This checks the flag</b> <see cref="MaterialFlags.Multiply"/> exists apart
	/// from the additive one; that <c>BuildShadows</c> uses it, and that the blend darkens by 152/255, rests on the game run.
	/// </summary>
	[TestMethod]
	public void TheMultiplyIsItsOwnBlend()
	{
		Assert.AreNotEqual( MaterialFlags.Additive, MaterialFlags.Multiply );
		Assert.AreEqual( MaterialFlags.None, MaterialFlags.Multiply & (MaterialFlags.Additive | MaterialFlags.DisableDepth | MaterialFlags.DisableCulling) );
	}

	/// <summary>
	/// <b>The shadow pipelines' flags</b>: the silhouette's is the people's, no depth written, multiplied; the own picture's,
	/// first person's and the orbit view's at high, is the people's with no depth written and the default alpha blend, no
	/// multiply (<c>0x556</c> | <c>0x800</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the multiply or the depth-write drop taken out of the silhouette's, the multiply put on the
	/// own picture's, its depth write left on, or the orbit view's own picture given the silhouette's, each fails.</remarks>
	[TestMethod]
	public void EachShadowPipelineHasItsFlags()
	{
		Assert.AreEqual( MaterialFlags.DisableCulling | MaterialFlags.DisableDepthWrite | MaterialFlags.Multiply, ParkGuestSprites.SilhouetteShadowMaterial );
		Assert.AreEqual( MaterialFlags.DisableCulling | MaterialFlags.DisableDepthWrite, ParkGuestSprites.OwnPictureShadowMaterial );
		Assert.AreEqual( MaterialFlags.None, ParkGuestSprites.OwnPictureShadowMaterial & MaterialFlags.Multiply );
		Assert.AreEqual( ParkGuestSprites.SilhouetteShadowMaterial, ParkGuestSprites.ShadowMaterialFor( ParkPersonShadows.Kind.Silhouette ) );
		Assert.AreEqual( ParkGuestSprites.OwnPictureShadowMaterial, ParkGuestSprites.ShadowMaterialFor( ParkPersonShadows.Kind.FirstPersonOwnPicture ) );
		Assert.AreEqual( ParkGuestSprites.OwnPictureShadowMaterial, ParkGuestSprites.ShadowMaterialFor( ParkPersonShadows.Kind.OrbitOwnPicture ) );
	}

	/// <summary>
	/// <b>First person's colour is black at the darkness alpha</b>: the light's darkness word, <c>trunc(-127 Ly) &lt;&lt; 24</c>,
	/// 0x67000000 under the jungle sun (<c>0x00589750</c>, <c>0x00584d97</c>), not the silhouette's grey.
	/// </summary>
	/// <remarks><b>Mutation:</b> the shift 16 or 0, the grey colour, or the darkness taken from 255, each fails.</remarks>
	[TestMethod]
	public void FirstPersonsColourIsBlackAtTheDarknessAlpha()
	{
		Assert.AreEqual( 0x67000000u, ParkPersonShadows.OwnPictureColour( 103 ) );
		Assert.AreEqual( 0x7f000000u, ParkPersonShadows.OwnPictureColour( 127 ) );
		Assert.AreEqual( 0u, ParkPersonShadows.OwnPictureColour( 0 ) );
		Assert.AreEqual( 103, (int)(ParkPersonShadows.OwnPictureColour( ParkPersonShadows.Darkness( Sun )!.Value ) >> 24) );
	}

	/// <summary>
	/// <b>A picture's own uv rides the corners, unflipped by a reflection</b> (<c>SpriteShadow_BuildQuad</c> negates the sides
	/// under flag <c>0x10</c> and leaves the picture's columns alone): corner 0 takes (Left, Top), 1 (Right, Top), 2
	/// (Right, Bottom), 3 (Left, Bottom), reflected or not.
	/// </summary>
	/// <remarks><b>Mutations:</b> the whole-texture uv used, the Left and Right swapped when reflected, or the rows swapped,
	/// each fails.</remarks>
	[TestMethod]
	public void ARegionsUvRidesTheCornersUnflipped()
	{
		foreach ( var mirrored in new[] { false, true } )
		{
			var corners = new Vertex[4];

			Assert.IsTrue( ParkPersonShadows.Corners( corners, new Vector2( 100f, 200f ), 41, 52, 21, mirrored, Sun, ( _, _ ) => 0f,
				ParkPersonShadows.FirstPersonLift, 0x67000000u, (0.25f, 0.1f, 0.5f, 0.3f) ) );

			var want = new[] { (0.25f, 0.1f), (0.5f, 0.1f), (0.5f, 0.3f), (0.25f, 0.3f) };

			for ( var i = 0; i < 4; ++i )
			{
				Assert.AreEqual( want[i], (corners[i].TexCoords.X, corners[i].TexCoords.Y), $"mirrored {mirrored} corner {i}" );
				Assert.AreEqual( 0x67000000u, corners[i].MatFlags );
				Assert.AreEqual( 0.25f, corners[i].Position.Z, 0.0001f );
			}
		}
	}

	/// <summary>
	/// <b>Which shadow each tier casts in each view</b>: tier 0 none in either (<c>0x20000</c>); first person (<c>0x80</c>) the
	/// body's own picture at every other tier, as <c>FUN_00589770</c> never reads <c>0x40000</c>; the orbit view's silhouette at
	/// 1 and its own picture (<c>SpriteShadow_Draw</c>'s non-<c>0x40000</c> arm) at 2 and 3.
	/// </summary>
	/// <remarks><b>Mutations:</b> first person keyed on tier 1, tier 0 let through in first person, or the orbit view's tier 2
	/// given the silhouette, each fails.</remarks>
	[TestMethod]
	public void EachTierCastsItsKindInEachView()
	{
		Assert.AreEqual( ParkPersonShadows.Kind.None, ParkPersonShadows.KindFor( 0, false ) );
		Assert.AreEqual( ParkPersonShadows.Kind.None, ParkPersonShadows.KindFor( 0, true ) );
		Assert.AreEqual( ParkPersonShadows.Kind.Silhouette, ParkPersonShadows.KindFor( 1, false ) );
		Assert.AreEqual( ParkPersonShadows.Kind.FirstPersonOwnPicture, ParkPersonShadows.KindFor( 1, true ) );
		Assert.AreEqual( ParkPersonShadows.Kind.OrbitOwnPicture, ParkPersonShadows.KindFor( 2, false ) );
		Assert.AreEqual( ParkPersonShadows.Kind.OrbitOwnPicture, ParkPersonShadows.KindFor( 3, false ) );
		Assert.IsFalse( ParkPersonShadows.IsOwnPicture( ParkPersonShadows.Kind.Silhouette ) );
		Assert.IsTrue( ParkPersonShadows.IsOwnPicture( ParkPersonShadows.Kind.OrbitOwnPicture ) );
		Assert.AreEqual( ParkPersonShadows.Kind.FirstPersonOwnPicture, ParkPersonShadows.KindFor( 2, true ) );
		Assert.AreEqual( ParkPersonShadows.Kind.FirstPersonOwnPicture, ParkPersonShadows.KindFor( 3, true ) );
	}
}
