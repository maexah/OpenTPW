using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using static OpenTPW.Common.GlobalNamespace;

namespace OpenTPW.Tests;

/// <summary>
/// How many channels a saved model has, by the item its record names (<c>docs/exe/saves.md</c>, "How many channels a saved
/// model has"): a coaster's pylon type five, which the walk of the model module needs to step over its record.
/// </summary>
[TestClass]
public class ParkPylonChannelsTests
{
	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	/// <summary>The Minecart's pylons and car, which Alexah's played jungle park holds 40 and 24 records of (<c>minecart.wad</c>'s <c>coaster.sam</c>).</summary>
	private const int StdPylon = 1181, LoopPylon = 1182, Cart = 1183;

	private BaseFileSystem data = null!;

	[TestInitialize]
	public void Start()
	{
		Log ??= new();
		data = FileSystem = GameData.Required();
	}

	[TestMethod]
	public void TheMinecartsPylonsHoldFiveChannelsAndItsCartOne()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );

		Assert.IsFalse( catalogue.TryGet( StdPylon, out _ ), "a pylon type is no catalogue item" );
		Assert.AreEqual( 5, catalogue.ChannelsFor( StdPylon ) );
		Assert.AreEqual( 5, catalogue.ChannelsFor( LoopPylon ) );
		Assert.AreEqual( 1, catalogue.ChannelsFor( Cart ), "a coaster's car holds one" );
		Assert.AreEqual( 1, catalogue.ChannelsFor( 99999 ), "an id nothing names holds one" );
	}

	/// <summary>An item's own count is still its <c>NumSimultAnims</c>: the Jungle Spray runs three lanes.</summary>
	[TestMethod]
	public void AnItemKeepsItsOwnCount()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var item = catalogue.All.First( candidate => candidate.AnimationChannels == 3 );

		Assert.AreEqual( 3, catalogue.ChannelsFor( item.Id ) );
	}

	/// <summary>
	/// The twelve coasters of the four themes name 21 pylon types between them (<c>lib/modelchannels.py</c> counted them from
	/// every <c>.sam</c>), each outside its catalogue's items and each five.
	/// </summary>
	[TestMethod]
	public void TheFourThemesNameTwentyOnePylonTypes()
	{
		var types = new System.Collections.Generic.HashSet<int>();

		foreach ( var theme in Themes )
		{
			var catalogue = new ParkItemCatalogue( theme, data );

			foreach ( var item in catalogue.All.Where( item => data.OpenReadOrNull( $"{item.Directory}/coaster.sam" ) != null ) )
			{
				using var stream = data.OpenRead( $"{item.Directory}/coaster.sam" );
				var sam = new SettingsFile( stream );

				for ( var index = 0; index < 2; ++index )
				{
					if ( !int.TryParse( sam[$"asPylonControls[{index}].uiPylonTypeUniqueID"], out var id ) )
						continue;

					types.Add( id );
					Assert.IsFalse( catalogue.TryGet( id, out _ ), $"pylon type {id} is no item" );
					Assert.AreEqual( 5, catalogue.ChannelsFor( id ), $"pylon type {id}" );
				}
			}
		}

		Assert.AreEqual( 21, types.Count );
	}

	/// <summary>
	/// A module holding a pylon's record, five channels each told apart by its role, then a cart's: the catalogue's count
	/// walks it to the module's end and reads every channel, where the item-only count the walk had stops at the first
	/// record (it takes the pylon as one and lands in the middle of its channels).
	/// </summary>
	[TestMethod]
	public void AModuleWithAPylonWalksToItsEndOnlyWithFiveChannels()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );

		var pylon = ParkThingStates.MadeRecord( StdPylon, 30, 40, 1, 1, 0, 0, 0f, 0,
			[.. Enumerable.Range( 0, 5 ).Select( index => Channel( index + 1 ) )] );
		var cart = ParkThingStates.MadeRecord( Cart, 31, 41, 1, 1, 0, 0, 0f, 0, [Channel( 7 )] );

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( Encoding.ASCII.GetBytes( "SYSG" ) );
		writer.Write( 12 + pylon.Length + cart.Length );
		writer.Write( 2 );
		writer.Write( 0 );
		writer.Write( 2 );
		writer.Write( pylon );
		writer.Write( cart );
		writer.Flush();

		var whole = new ParkThingStates( stream.ToArray(), catalogue.ChannelsFor );

		Assert.IsNull( whole.Problem );
		Assert.IsTrue( whole.ClosedOnEnd );
		CollectionAssert.AreEqual( new[] { 1, 2, 3, 4, 5 }, whole.Things[0].Channels.Select( channel => channel.Role ).ToArray() );
		CollectionAssert.AreEqual( new[] { 7 }, whole.Things[1].Channels.Select( channel => channel.Role ).ToArray() );

		var oldRule = new ParkThingStates( stream.ToArray(), id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

		Assert.IsNotNull( oldRule.Problem, "walked with a pylon's one channel the module does not close" );
	}

	/// <summary>
	/// The stock jungle park with a pylon's record of five channels added to the end of its model module: what a played park
	/// with a coaster holds among its records. The walk each caller makes of it closes only with the catalogue's count.
	/// </summary>
	private (ParkWorld World, ParkItemCatalogue Catalogue, ParkWorld Stock) WithAPylon()
	{
		var catalogue = new ParkItemCatalogue( "jungle", data );
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var payload = new SaveReader( stream ).ReadFile();
		var pylon = ParkThingStates.MadeRecord( StdPylon, 30, 40, 1, 1, 0, 0, 0f, 0,
			[.. Enumerable.Range( 0, 5 ).Select( index => Channel( index + 1 ) )] );

		var module = payload.AsSpan().IndexOf( "SYSG"u8 ) + 4;
		var length = BitConverter.ToInt32( payload, module );
		var end = module + 4 + length;
		var spliced = payload[..end].Concat( pylon ).Concat( payload[end..] ).ToArray();

		BitConverter.GetBytes( length + pylon.Length ).CopyTo( spliced, module );
		BitConverter.GetBytes( BitConverter.ToInt32( payload, module + 4 ) + 1 ).CopyTo( spliced, module + 8 );

		var oldRule = new ParkThingStates( spliced, id => catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

		Assert.IsNotNull( oldRule.Problem, "the pylon's record is what the item-only count cannot walk" );
		Assert.IsNull( new ParkThingStates( spliced, catalogue.ChannelsFor ).Problem );

		return (new ParkWorld( spliced ), catalogue, new ParkWorld( payload ));
	}

	/// <summary>The boundary wall's pieces of a file that holds a pylon: all 143, where the walk that fails makes none (Q811).</summary>
	[TestMethod]
	public void TheBoundaryWallReadsAParkThatHoldsAPylon()
	{
		var (world, catalogue, stock) = WithAPylon();

		Assert.AreEqual( 143, ParkBoundaryHoarding.Read( stock, catalogue ).Count );
		Assert.AreEqual( 143, ParkBoundaryHoarding.Read( world, catalogue ).Count );
	}

	/// <summary>The rides' pairing of a thing with its saved model: the stock park's 14, whether or not a pylon's record follows.</summary>
	[TestMethod]
	public void TheRidesPairTheirSavedModelsInAParkThatHoldsAPylon()
	{
		var (world, catalogue, stock) = WithAPylon();
		var rides = new ParkRides( "jungle", stock, catalogue, data );

		var paired = rides.PairSavedThings( stock, catalogue ).Count;

		Assert.IsTrue( paired > 0 );
		Assert.AreEqual( paired, rides.PairSavedThings( world, catalogue ).Count );
	}

	/// <summary>The objects' read of the model module warns "Hoarding restore" when it will not read; a park holding a pylon does not.</summary>
	[TestMethod]
	public void TheObjectsRestoreTheirHoardingsInAParkThatHoldsAPylon()
	{
		var (world, catalogue, _) = WithAPylon();
		var warnings = new System.Collections.Generic.List<string>();

		void Hear( Logger.Level severity, string text )
		{
			if ( text.Contains( "Hoarding restore" ) )
				warnings.Add( text );
		}

		Logger.OnLog += Hear;

		try
		{
			_ = new ParkObjects( "jungle", world, catalogue );
		}
		finally
		{
			Logger.OnLog -= Hear;
		}

		CollectionAssert.AreEqual( Array.Empty<string>(), warnings );
	}

	private static SavedChannel Channel( int role )
		=> new( role, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 0f );
}
