using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What a switch into and out of first person asks of the people's sprite pool (<see cref="ParkGuestSprites.UseFirstPersonPictures"/>,
/// the camcorder's <c>0x0042af85</c> and <c>0x0042afba</c>): which pack of each bank is read, how many packs the atlas is
/// made of, and the order the new atlas, the models over it and the old atlas's release come in.
///
/// <para>
/// These need no game and no device. The banks are tiny files of the right layout in a folder of their own, mounted as the
/// file system; the atlas and the models go to a stand-in (<see cref="ParkGuestSprites.GuestArt"/>) that records what it was
/// handed. A top view's pack and a first person's differ in the width of their pictures, so the pictures the stand-in is
/// handed say which file was read. That the real atlas and models are made as before is the game's to show.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestSpriteFirstPersonTests
{
	/// <summary>The width of the pictures in a bank's <c>.TPC</c>, and the count.</summary>
	private const int TopWidth = 4, TopCount = 2;

	/// <summary>The same for its <c>.FPC</c>.</summary>
	private const int FirstWidth = 6, FirstCount = 3;

	/// <summary>The width of the <c>.TPC</c> of the balloon bank, whose flag is clear, and of the <c>.FPC</c> that lies beside it all the same.</summary>
	private const int BalloonWidth = 5, BalloonFirstWidth = 7;

	private BaseFileSystem? _was;
	private string _root = "";

	[TestInitialize]
	public void MountABankFolder()
	{
		_was = FileSystemOrNull;
		_root = Directory.CreateTempSubdirectory( "opentpw-firstperson-" ).FullName;
	}

	[TestCleanup]
	public void PutTheFileSystemBack()
	{
		FileSystemOrNull = _was;
		TestRun.DeleteEvery<ParkGuestSprites>();
		Directory.Delete( _root, true );
	}

	/// <summary>
	/// A Lost Kingdom folder with one kids bank that asks for first person's pictures (<c>SPR_BI</c>, bank 0), the balloon bank,
	/// which does not, though a <c>.FPC</c> lies beside its <c>.TPC</c>, and the thought bubbles' folder, empty.
	/// </summary>
	/// <param name="topPack">Whether the kids bank has its <c>.TPC</c>.</param>
	/// <param name="firstPack">Whether it has its <c>.FPC</c>.</param>
	/// <param name="balloons">Whether the balloon bank is in its folder.</param>
	private void Mount( bool topPack = true, bool firstPack = true, bool balloons = true )
	{
		Bank( "esprites/Generic/Kids/SPR_BI", flagged: true, topPack ? (TopWidth, TopCount) : null, firstPack ? (FirstWidth, FirstCount) : null );

		Directory.CreateDirectory( Path.Combine( _root, Balloon.Folder ) );

		if ( balloons )
			Bank( "esprites/Generic/Balloons/SPR_BA", flagged: false, (BalloonWidth, 1), (BalloonFirstWidth, 1) );

		Directory.CreateDirectory( Path.Combine( _root, Thoughts.Folder ) );

		FileSystemOrNull = new BaseFileSystem( _root );
	}

	private void Bank( string path, bool flagged, (int Width, int Count)? topView, (int Width, int Count)? firstPerson )
	{
		var at = Path.Combine( _root, path );
		Directory.CreateDirectory( Path.GetDirectoryName( at )! );

		File.WriteAllBytes( at + ".esp", BankBytes( flagged ) );

		if ( topView is { } top )
			File.WriteAllBytes( at + ".TPC", PackBytes( top.Width, top.Count ) );

		if ( firstPerson is { } first )
			File.WriteAllBytes( at + ".FPC", PackBytes( first.Width, first.Count ) );
	}

	/// <summary>A sprite bank (<c>SpriteBankFile</c>): the magic, a 256-byte name, the first-person flag at <c>0x10C</c>, sixteen sets and four state groups.</summary>
	private static byte[] BankBytes( bool flagged )
	{
		var bytes = new byte[12 + 256 + 2 + (16 * 4) + (4 * 4)];
		System.Text.Encoding.ASCII.GetBytes( "ESP_FILE2.00" ).CopyTo( bytes, 0 );
		bytes[12 + 256] = flagged ? (byte)1 : (byte)0;

		return bytes;
	}

	/// <summary>A picture pack (<c>SpritePackFile</c>, version 3) of <paramref name="count"/> pictures, each <paramref name="width"/> square, one pixel set in each row.</summary>
	private static byte[] PackBytes( int width, int count )
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( (ushort)3 );
		writer.Write( (ushort)0 );
		writer.Write( count );
		writer.Write( new byte[256 * 4] );

		for ( int i = 0; i < count; ++i )
		{
			var rows = new List<byte>();

			for ( int row = 0; row < width; ++row )
				rows.AddRange( [2, 1, 0] );

			writer.Write( rows.Count );
			writer.Write( (ushort)width );
			writer.Write( (ushort)width );
			writer.Write( (ushort)0 );
			writer.Write( (ushort)0 );
			writer.Write( 0 );
			writer.Write( 0 );
			writer.Write( rows.ToArray() );
		}

		return stream.ToArray();
	}

	/// <summary>What the stand-in saw, in the order it saw it.</summary>
	private sealed class Seen
	{
		public readonly List<string> Events = [];
		public readonly List<int[]> Packed = [];

		public ParkGuestSprites.GuestArt Art => new(
			pictures =>
			{
				Packed.Add( pictures.Select( picture => picture.Width ).ToArray() );
				Events.Add( $"pack {Packed.Count}" );

				// One region each, in order; the width is the picture's index, so a region misplaced shows.
				return (new Atlas( this, Packed.Count ), Enumerable.Range( 0, pictures.Count )
					.Select( index => new ParkGuestSprites.Region( 0f, 0f, 1f, 1f, 100 + index, 0, 0, 0 ) ).ToArray());
			},
			( pool, _ ) => Events.Add( $"models over {((Atlas)pool.Atlas!).Number}" ) );
	}

	private sealed class Atlas( Seen seen, int number ) : IPictureAtlas
	{
		public int Number { get; } = number;

		public void Delete() => seen.Events.Add( $"let go {Number}" );
	}

	/// <summary>A park holding the given people, each with a kids' sprite on bank 0, and nothing else the pool reads.</summary>
	private class Seed : DispatchProxy
	{
		public int People;

		protected override object? Invoke( MethodInfo? method, object?[]? args ) => method!.Name switch
		{
			"get_People" => Enumerable.Range( 1, People ).Select( thing => new ParkWorld.Person(
				thing, 0, 0, 0, thing, 0, default!, null ) ).ToList(),
			"get_Sprites" => Enumerable.Range( 1, People ).Select( slot => new ParkWorld.Sprite(
				slot, 0, 0, 0, 0f, 0f, 0f, 0, 0, 0, 0, 0, 0 ) ).ToList(),
			_ => method.ReturnType.IsValueType ? Activator.CreateInstance( method.ReturnType ) : null
		};
	}

	private static IParkInitialState Park( int people )
	{
		var park = DispatchProxy.Create<IParkInitialState, Seed>();
		((Seed)(object)park).People = people;

		return park;
	}

	private ParkGuestSprites Pool( Seen seen, int people = 1 )
	{
		Mount();

		return new ParkGuestSprites( "jungle", Park( people ), null, seen.Art );
	}

	/// <summary>
	/// A park loads with the top view's packs: the kids bank's <c>.TPC</c> (2 pictures of width 4), the balloon bank's
	/// <c>.TPC</c> (1 of width 5) and the white square the dash is drawn from (width 4), in one pack of the atlas and one
	/// building of the models.
	/// </summary>
	/// <remarks><b>Mutations:</b> a park that loads first person's pictures at the start.</remarks>
	[TestMethod]
	public void AParkLoadsTheTopViewsPacks()
	{
		var seen = new Seen();
		var pool = Pool( seen );

		Assert.IsFalse( pool.FirstPerson );
		CollectionAssert.AreEqual( new[] { TopWidth, TopWidth, BalloonWidth, 4 }, seen.Packed.Single() );
		CollectionAssert.AreEqual( new[] { "pack 1", "models over 1" }, seen.Events );
	}

	/// <summary>
	/// Entering first person reads the flagged bank's <c>.FPC</c> (3 pictures of width 6, not the 2 of width 4) and leaves the
	/// unflagged balloon bank on its <c>.TPC</c>, though its <c>.FPC</c> is there to be read: the atlas is packed once more.
	/// </summary>
	/// <remarks><b>Mutations:</b> first person asks for the <c>.TPC</c> still; asks for every bank's <c>.FPC</c> without the flag; the switch packs nothing.</remarks>
	[TestMethod]
	public void EnteringFirstPersonAsksForEachFlaggedBanksFirstPersonPack()
	{
		var seen = new Seen();
		var pool = Pool( seen );

		pool.UseFirstPersonPictures( true );

		Assert.IsTrue( pool.FirstPerson );
		Assert.AreEqual( 2, seen.Packed.Count, "one pack at the load, one at the switch" );
		CollectionAssert.AreEqual( new[] { FirstWidth, FirstWidth, FirstWidth, BalloonWidth, 4 }, seen.Packed[1] );
	}

	/// <summary>Leaving it asks for the <c>.TPC</c>s again: the atlas the park loaded with, packed a third time.</summary>
	/// <remarks><b>Mutations:</b> leaving keeps first person's packs; leaving packs nothing.</remarks>
	[TestMethod]
	public void LeavingFirstPersonAsksForTheTopViewsPacksAgain()
	{
		var seen = new Seen();
		var pool = Pool( seen );

		pool.UseFirstPersonPictures( true );
		pool.UseFirstPersonPictures( false );

		Assert.IsFalse( pool.FirstPerson );
		Assert.AreEqual( 3, seen.Packed.Count );
		CollectionAssert.AreEqual( seen.Packed[0], seen.Packed[2] );
	}

	/// <summary>
	/// The new atlas is made, the models are built over it, and only then is the old atlas let go - a model already built
	/// samples the old one until it is built again. Leaving does the same with the first person's atlas.
	/// </summary>
	/// <remarks><b>Mutations:</b> the old atlas is let go before the new one is packed; before the models are built; never; the new atlas is let go instead.</remarks>
	[TestMethod]
	public void TheOldAtlasGoesAfterTheNewOneAndItsModelsAreMade()
	{
		var seen = new Seen();
		var pool = Pool( seen );
		seen.Events.Clear();

		pool.UseFirstPersonPictures( true );
		pool.UseFirstPersonPictures( false );

		CollectionAssert.AreEqual(
			new[] { "pack 2", "models over 2", "let go 1", "pack 3", "models over 3", "let go 2" }, seen.Events );
		Assert.AreEqual( 3, ((Atlas)pool.Atlas!).Number, "the pool draws from the last pack" );
	}

	/// <summary>
	/// A model already built is built again over the new atlas, as many times as the pool has switched; a pool that has no
	/// people and so built no model builds none (the switch is not what makes the first); a pool whose last person has gone
	/// home still has its model, and builds it again.
	/// </summary>
	/// <remarks><b>Mutations:</b> the models are not built again; they are built whether or not there is a model or a person; only a person (not a model made) makes them again.</remarks>
	[TestMethod]
	public void AModelAlreadyBuiltIsBuiltAgainAndNoneIsBuiltForNobody()
	{
		var crowd = new Seen();
		var pool = Pool( crowd, people: 3 );
		var built = crowd.Events.Count( e => e.StartsWith( "models" ) );

		pool.UseFirstPersonPictures( true );

		Assert.AreEqual( built + 1, crowd.Events.Count( e => e.StartsWith( "models" ) ) );

		TestRun.DeleteEvery<ParkGuestSprites>();

		var empty = new Seen();
		var nobody = new ParkGuestSprites( "jungle", Park( 0 ), null, empty.Art );

		nobody.UseFirstPersonPictures( true );

		Assert.AreEqual( 2, empty.Packed.Count, "the atlas is still packed again" );
		Assert.AreEqual( 0, empty.Events.Count( e => e.StartsWith( "models" ) ), "but nobody is there to be drawn over it" );

		// The last person goes home: the models are built for nobody, and they exist - so the next switch builds them again.
		var left = new Seen();
		var last = new ParkGuestSprites( "jungle", Park( 1 ), null, left.Art );

		Assert.IsTrue( last.Remove( 1 ) );
		var then = left.Events.Count( e => e.StartsWith( "models" ) );

		last.UseFirstPersonPictures( true );

		Assert.AreEqual( then + 1, left.Events.Count( e => e.StartsWith( "models" ) ), "a model that exists is built again, with nobody to draw" );
	}

	/// <summary>
	/// The census counts the banks packed from their <c>.FPC</c>: none for a park's top view, one (the flagged kids bank, not the
	/// balloon bank that has a <c>.FPC</c> beside it) inside first person, none again after leaving.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count ignores the bank's flag; the count ignores the mode.</remarks>
	[TestMethod]
	public void TheCensusCountsOnlyTheFlaggedBanksPackedFromFirstPerson()
	{
		var pool = Pool( new Seen() );

		Assert.AreEqual( 0, pool.PackedFromFirstPerson );

		pool.UseFirstPersonPictures( true );
		Assert.AreEqual( 1, pool.PackedFromFirstPerson );

		pool.UseFirstPersonPictures( false );
		Assert.AreEqual( 0, pool.PackedFromFirstPerson );
	}

	/// <summary>
	/// The regions the atlas answers go where the pool puts them: the white square is the last, and each bank gets the
	/// slice of its own pictures in order. The stand-in's region <c>i</c> has width <c>100 + i</c>: the kids bank (3 pictures in
	/// first person) is regions 0..2, the balloon bank 3, the square 4.
	/// </summary>
	/// <remarks><b>Mutations:</b> the plain region is the first; a bank's slice starts one late or one early.</remarks>
	[TestMethod]
	public void EachBankAndTheWhiteSquareGetTheirOwnRegionsOfThePack()
	{
		var pool = Pool( new Seen() );

		Assert.AreEqual( 103, pool.PlainRegion!.Value.Width, "top view: kids 0..1, balloons 2, square 3" );
		CollectionAssert.AreEqual( new[] { 100, 101 }, pool.PicturesAt( 0, 0 )!.Select( r => r.Width ).ToArray() );
		CollectionAssert.AreEqual( new[] { 102 }, pool.PicturesAt( Balloon.SpriteKind, 0 )!.Select( r => r.Width ).ToArray() );

		pool.UseFirstPersonPictures( true );

		Assert.AreEqual( 104, pool.PlainRegion!.Value.Width );
		CollectionAssert.AreEqual( new[] { 100, 101, 102 }, pool.PicturesAt( 0, 0 )!.Select( r => r.Width ).ToArray() );
		CollectionAssert.AreEqual( new[] { 103 }, pool.PicturesAt( Balloon.SpriteKind, 0 )!.Select( r => r.Width ).ToArray() );
	}

	/// <summary>
	/// A call that changes nothing packs nothing and lets nothing go: the same mode asked for twice, the mode the park loaded in,
	/// and a pool made for no park (the one the game's camcorder finds none of, and a test makes): no theme to read, no park.
	/// </summary>
	/// <remarks><b>Mutations:</b> the same mode packs again; a pool of no park packs or throws. (A pool with a park and no theme cannot be made: the constructor sets both together, so the theme's own test in the guard is not reachable alone.)</remarks>
	[TestMethod]
	public void ACallThatChangesNothingPacksNothing()
	{
		var seen = new Seen();
		var pool = Pool( seen );
		seen.Events.Clear();

		pool.UseFirstPersonPictures( false );

		Assert.AreEqual( 0, seen.Events.Count, "the park loaded in the top view" );

		pool.UseFirstPersonPictures( true );
		var after = seen.Events.Count;
		pool.UseFirstPersonPictures( true );

		Assert.AreEqual( after, seen.Events.Count, "first person asked for twice" );

		var parkless = new ParkGuestSprites( "jungle", null, null, seen.Art );
		parkless.UseFirstPersonPictures( true );

		Assert.IsFalse( parkless.FirstPerson, "no park to draw pictures for" );
		Assert.AreEqual( after, seen.Events.Count );
	}

	/// <summary>
	/// The banks the pool draws from are those of the atlas it has now. A flagged bank whose first-person pack is missing does
	/// not load there, and is not left standing from the top view's: nothing is drawn from a bank of the old atlas.
	/// </summary>
	/// <remarks><b>Mutations:</b> the switch does not clear the banks it had.</remarks>
	[TestMethod]
	public void ABankThatWillNotLoadInFirstPersonIsNotLeftFromTheTopView()
	{
		var seen = new Seen();
		Mount( firstPack: false );
		var pool = new ParkGuestSprites( "jungle", Park( 1 ), null, seen.Art );

		Assert.IsNotNull( pool.BankAt( 0, 0 ), "the top view's pack is there" );

		pool.UseFirstPersonPictures( true );

		Assert.IsNull( pool.BankAt( 0, 0 ), "its first person pack is not" );
		Assert.IsNotNull( pool.BankAt( Balloon.SpriteKind, 0 ), "the balloon bank, which has no first person pack to ask for, is" );
	}

	/// <summary>
	/// When first person's packs hold no picture at all (the kids bank's <c>.FPC</c> is missing and there is no balloon bank), the
	/// atlas is not packed and no model is built over nothing; the top view's atlas is still let go, and the pool is left with none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the models are built with no atlas to build them over.</remarks>
	[TestMethod]
	public void NothingToPackBuildsNoModelAndStillLetsTheOldAtlasGo()
	{
		var seen = new Seen();
		Mount( firstPack: false, balloons: false );
		var pool = new ParkGuestSprites( "jungle", Park( 1 ), null, seen.Art );
		seen.Events.Clear();

		pool.UseFirstPersonPictures( true );

		Assert.IsNull( pool.Atlas );
		CollectionAssert.AreEqual( new[] { "let go 1" }, seen.Events );
	}

	/// <summary>
	/// A pool whose top view packed nothing (no <c>.TPC</c>, no balloon bank) makes its first models when first person is the
	/// first to pack a picture: there is a person to draw, and no model yet.
	/// </summary>
	/// <remarks><b>Mutations:</b> only a model that exists is built again.</remarks>
	[TestMethod]
	public void FirstPersonMakesTheFirstModelsWhenTheTopViewPackedNothing()
	{
		var seen = new Seen();
		Mount( topPack: false, balloons: false );
		var pool = new ParkGuestSprites( "jungle", Park( 1 ), null, seen.Art );

		Assert.AreEqual( 0, seen.Events.Count, "the top view has nothing to pack" );

		pool.UseFirstPersonPictures( true );

		CollectionAssert.AreEqual( new[] { "pack 1", "models over 1" }, seen.Events );
		CollectionAssert.AreEqual( new[] { FirstWidth, FirstWidth, FirstWidth, 4 }, seen.Packed.Single() );
	}
}
