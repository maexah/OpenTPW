using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// What <see cref="Level"/> hands from one part to another, tested at <c>Level</c>'s own call: a helper's test that sets the
/// value by hand cannot see the hand-over taken out (Q211). Skipped without the game (<see cref="GameData"/>).
/// </summary>
/// <remarks>
/// <b>Mutations:</b> the park HUD's <c>Locator</c> not taken from its front end's gadget (Q444); <c>state.RandomSeed</c> not
/// handed to <see cref="ParkFileWriter.Running"/> (Q443).
/// </remarks>
[TestClass]
public class LevelHandOverTests
{
	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private Level? _level;
	private BaseFileSystem? _oldSaves;
	private string? _root;

	[TestInitialize]
	public void Prepare()
	{
		_level = Level.Current;
		Log ??= new();
		FileSystem = GameData.Required();
		Unimplemented.Forget();
	}

	[TestCleanup]
	public void PutAway()
	{
		Level.Current = _level!;
		TestRun.DeleteEvery<ParkPeople>();

		if ( _root != null )
		{
			typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, null );
			SaveFileSystemOrNull = _oldSaves;
			Directory.Delete( _root, true );
			_root = null;
		}

		Unimplemented.Forget();
	}

	/// <summary>
	/// <b>The park's HUD gives the level the gadget's locator</b> (Q444): <c>Level.Locator</c>, which the keys and the world
	/// click read (<c>Level.LocatorOpen</c>), is the one of the <see cref="ParkFrontEnd"/> the HUD holds, not left empty. The HUD's
	/// last step, the screen effects, needs a graphics device and stops the build here after the hand-over.
	/// </summary>
	[TestMethod]
	public void TheParkHudHandsTheLevelTheGadgetsLocator()
	{
		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.ThemeName ) )!.SetValue( level, Theme );

		try
		{
			typeof( Level ).GetMethod( "SetupParkHud", BindingFlags.NonPublic | BindingFlags.Instance )!.Invoke( level, null );
		}
		catch ( TargetInvocationException e ) when ( e.InnerException is NullReferenceException )
		{
			// The on-screen effects, built after the hand-over, make a texture and there is no graphics device here.
		}

		var front = level.Hud.Children.OfType<ParkFrontEnd>().Single();

		Assert.IsNotNull( level.Locator, "the level has a locator" );
		Assert.AreSame( front.Gadget.Locator, level.Locator );
	}

	/// <summary>
	/// <b>The park's generator is written as <c>mRandomSeed</c> by <see cref="Level.WritePark"/></b> (Q443): after a draw
	/// the file holds the state the park stands on, not the shipped file's seed that <see cref="ParkFileWriter"/> keeps where it
	/// is handed none.
	/// </summary>
	[TestMethod]
	public void TheWrittenParkHoldsTheGeneratorItStandsOn()
	{
		var data = (BaseFileSystem)FileSystem;
		var shipped = Read( data.ReadAllBytes( ShippedPark ) );
		var people = new ParkPeople( shipped );
		var state = people.State;

		_root = Directory.CreateTempSubdirectory( "opentpw-handover-" ).FullName;
		var jungle = Path.Combine( _root, "users", "1Test", Theme );
		Directory.CreateDirectory( jungle );
		_oldSaves = SaveFileSystemOrNull;
		SaveFileSystem = new BaseFileSystem( _root );
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		state.Draw();
		state.Draw();

		Assert.AreNotEqual( unchecked((uint)shipped.RandomSeed), state.RandomSeed, "the draws moved the generator" );
		Assert.IsNotNull( Level.WritePark( shipped, state, Theme, "Seeded", people ), "the park is written" );

		var written = Read( File.ReadAllBytes( Path.Combine( jungle, "Seeded.TPWS" ) ) );

		Assert.IsNull( written.Problem );
		Assert.AreEqual( unchecked((int)state.RandomSeed), written.RandomSeed );
	}

	/// <summary>
	/// <b>A park read in part is not written and the player is told</b> (Q1200): <see cref="Level.WritePark"/> over a file whose body
	/// stops halfway answers null, writes nothing, and leaves PARK NOT SAVED waiting in the refusal's own words, not the folder's.
	/// </summary>
	[TestMethod]
	public void AParkReadInPartIsNotWrittenAndSaysParkNotSaved()
	{
		var data = (BaseFileSystem)FileSystem;
		var file = data.ReadAllBytes( ShippedPark );
		var shipped = Read( file );
		var people = new ParkPeople( shipped );

		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		var body = reader.ReadFile();
		var cut = new ParkWorld( body[..(body.Length / 2)], reader.Preamble );

		Assert.IsNotNull( cut.Problem, "the cut body is read in part" );

		_root = Directory.CreateTempSubdirectory( "opentpw-handover-" ).FullName;
		var jungle = Path.Combine( _root, "users", "1Test", Theme );
		Directory.CreateDirectory( jungle );
		_oldSaves = SaveFileSystemOrNull;
		SaveFileSystem = new BaseFileSystem( _root );
		typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

		while ( SaveTrouble.Pending )
			SaveTrouble.Take();

		SaveTrouble.NewVisit();

		try
		{
			Assert.IsNull( Level.WritePark( cut, people.State, Theme, "Cut", people ), "the park is refused" );
			Assert.IsFalse( File.Exists( Path.Combine( jungle, "Cut.TPWS" ) ) );
			Assert.IsTrue( SaveTrouble.Pending, "the player is told" );
			Assert.AreEqual( $"PARK NOT SAVED\n\n{SaveTrouble.ParkRefusedWhy}", SaveTrouble.Take() );
		}
		finally
		{
			while ( SaveTrouble.Pending )
				SaveTrouble.Take();

			SaveTrouble.NewVisit();
		}
	}

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}
}
