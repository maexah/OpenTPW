using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace OpenTPW.Tests;

/// <summary>
/// Where a sound sits across the stereo field, as heard from somewhere pointing somewhere. None of
/// this needs the game installed.
/// </summary>
[TestClass]
public class AudioListenerTests
{
	/// <summary>
	/// At the origin, with its right ear pointing the way the world calls right.
	///
	/// That is <b>-Y</b>, not +Y - see <see cref="Vector3.Right"/> - and it is the whole reason these
	/// exist: a pan that had the sign backwards would still sound like working positional audio, just
	/// mirrored, and nothing about listening to it would say which way round it was meant to be.
	/// </summary>
	private static readonly AudioListener Listener = new( Vector3.Zero, Vector3.Right );

	[TestMethod]
	public void ASoundStraightAheadIsCentred()
		=> Assert.AreEqual( 0f, Listener.PanTo( new Vector3( 10, 0, 0 ) ), 1e-5f );

	[TestMethod]
	public void ASoundToTheRightPansRight()
		=> Assert.AreEqual( 1f, Listener.PanTo( new Vector3( 0, -10, 0 ) ), 1e-5f );

	[TestMethod]
	public void ASoundToTheLeftPansLeft()
		=> Assert.AreEqual( -1f, Listener.PanTo( new Vector3( 0, 10, 0 ) ), 1e-5f );

	/// <summary>
	/// Two speakers cannot say front from back, and this does not pretend to - the original bought
	/// that distinction with QSound's own processing, which a pan cannot reproduce.
	/// </summary>
	[TestMethod]
	public void ASoundBehindIsCentredJustLikeOneAhead()
		=> Assert.AreEqual(
			Listener.PanTo( new Vector3( 10, 0, 0 ) ),
			Listener.PanTo( new Vector3( -10, 0, 0 ) ), 1e-5f );

	/// <summary>Straight overhead is neither side either. The world is Z-up.</summary>
	[TestMethod]
	public void ASoundOverheadIsCentred()
		=> Assert.AreEqual( 0f, Listener.PanTo( new Vector3( 0, 0, 10 ) ), 1e-5f );

	/// <summary>
	/// Standing on top of a sound there is no direction to pan it by. Normalising would divide by a
	/// zero length, and a NaN gain would silence the whole mix rather than just this voice.
	/// </summary>
	[TestMethod]
	public void ASoundOnTheListenerIsCentredRatherThanNotANumber()
	{
		var pan = Listener.PanTo( Vector3.Zero );

		Assert.IsFalse( float.IsNaN( pan ), "a source at the listener panned to NaN" );
		Assert.AreEqual( 0f, pan, 1e-5f );
	}

	/// <summary>How far away it is says nothing about which side it is on.</summary>
	[TestMethod]
	public void DistanceDoesNotChangeWhichSideItIsOn()
		=> Assert.AreEqual(
			Listener.PanTo( new Vector3( 0, -1, 0 ) ),
			Listener.PanTo( new Vector3( 0, -1000, 0 ) ), 1e-5f );

	/// <summary>
	/// Halfway between ahead and hard right comes out at the cosine of the angle rather than at a
	/// half, which is what says this is a direction and not a switch.
	/// </summary>
	[TestMethod]
	public void ASoundOnTheDiagonalIsPartWayAcross()
		=> Assert.AreEqual( MathF.Sqrt( 0.5f ), Listener.PanTo( new Vector3( 10, -10, 0 ) ), 1e-5f );

	/// <summary>
	/// Facing the world's forward, the right ear points the world's right. The simplest case, and the
	/// one that says the cross product is the right way round rather than mirrored.
	/// </summary>
	[TestMethod]
	public void FacingForwardPutsTheRightEarOnTheRight()
	{
		var listener = AudioListener.Facing( Vector3.Zero, Vector3.Forward );

		Assert.AreEqual( Vector3.Right.X, listener.Right.X, 1e-5f );
		Assert.AreEqual( Vector3.Right.Y, listener.Right.Y, 1e-5f );
		Assert.AreEqual( Vector3.Right.Z, listener.Right.Z, 1e-5f );
	}

