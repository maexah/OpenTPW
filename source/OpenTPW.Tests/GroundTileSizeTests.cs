using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The procedural ground's tile size (<see cref="Level.TileSizeFor"/>): the size the detail file asks, as the original
/// asks it (<c>FUN_00457a90</c>), unless opentpw.cfg's <c>graphics faithful</c> takes the 16 the original cuts it to on any
/// machine over 4 GB (docs/exe/park-engine.md, "The tile size").
/// </summary>
[TestClass]
public class GroundTileSizeTests
{
	/// <summary>
	/// 1, 2 and 3 ask 16, 32 and 64 (<c>0x0044d93e</c> sets bits 4, 2, 1; <c>0x00457ac5</c> reads them in that order),
	/// any other value 32, and 0 turns the compositor off; the original's 16 for every ask but 0.
	/// </summary>
	/// <remarks><b>Mutation:</b> 1 and 3 swapped, the option ignored, the option cutting 0 to 16, or the default 16 for
	/// an ask the switch does not name, each fails a row.</remarks>
	[TestMethod]
	[DataRow( 0, false, 0 )]
	[DataRow( 1, false, 16 )]
	[DataRow( 2, false, 32 )]
	[DataRow( 3, false, 64 )]
	[DataRow( 5, false, 32 )]
	[DataRow( 0, true, 0 )]
	[DataRow( 1, true, 16 )]
	[DataRow( 2, true, 16 )]
	[DataRow( 3, true, 16 )]
	public void TheTileSizeIsTheAskUnlessTheOriginalsIsAsked( int ask, bool original, int expected )
		=> Assert.AreEqual( expected, Level.TileSizeFor( ask, original ) );

	/// <summary>
	/// The shipped detail files, read as a park reads them: low.sam's 0 draws no tiles, med.sam's and high.sam's 2 draw
	/// 32 by default and 16 under <c>graphics faithful</c>. The option is off by default.
	/// </summary>
	/// <remarks><b>Mutation:</b> the wrong key read, the file's value ignored, or the option not consulted fails a row.</remarks>
	[TestMethod]
	public void TheShippedDetailFilesAsk32()
	{
		FileSystem = GameData.Required();
		Log = new();
		var was = GameOptions.Current;

		try
		{
			Assert.IsFalse( new GameOptions().Faithful, "OpenTPW's default" );

			int[] Sizes( bool original )
			{
				var sizes = new int[3];

				for ( var quality = 0; quality < 3; ++quality )
				{
					GameOptions.Current = new() { GraphicsQuality = quality, Faithful = original };
					sizes[quality] = Level.ProceduralTileSize();
				}

				return sizes;
			}

			CollectionAssert.AreEqual( new[] { 0, 32, 32 }, Sizes( false ), "low, medium, high, as asked" );
			CollectionAssert.AreEqual( new[] { 0, 16, 16 }, Sizes( true ), "the original's" );
		}
		finally { GameOptions.Current = was; }
	}

	/// <summary>
	/// The park's ground is made at the size <see cref="Level.ProceduralTileSize"/> gives: a level's one ground-making call
	/// (<see cref="Level.MakeGround"/>) lays its tiles at 32 by default and at 16 under <c>graphics faithful</c>, as
	/// <c>GroundTiles.Make</c> logs it. The ground's texture needs a device, so the call may throw once its tiles are laid;
	/// the log line is read either way.
	/// </summary>
	/// <remarks><b>Mutation:</b> a literal 16 in <see cref="Level.MakeGround"/>, or the tile size ignored by the ground,
	/// fails the first row.</remarks>
	[TestMethod]
	public void TheParksGroundIsLaidAtTheSizeTheOptionsGive()
	{
		FileSystem = GameData.Required();
		Log = new();
		var was = GameOptions.Current;
		var wasLog = Logger.OnLog;
		var lines = new System.Collections.Generic.List<string>();

		Logger.OnLog = ( _, text ) => lines.Add( text );

		try
		{
			foreach ( var (original, pixels) in new[] { (false, 32), (true, 16) } )
			{
				lines.Clear();
				GameOptions.Current = new() { GraphicsQuality = 1, Faithful = original };

				try { Level.MakeGround( "jungle", null, null, true ); }
				catch ( System.Exception ) { /* the device-bound part, after the tiles are laid */ }

				Assert.IsTrue( lines.Any( line => line.Contains( $"tiles of {pixels} pixels" ) ),
					$"original {original}: the ground lays {pixels}-pixel tiles\n{string.Join( '\n', lines )}" );
			}
		}
		finally
		{
			Logger.OnLog = wasLog;
			GameOptions.Current = was;
			Entity.ApplyDeletions();
		}
	}
}
