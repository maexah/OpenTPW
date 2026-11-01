using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A person's puff (particle <c>0x13</c>) stands where <c>FUN_004fa470</c> puts it: the person's x and z, at the ground's
/// height there, each times 1024 and truncated (<c>docs/exe/park-engine.md</c>, "The three words Q822 left unread", 1).
/// </summary>
[TestClass]
public class PersonPuffsTests
{
	private HeightfieldFile _field = null!;
	private ParticleSystem _system = null!;

	[TestInitialize]
	public void MakeTheSystem()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		using var terrain = FileSystem.OpenRead( "levels/jungle/terrain/base.MD2" )!;
		_field = new HeightfieldFile( terrain );
		_system = new ParticleSystem( "Particle/Tp2.plb", 1000 );
		GameClock.Rebase();
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutItAway()
	{
		PersonPuffs.Ground = null;
		_system?.Shutdown();
		Unimplemented.Forget();
	}

	/// <summary>
	/// <b>The place is x, the ground's height, z, each times 1024 truncated</b>: the second row stands off a cell corner on
	/// ground that is not level, where the height read at the cell's corner, the sprite's own nought, or x and z swapped
	/// each give another word.
	/// </summary>
	/// <remarks><b>Mutations:</b> the height nought; the height of the cell's corner; x and z swapped; the factor 1000.</remarks>
	[TestMethod]
	[DataRow( 40.0, 58.0 )]
	[DataRow( 31.37, 62.81 )]
	[DataRow( 12.5, 20.25 )]
	public void APuffStandsAtTheFeetOnTheGround( double cellX, double cellY )
	{
		var at = new FixedVector( (int)(cellX * FixedVector.One), (int)(cellY * FixedVector.One) );
		var x = at.X / (float)FixedVector.One * _field.CellSizeX;
		var z = at.Y / (float)FixedVector.One * _field.CellSizeY;
		var height = _field.ScapeHeight( x, z );

		var place = PersonPuffs.PlaceOn( _field, at );

		Assert.AreEqual( ((int)(x * 1024f), (int)(height * 1024f), (int)(z * 1024f)), place );
		Assert.AreNotEqual( place.X, place.Z, "x and z are different words, so a swap shows" );
	}

	/// <summary>
	/// <b>Started, it is one emitter of effect 19 at that place</b>, in the particle system's own units (the words over 16).
	/// </summary>
	/// <remarks><b>Mutations:</b> another effect; no start; started twice.</remarks>
	[TestMethod]
	public void StartingOneMakesAnEmitterOfEffectNineteenThere()
	{
		var at = new FixedVector( (int)(31.37 * FixedVector.One), (int)(62.81 * FixedVector.One) );
		var (x, height, z) = PersonPuffs.PlaceOn( _field, at );

		var handle = PersonPuffs.Start( _system, _field, at );

		Assert.AreNotEqual( 0, handle );
		Assert.IsTrue( _system.TryEmitter( handle, out var emitter ) );
		Assert.AreEqual( (0x13, x >> 4, height >> 4, z >> 4), (emitter.Effect, emitter.X, emitter.Y, emitter.Z) );
		Assert.AreEqual( 1, _system.Emitters.Count( e => e.Active ), "one, and nothing else started" );
	}

	/// <summary>No system, or no ground, starts nothing and counts nothing.</summary>
	[TestMethod]
	public void WithNoSystemOrNoGroundNothingStarts()
	{
		var at = new FixedVector( 40 * FixedVector.One, 58 * FixedVector.One );

		Assert.AreEqual( 0, PersonPuffs.Start( null, _field, at ) );
		Assert.AreEqual( 0, PersonPuffs.Start( _system, null, at ) );
		Assert.AreEqual( 0, _system.Emitters.Count( e => e.Active ) );
	}
}