	/// <summary>And turning round swaps the ears over, rather than leaving them where they were.</summary>
	[TestMethod]
	public void TurningRoundSwapsTheEarsOver()
	{
		var listener = AudioListener.Facing( Vector3.Zero, Vector3.Backward );

		Assert.AreEqual( 1f, listener.PanTo( new Vector3( 0, 10, 0 ) ), 1e-5f,
			"facing backward, a sound at +Y should be on the right" );
	}

	/// <summary>
	/// However far the camera is tipped down, the ears stay level. This is what
	/// <see cref="AudioListener.Facing"/> exists for: the lobby camera looks down at its island from a
	/// height, and taking the right ear off the camera's own rotation gave a vector tilted as much as
	/// 0.79 out of the horizontal.
	/// </summary>
	[TestMethod]
	public void TheEarsStayLevelHoweverFarTheCameraLooksDown()
	{
		for ( int degrees = -80; degrees <= 80; degrees += 10 )
		{
			var pitch = degrees * MathF.PI / 180f;
			var forward = new Vector3( MathF.Cos( pitch ), 0, MathF.Sin( pitch ) );
			var listener = AudioListener.Facing( Vector3.Zero, forward );

			Assert.AreEqual( 0f, listener.Right.Z, 1e-5f, $"looking {degrees} degrees off level" );
			Assert.AreEqual( 1f, listener.Right.Length, 1e-5f, $"looking {degrees} degrees off level" );
		}
	}

	/// <summary>
	/// Straight down, there is no level right ear to be had - forward and up are parallel and the
	/// cross product collapses. It must still come back with a usable direction rather than a zero
	/// vector, which would quietly put every sound dead centre.
	/// </summary>
	[TestMethod]
	public void LookingStraightDownStillLeavesAUsableDirection()
	{
		foreach ( var forward in new[] { Vector3.Up, Vector3.Down } )
		{
			var listener = AudioListener.Facing( Vector3.Zero, forward );

			Assert.AreEqual( 1f, listener.Right.Length, 1e-5f, $"looking along {forward}" );
			Assert.IsFalse( float.IsNaN( listener.PanTo( new Vector3( 10, 0, 0 ) ) ) );
		}
	}

	/// <summary>
	/// The lobby's own geometry, at the angle that used to come out mirrored. The camera stands east
	/// of the Space island looking west at it; the antenna's emitter sits a little south of the
	/// island's middle, which from there is on the camera's left.
	/// </summary>
	[TestMethod]
	public void TheSpaceIslandAntennaIsOnTheLeftFromTheEast()
	{
		var camera = new Vector3( 470, 600, 55 );
		var lookAt = new Vector3( 400, 600, 35 );
		var emitter = new Vector3( 400.570f, 589.559f, 40.464f );

		var listener = AudioListener.Facing( camera, (lookAt - camera).Normal );

		Assert.IsTrue( listener.PanTo( emitter ) < 0f,
			$"the antenna should be to the left from there, but panned to {listener.PanTo( emitter )}" );
	}

	/// <summary>A listener with distance switched on, ten units being the point it starts to fade from.</summary>
	private static readonly AudioListener Hearing = new( Vector3.Zero, Vector3.Right, 10f );

	/// <summary>
	/// With no reference distance - which is what the engine starts at - nothing is ever turned down
	/// for being far away. This is what keeps a scene that has not asked for distance exactly as it was.
	/// </summary>
	[TestMethod]
	public void WithNoReferenceDistanceNothingFades()
	{
		Assert.AreEqual( 1f, Listener.AttenuationTo( new Vector3( 10000, 0, 0 ) ), 1e-6f );
		Assert.AreEqual( 1f, Listener.AttenuationTo( Vector3.Zero ), 1e-6f );
	}

