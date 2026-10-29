using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// A park's own view is drawn with no fog, as the original's: its terrain and objects are fogged only in first person
/// and a ride's view (docs/exe/park-engine.md, "The park view's colours"; first person's is FirstPersonFogTests). The
/// lobby keeps its haze.
/// </summary>
[TestClass]
public class ParkViewFogTests
{
	private Level? _level;

	[TestInitialize]
	public void Remember()
	{
		Log ??= new();
		_level = Level.Current;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkCamcorderCameraMode.Forget();
		Camera.SetCameraMode<CameraMode>();
		Level.Current = _level;
	}

	/// <remarks><b>Mutation:</b> a park given the lobby's haze, or the test against <c>Scene.Lobby</c>, fails here.</remarks>
	[TestMethod]
	public void AParkIsDrawnWithNoFog()
	{
		Assert.AreEqual( 0f, Level.FogDensityFor( Level.Scene.Park ) );
	}

	/// <remarks><b>Mutation:</b> no fog anywhere, or none outside a scene, fails here.</remarks>
	[TestMethod]
	public void TheLobbyKeepsItsHaze()
	{
		Assert.AreEqual( 0.025f, Level.FogDensityFor( Level.Scene.Lobby ) );
		Assert.AreEqual( 0.025f, Level.FogDensityFor( null ) );
	}

	/// <summary>What every draw reads is the running scene's.</summary>
	/// <remarks><b>Mutation:</b> <see cref="Level.FogDensity"/> answering the lobby's haze whatever the scene fails here.</remarks>
	[TestMethod]
	public void TheDrawsReadTheRunningScenesFog()
	{
		Level.Current = Scene( Level.Scene.Park );
		Assert.AreEqual( 0f, Level.FogDensity );

		Level.Current = Scene( Level.Scene.Lobby );
		Assert.AreEqual( 0.025f, Level.FogDensity );
	}

	private static Level Scene( Level.Scene kind )
	{
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( level, kind );
		return level;
	}
}
