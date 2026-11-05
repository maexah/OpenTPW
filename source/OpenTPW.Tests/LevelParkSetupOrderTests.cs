using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// A park's set-up runs the particle system first, then the entities, then the HUD (<see cref="Level.SetUpPark"/>): the
/// entities step reads <see cref="ParticleSystem.Current"/> for the catalogue's effects, the placed objects' emitters and the
/// park file's density, and with none made it would quietly run without particles (Q1030, from Q211).
///
/// <para>
/// <b>What these cannot see.</b> No test builds a park's <see cref="Level"/>, which needs a graphics device, so the steps are
/// handed in. That the constructor hands in the right three, in the right places, rests on the game run (the log's
/// <c>Particles: ... density</c> line, which <see cref="Level.TakeParticleDensity"/> writes only when the system exists).
/// </para>
/// </summary>
[TestClass]
public class LevelParkSetupOrderTests
{
	[TestInitialize]
	public void StartWithNoSystem()
	{
		Log ??= new();
		ParticleSystem.Current?.Shutdown();
	}

	[TestCleanup]
	public void PutTheSystemAway() => ParticleSystem.Current?.Shutdown();

	[TestMethod]
	public void TheStepsRunParticlesThenEntitiesThenHud()
	{
		var ran = new List<string>();

		Level.SetUpPark( () => ran.Add( "particles" ), () => ran.Add( "entities" ), () => ran.Add( "hud" ) );

		CollectionAssert.AreEqual( new[] { "particles", "entities", "hud" }, ran );
	}

	[TestMethod]
	public void TheEntitiesStepStartsWithTheRealSystemMade()
	{
		FileSystem = GameData.Required();

		Assert.IsNull( ParticleSystem.Current, "the test starts with no system" );

		bool? madeWhenEntitiesStarted = null;
		int effects = 0;
		int density = 0;

		Level.SetUpPark(
			Level.SetupParticles,
			() =>
			{
				madeWhenEntitiesStarted = ParticleSystem.Current != null;
				effects = ParticleSystem.Current?.Library.Effects.Length ?? 0;
				density = ParticleSystem.Current?.Density ?? 0;
			},
			() => { } );

		Assert.AreEqual( true, madeWhenEntitiesStarted, "ParticleSystem.Current where the entities step starts" );
		Assert.IsTrue( effects > 0, "the library the entities step registers its effects from holds effects" );
		Assert.IsTrue( density > 0, "the density the park file's replaces is the options'" );
	}
}