	/// <summary>
	/// Inside the reference it holds at 1 rather than climbing. Gains may only ever reduce - the mix
	/// already sums to 1.18 before the master volume - so walking up to a sound must not amplify it.
	/// </summary>
	[TestMethod]
	public void ComingCloserThanTheReferenceNeverMakesItLouder()
	{
		Assert.AreEqual( 1f, Hearing.AttenuationTo( new Vector3( 10, 0, 0 ) ), 1e-6f, "at the reference" );
		Assert.AreEqual( 1f, Hearing.AttenuationTo( new Vector3( 5, 0, 0 ) ), 1e-6f, "half way in" );
		Assert.AreEqual( 1f, Hearing.AttenuationTo( new Vector3( 0.01f, 0, 0 ) ), 1e-6f, "almost on top of it" );
		Assert.AreEqual( 1f, Hearing.AttenuationTo( Vector3.Zero ), 1e-6f, "exactly on the listener" );
	}

	/// <summary>
	/// Amplitude falls as one over the distance, so twice as far is half as loud - 6dB - and four
	/// times as far is a quarter. Getting this wrong by squaring it is the easy mistake, and it would
	/// be 12dB instead.
	/// </summary>
	[TestMethod]
	public void TwiceAsFarIsHalfAsLoud()
	{
		Assert.AreEqual( 0.5f, Hearing.AttenuationTo( new Vector3( 20, 0, 0 ) ), 1e-6f );
		Assert.AreEqual( 0.25f, Hearing.AttenuationTo( new Vector3( 40, 0, 0 ) ), 1e-6f );
		Assert.AreEqual( 0.1f, Hearing.AttenuationTo( new Vector3( 100, 0, 0 ) ), 1e-6f );
	}

	/// <summary>Which way the sound lies says nothing about how far it is.</summary>
	[TestMethod]
	public void TheDirectionItLiesInDoesNotChangeHowFarItIs()
	{
		foreach ( var source in new[]
		{
			new Vector3( 20, 0, 0 ), new Vector3( -20, 0, 0 ), new Vector3( 0, 20, 0 ),
			new Vector3( 0, -20, 0 ), new Vector3( 0, 0, 20 ), new Vector3( 0, 0, -20 )
		} )
		{
			Assert.AreEqual( 0.5f, Hearing.AttenuationTo( source ), 1e-6f, $"from {source}" );
		}
	}

	/// <summary>
	/// The lobby's own numbers. The camera rig stands sqrt(70^2 + 20^2) from the island it shows, so
	/// the antenna - which the camera orbits between 61.3 and 81.8 away from - is at full level over
	/// the near half of the orbit and about a decibel down at the far side.
	/// </summary>
	[TestMethod]
	public void OverTheLobbyOrbitTheAntennaBarelyMoves()
	{
		var lobby = new AudioListener( Vector3.Zero, Vector3.Right, MathF.Sqrt( (70f * 70f) + (20f * 20f) ) );

		Assert.AreEqual( 1f, lobby.AttenuationTo( new Vector3( 61.31f, 0, 0 ) ), 1e-4f, "nearest point of the orbit" );

		var far = lobby.AttenuationTo( new Vector3( 81.75f, 0, 0 ) );
		var decibels = 20f * MathF.Log10( far );

		Assert.IsTrue( decibels < 0f && decibels > -1.5f, $"furthest point came to {decibels:F2}dB" );
	}

	/// <summary>
	/// It never leaves the range the mixer turns into gains. Swept over a box the size of the lobby
	/// around a listener standing where the lobby camera does.
	/// </summary>
	[TestMethod]
	public void ThePanNeverLeavesItsRange()
	{
		var listener = new AudioListener( new Vector3( 400, 600, 55 ), Vector3.Right );

		for ( int x = -200; x <= 200; x += 25 )
		{
			for ( int y = -200; y <= 200; y += 25 )
			{
				for ( int z = -100; z <= 100; z += 25 )
				{
					var pan = listener.PanTo( new Vector3( 400 + x, 600 + y, 55 + z ) );

					Assert.IsFalse( float.IsNaN( pan ), $"({x},{y},{z}) panned to NaN" );
					Assert.IsTrue( pan >= -1f && pan <= 1f, $"({x},{y},{z}) panned to {pan}" );
				}
			}
		}
	}
}
