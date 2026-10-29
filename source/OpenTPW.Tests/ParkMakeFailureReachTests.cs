using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The reach of a make that fails after it took its thing id (Q615): <see cref="ParkBuilding.Build"/> past
/// <c>PlaceNow</c> and <see cref="ParkEndFeature.Make"/> past <c>BindFixed</c>. Every Lost Kingdom item is put through the
/// script load <c>ParkRides.Load</c> makes, which is what <c>BindFixed</c> answers for the End. Build's return is another
/// matter: it is <c>PlaceNow</c>'s answer, which a script that will not run cannot reach (<c>BindOperation</c> discards
/// <c>BindNew</c>'s), and no test pins it. That half rests on the running game's census of <c>ParkObjects.Place</c>
/// (<c>q615/run8</c>, 61 built, none refused), which needs a graphics device.
/// </summary>
[TestClass]
public class ParkMakeFailureReachTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>
	/// Every item a buy list shows (UI type 0 to 3, as <see cref="ParkBuilding"/> admits) has a script that loads, the
	/// load <c>ParkRides.Load</c> makes (open, valid, constructed): so the End, the one item
	/// <see cref="ParkRides.BindFixed"/> is asked for, cannot reach its "script would not run" return. This says nothing
	/// of <see cref="ParkBuilding.Build"/>'s return, which is <c>PlaceNow</c>'s (the class summary).
	/// </summary>
	[TestMethod]
	public void EveryBuyableItemsScriptReads()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var failures = new List<string>();
		var seen = 0;

		foreach ( var item in catalogue.All.Where( i => i.UiType is >= 0 and <= ItemDescriptionFile.Feature ) )
		{
			++seen;

			if ( !ScriptLoads( item ) )
				failures.Add( $"{item.Name} ({item.Id})" );
		}

		Console.WriteLine( "Q615 ids: " + string.Join( " ", catalogue.All.Where( i => i.UiType is >= 0 and <= ItemDescriptionFile.Feature ).Select( i => i.Id ).Order() ) );
		Console.WriteLine( $"Q615: {seen} buyable items, {failures.Count} scripts that do not read" );
		Assert.AreEqual( 61, seen, "Lost Kingdom's buy list is 61 items in Full Simulation" );
		Assert.AreEqual( 0, failures.Count, string.Join( "; ", failures ) );
	}

	/// <summary>What <c>ParkRides.Load</c> does inside its try: open the file, read it valid, build the script.</summary>
	private bool ScriptLoads( ParkItemCatalogue.Item item )
	{
		try
		{
			using var stream = data.OpenReadOrNull( ParkRides.ScriptPathFor( item ) );

			return stream != null && new RideScriptFile( stream ) is { IsValid: true } file && new RideScript( file ) != null;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>The End's script reads: the one <see cref="ParkRides.BindFixed"/> is asked for.</summary>
	[TestMethod]
	public void TheEndsScriptReads()
	{
		var end = ParkEndFeature.Find( new ParkItemCatalogue( "jungle", data ) )!;

		Assert.IsTrue( ScriptLoads( end.Value ), "the End is in the catalogue and its script loads" );
	}
}
