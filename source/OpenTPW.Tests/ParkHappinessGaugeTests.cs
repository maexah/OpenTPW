using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// The number behind the management gadget's happiness gauge, <see cref="ParkPeople.GuestsInsideHappiness"/>, and the
/// count behind the park's music, <see cref="ParkPeople.GateGuestCensus"/>: the original's <c>FUN_004c7bb0</c> and
/// <c>FUN_004c7fa0</c>, which both take a guest only where <c>FUN_004fa990</c> passes (the cell under them is of type
/// 0, 1, 3, 9 or 10, <see cref="Peep.CountsOn"/>) - Q148.
///
/// <para>
/// <b>The shipped park is a poor witness on its own.</b> Every guest is saved at exactly fifty and every one stands
/// on the bus stop's type 30 cell, so a test that only read it would pass against a count that took nobody and a mean
/// that took any rule. So the guests are moved onto paths one way or another and given a total whose mean is 50.54,
/// where a float mean and a whole one disagree in the gauge's units (517 and 511): the original divides with
/// <c>FILD</c>/<c>FIDIV</c> (<c>0x004c7c96</c>), which is a float division.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkHappinessGaugeTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkPeople Stock( ParkWorld world, out ParkState state )
	{
		state = new ParkState( world );
		return new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state );
	}

	/// <summary>Makes the cell each of these guests stands on a path, so the original's filter passes them.</summary>
	private static void PutOnPaths( ParkState state, IEnumerable<Peep> guests )
	{
		foreach ( var (x, y) in guests.Select( peep => peep.Navigator.Position.Cell ).Distinct() )
			state.SetRecord( x, y, state.Record( x, y ) with { Type = 1 } );
	}

	/// <summary>
	/// The shipped park: thirteen guests at fifty, none yet off the bus stop. The gauge reads nought and the music
	/// level nought for them (thirteen halved is six, which the count of every guest gave); put on paths, the gauge
	/// reads fifty (511 of 1024) and the music six.
	/// </summary>
	/// <remarks><b>Mutations:</b> the music handed every guest; the gauge's mean taken over every guest; the filter passing every cell.</remarks>
	[TestMethod]
	public void TheGuestsAtTheBusStopAreNotCountedUntilTheyStandOnACountingCell()
	{
		var people = Stock( Park(), out var state );

		Assert.AreEqual( 13, people.Peeps.Count, "the shipped park's guests" );
		Assert.IsTrue( people.Peeps.All( peep => peep.Happiness == 50f ), "and every one of them is saved at fifty" );
		Assert.AreEqual( 0, people.GateGuestCensus, "none stands on a cell that passes FUN_004fa990" );
		Assert.AreEqual( 0f, people.GuestsInsideHappiness, "so the gauge's mean has nobody to take" );
		Assert.AreEqual( 0, ParkGadget.GaugeUnits( people.GuestsInsideHappiness ) );
		Assert.AreEqual( 0, ParkAudio.MusicLevelFor( people, 0 ), "and the music has nobody to count" );

		PutOnPaths( state, people.Peeps );

		Assert.AreEqual( 13, people.GateGuestCensus );
		Assert.AreEqual( 50f, people.GuestsInsideHappiness );
		Assert.AreEqual( 511, ParkGadget.GaugeUnits( people.GuestsInsideHappiness ), "fifty is 511 of 1024 (docs/exe/hud.md)" );
		Assert.AreEqual( 6, ParkAudio.MusicLevelFor( people, 0 ), "thirteen halved, truncated" );
		Assert.AreEqual( 0, ParkAudio.MusicLevelFor( people, ParkAudio.SilentWorldState ), "and nought while the world's state is 4" );
	}

	/// <summary>
	/// <b>The one that can actually fail.</b> Twelve guests at fifty and one at fifty-seven is 657 over 13, 50.54: the
	/// gauge's units are 517 where the whole mean the gauge used to be given, fifty, gives 511. Then a guest at a
	/// hundred left off the paths, which neither the mean nor the music's count takes: twelve at fifty read 511 and a
	/// level of six.
	/// </summary>
	/// <remarks><b>Mutations:</b> the mean cut to a whole number; the mean of every guest; the count of every guest.</remarks>
	[TestMethod]
	public void TheMeanIsOfTheCountedGuestsAndIsNotWhole()
	{
		var people = Stock( Park(), out var state );
		var guests = people.Peeps.ToList();

		PutOnPaths( state, guests );

		foreach ( var peep in guests )
			peep.Happiness = 50f;

		guests[0].Happiness = 57f;

		Assert.AreEqual( 13, people.GateGuestCensus );
		Assert.AreEqual( 517, ParkGadget.GaugeUnits( people.GuestsInsideHappiness ),
			"657 over thirteen is 50.54, which the gauge scales to 517 of 1024, not the 511 of fifty" );

		// One guest alone on her cell is put back on a cell that does not count, and made the happiest there is.
		var cells = guests.Select( peep => peep.Navigator.Position.Cell ).ToList();
		var left = guests.First( peep => cells.Count( cell => cell == peep.Navigator.Position.Cell ) == 1 );
		var (lx, ly) = left.Navigator.Position.Cell;
		state.SetRecord( lx, ly, state.Record( lx, ly ) with { Type = 30 } );

		foreach ( var peep in guests )
			peep.Happiness = 50f;

		left.Happiness = 100f;

		Assert.AreEqual( 12, people.GateGuestCensus, "twelve are on paths" );
		Assert.AreEqual( 50f, people.GuestsInsideHappiness, "and the one at a hundred is not in their mean" );
		Assert.AreEqual( 6, ParkAudio.MusicLevelFor( people, 0 ), "nor in the music's count of twelve" );
	}

	/// <summary>A park with nobody in it reads nought, and so does a shut one with guests on its paths.</summary>
	/// <remarks><b>This one would pass with the feature deleted</b>: it pins the empty and shut cases, not the arithmetic.</remarks>
	[TestMethod]
	public void NobodyInTheParkReadsNoughtAndSoDoesAShutPark()
	{
		var empty = new ParkPeople( null );

		Assert.AreEqual( 0, empty.Peeps.Count, "a null park has no guests to average" );
		Assert.AreEqual( 0f, empty.GuestsInsideHappiness, "so the gauge rests at the bottom" );
		Assert.AreEqual( 0, ParkAudio.MusicLevelFor( empty, 0 ) );
		Assert.AreEqual( 0, ParkAudio.MusicLevelFor( null, 0 ), "and no park at all is silent" );

		var shut = Stock( Park(), out var state );
		PutOnPaths( state, shut.Peeps );
		state.SetParkClosed( true );

		Assert.AreEqual( 13, shut.GateGuestCensus, "the guests still stand inside" );
		Assert.AreEqual( 0f, shut.GuestsInsideHappiness, "the shut park reads nought (FUN_0051a280)" );
		Assert.AreEqual( 6, ParkAudio.MusicLevelFor( shut, 0 ), "and the music does not ask whether the park is shut" );
	}
}
