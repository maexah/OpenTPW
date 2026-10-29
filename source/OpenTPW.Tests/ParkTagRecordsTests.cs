using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The aerial's tags in a park file (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the aerial's tags"): the block
/// that ends the body, a tag's item and text, the order a file is written in and what a load puts back. The
/// first four need no game; the last two read the shipped park and are skipped where there is no installation.
/// </summary>
[TestClass]
public class ParkTagRecordsTests
{
	[TestInitialize]
	public void ALog() => Log ??= new();

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( one => one.What == what ).Times;

	/// <summary>A record laid down by hand at the listing's offsets, with the writer's stack after the text and in the pads.</summary>
	private static byte[] Laid( int id, string text, int life, short picture, int button1, int button2, ushort thing, int item )
	{
		var record = Enumerable.Repeat( (byte)0xcd, 0x21c ).ToArray();

		BinaryPrimitives.WriteInt32LittleEndian( record, id );
		Encoding.Unicode.GetBytes( text + "\0" ).CopyTo( record, 4 );
		BinaryPrimitives.WriteInt32LittleEndian( record.AsSpan( 0x204 ), life );
		BinaryPrimitives.WriteInt16LittleEndian( record.AsSpan( 0x208 ), picture );
		BinaryPrimitives.WriteInt32LittleEndian( record.AsSpan( 0x20c ), button1 );
		BinaryPrimitives.WriteInt32LittleEndian( record.AsSpan( 0x210 ), button2 );
		BinaryPrimitives.WriteUInt16LittleEndian( record.AsSpan( 0x214 ), thing );
		BinaryPrimitives.WriteInt32LittleEndian( record.AsSpan( 0x218 ), item );
		return record;
	}

	/// <summary>A body's end as a file has it: the cheats' tag, the advisor scoring's dword and tag, then the block.</summary>
	private static byte[] Body( params byte[][] records )
		=> [.. "an earlier module, CSDA and all"u8, .. "STHC"u8, 0x15, 1, 0, 0, .. "CSDA"u8, .. BitConverter.GetBytes( records.Length ), .. records.SelectMany( record => record )];

	/// <summary>
	/// <b>The block is a count and 0x21c bytes a tag, after the last module's tag</b> (<c>FUN_0048ee50</c>): the id,
	/// the text to its terminator, the life at 0x204, the picture at 0x208, the buttons at 0x20c and 0x210, the
	/// thing at 0x214 and the item at 0x218; what follows the terminator and the pads are not read.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> each field read from its neighbour's place; the text read past its terminator; the thing
	/// read signed; another record size; a <c>CSDA</c> with no cheats' tag before it taken; a count that does not
	/// end the body accepted.
	/// </remarks>
	[TestMethod]
	public void ABlockIsReadByTheListingsOffsets()
	{
		var read = new ParkTagRecords( Body(
			Laid( 72, "Your Belly Bounce ride", 30000, 1, 3, 0, 13, 1100 ),
			Laid( 94, "cheap", 90000, 2, 24, 27, 0xfff0, -1 ) ) );

		Assert.IsNull( read.Problem );
		CollectionAssert.AreEqual( new[]
		{
			new ParkTagRecords.Record( 72, "Your Belly Bounce ride", 30000, 1, 3, 0, 13, 1100 ),
			new ParkTagRecords.Record( 94, "cheap", 90000, 2, 24, 27, 0xfff0, -1 )
		}, read.Saved.ToArray() );

		Assert.AreEqual( 0, new ParkTagRecords( Body() ).Saved.Count );
		Assert.IsNull( new ParkTagRecords( Body() ).Problem );

		// A count whose records do not end the body is no block, and neither is a body with no tag before it.
		var short1 = Body( Laid( 72, "a", 1, 1, 0, 0, 1, 1 ) )[..^1];
		Assert.IsNotNull( new ParkTagRecords( short1 ).Problem );
		Assert.AreEqual( 0, new ParkTagRecords( short1 ).Saved.Count );
		Assert.IsNotNull( new ParkTagRecords( new byte[64] ).Problem );

		Assert.IsNotNull( new ParkTagRecords( [.. Body( Laid( 72, "a", 1, 1, 0, 0, 1, 1 ) ), 0, 0, 0] ).Problem, "bytes after the last record" );
		Assert.IsNotNull( new ParkTagRecords( [.. "CSDA"u8, 0, 0, 0, 0] ).Problem, "the advisor scoring's tag alone" );

		var eleven = Body( [.. Enumerable.Range( 0, 11 ).Select( n => Laid( n, "a", 1, 1, 0, 0, 1, 1 ) )] );
		Assert.IsNotNull( new ParkTagRecords( eleven ).Problem, "the aerial holds ten" );

		Assert.AreEqual( 0x21c, ParkTagRecords.RecordSize );
		Assert.AreEqual( 31 + 12 + 4 + (2 * 0x21c), Body( Laid( 1, "a", 1, 1, 0, 0, 1, 1 ), Laid( 2, "b", 1, 1, 0, 0, 1, 1 ) ).Length );

		// An earlier CSDA and a count of two, a record's length before the block: no cheats' tag stands before
		// it, so it is passed over for the block that has one.
		byte[] decoyed = [.. "8 before"u8, .. "CSDA"u8, 2, 0, 0, 0, .. new byte[0x21c - 4 - 12], .. "STHC"u8, 0x15, 1, 0, 0, .. "CSDA"u8, 1, 0, 0, 0,
			.. Laid( 94, "cheap", 90000, 2, 24, 27, 7, 9 )];
		Assert.AreEqual( new ParkTagRecords.Record( 94, "cheap", 90000, 2, 24, 27, 7, 9 ), new ParkTagRecords( decoyed ).Saved.Single() );
	}

	/// <summary>
	/// <b>The writer lays the same record, with nought where the original leaves its stack</b>, in place of the
	/// block the body ends on, whatever its length was; the body before it is untouched; a text is cut to 255
	/// characters, and more than ten tags are refused.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the old block kept and the new put after it; the count not written; a field at its
	/// neighbour's place; the text not terminated; no cut at 255; eleven written.
	/// </remarks>
	[TestMethod]
	public void ABlockIsWrittenInPlaceOfTheOneTheBodyEndsOn()
	{
		var body = Body( Laid( 72, "old", 30000, 1, 3, 0, 13, 1100 ) );
		var head = body.Length - 4 - ParkTagRecords.RecordSize;
		var tags = new[]
		{
			new ParkTagRecords.Record( 150, "Your park is now open.", 30000, 1, 0, 0, 0, 0 ),
			new ParkTagRecords.Record( 72, new string( 'x', 300 ), 90000, 5, 3, 27, 43, 1140 )
		};

		var written = ParkTagRecords.Put( body, tags );

		Assert.AreEqual( head + 4 + (2 * ParkTagRecords.RecordSize), written.Length );
		CollectionAssert.AreEqual( body[..head], written[..head] );
		Assert.AreEqual( 2, BitConverter.ToInt32( written, head ) );

		var first = written.AsSpan( head + 4, ParkTagRecords.RecordSize );
		CollectionAssert.AreEqual( Laid( 150, "Your park is now open.", 30000, 1, 0, 0, 0, 0 ).Select( b => b == 0xcd ? (byte)0 : b ).ToArray(),
			first.ToArray(), "the listing's record, nought where it writes nothing" );

		var again = new ParkTagRecords( written );
		Assert.IsNull( again.Problem );
		Assert.AreEqual( tags[0], again.Saved[0] );
		Assert.AreEqual( tags[1] with { Text = new string( 'x', 255 ) }, again.Saved[1] );

		Assert.AreEqual( head + 4, ParkTagRecords.Put( written, [] ).Length, "and none is the count alone" );
		Assert.ThrowsException<InvalidOperationException>( () => ParkTagRecords.Put( body, [.. Enumerable.Repeat( tags[0], 11 )] ) );
		Assert.ThrowsException<InvalidOperationException>( () => ParkTagRecords.Put( new byte[64], tags ) );
	}

	private static readonly StringPart[][] Strings =
	[
		[new( "Your park is now open.", 0 )],
		[new( "Your ", 0 ), new( null, 0 ), new( " ride has broken down.", 0 ), new( null, 1 )],
	];

	/// <summary>
	/// <b>A tag's text is written as it is made, by its row's kind</b> (<c>FUN_00509e10</c>): kind 0 the string
	/// alone, kind 2 the string with the item's name where its parameter 0 stands; and <b>a standing tag is the new
	/// one's key only about the same thing and item</b> (<c>FUN_0048eb20</c>), one that is its own key taking the
	/// new text.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the name left out; the name at every parameter; kind 0 given the name; a kind 2 about no
	/// item written; another kind uncounted; the item not kept; a tag of another item taken off; the own-key tag's
	/// text not replaced.
	/// </remarks>
	[TestMethod]
	public void ATagKeepsItsItemAndItsText()
	{
		var rows = new[]
		{
			new ParkTags.Row( 150, 151, 1, 0, 0, 0, 30000, 0 ), new ParkTags.Row( 68, ParkTags.NoTag, 1, 1, 3, 0, 30000, 2 ),
			new ParkTags.Row( 72, 68, 1, 1, 3, 0, 30000, 2 ), new ParkTags.Row( 9, 9, 1, 1, 0, 0, 0, 2 ),
			new ParkTags.Row( 5, 5, 1, 1, 0, 0, 0, 9 ), new ParkTags.Row( 6, 6, 1, 0, 0, 0, 0, 0 )
		};
		var tags = new ParkTags( rows ) { Strings = Strings };
		var kinds = Times( "TAG_TEXT_KIND" );

		var door = tags.Said( 150, 0, 0, 0, "Belly Bounce" )!;
		Assert.AreEqual( (0, "Your park is now open."), (door.Item, door.Text) );

		var worn = tags.Said( 68, 13, 0, 1100, "Belly Bounce" )!;
		Assert.AreEqual( (13, 1100, "Your Belly Bounce ride has broken down."), (worn.Subject, worn.Item, worn.Text) );
		Assert.AreEqual( kinds, Times( "TAG_TEXT_KIND" ) );

		// Thing 13 as another item: the mechanic's tag is not that tag's.
		tags.Said( 72, 13, 0, 1140, "The Hot Pot" );
		CollectionAssert.AreEqual( new[] { 150, 68, 72 }, tags.Standing.Select( tag => tag.Row.Id ).ToArray() );
		tags.Said( 72, 13, 0, 1100, "Belly Bounce" );
		CollectionAssert.AreEqual( new[] { (150, 0), (72, 1140), (72, 1100) }, tags.Standing.Select( tag => (tag.Row.Id, tag.Item) ).ToArray() );

		// Its own key: the standing tag takes the new text.
		var own = tags.Said( 9, 20, 0, 1140, "The Hot Pot" )!;
		Assert.IsNull( tags.Said( 9, 20, 0, 1140, "Hot Pot" ) );
		Assert.AreEqual( "Your Hot Pot ride has broken down.", own.Text );
		Assert.IsNotNull( tags.Said( 9, 20, 0, 1100, "Belly Bounce" ), "another item's is new" );

		// A kind 2 about no item, and a kind this does not write: counted, empty.
		Assert.AreEqual( string.Empty, tags.Said( 68, 0, 0 )!.Text );
		Assert.AreEqual( kinds + 1, Times( "TAG_TEXT_KIND" ) );
		Assert.AreEqual( string.Empty, tags.Said( 5, 1, 0, 1100, "Belly Bounce" )!.Text );
		Assert.AreEqual( kinds + 2, Times( "TAG_TEXT_KIND" ) );

		// With no strings every text is empty and nothing is counted.
		Assert.AreEqual( string.Empty, new ParkTags( rows ).Said( 5, 1, 0, 1100, "Belly Bounce" )!.Text );
		Assert.AreEqual( string.Empty, new ParkTags( rows ) { Strings = [] }.Said( 150, 0, 0 )!.Text );
		Assert.AreEqual( kinds + 2, Times( "TAG_TEXT_KIND" ) );
		Assert.AreEqual( "Your park is now open.", tags.Said( 6, 0, 0, 1100, "Belly Bounce" )!.Text );
		var plain = new ParkTags( [new ParkTags.Row( 6, 6, 1, 1, 0, 0, 0, 0 )] ) { Strings = Strings };
		Assert.AreEqual( "Your  ride has broken down.", plain.Said( 6, 0, 0, 1100, "Belly Bounce" )!.Text, "kind 0 takes no name" );
	}

	/// <summary>
	/// <b>A file holds every tag standing, in the order of the list's ten records, each with its whole life</b>
	/// (<c>FUN_0048ee50</c>), and <b>a load puts each back as a new tag with that life before it</b>
	/// (<c>FUN_0048ef70</c>): its key is 0x17f, so it takes no standing tag's place; its picture, life and buttons
	/// are the file's, whatever rows this list holds; and a later line still takes it off by its id.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the mast's order written; a hidden tag left out; what is left of the life written; the
	/// life not started again; the restored tag keyed on its own id; the file's picture dropped for a row's; the
	/// restore uncounted in <c>Made</c>; an eleventh restored; the item or the thing dropped.
	/// </remarks>
	[TestMethod]
	public void TheStandingTagsAreWrittenAndPutBack()
	{
		var tags = new ParkTags( [new ParkTags.Row( 150, 151, 1, 0, 0, 0, 30000, 0 ), new ParkTags.Row( 151, 150, 1, 0, 0, 0, 30000, 0 ),
			new ParkTags.Row( 68, ParkTags.NoTag, 1, 1, 3, 0, 30000, 2 )] ) { Strings = Strings };
		var first = tags.Said( 150, 0, 1000 )!;
		tags.Said( 68, 13, 2000, 1100, "Belly Bounce" );
		tags.Hide( first, 3000 );
		tags.Tick( 3500 );
		tags.Said( 68, 14, 4000, 1140, "The Hot Pot" );
		var hidden = tags.Said( 151, 0, 5000 )!;
		tags.Hide( hidden, 5100 );

		// The third took the first record, which the first had left; the mast has it above the second.
		CollectionAssert.AreEqual( new[] { (68, 13, 1), (68, 14, 0), (151, 0, 2) },
			tags.Standing.Select( tag => (tag.Row.Id, tag.Subject, tag.Slot) ).ToArray() );

		var written = tags.Written();
		CollectionAssert.AreEqual( new[]
		{
			new ParkTagRecords.Record( 68, "", 30000, 1, 3, 0, 14, 1140 ),
			new ParkTagRecords.Record( 68, "", 30000, 1, 3, 0, 13, 1100 ),
			new ParkTagRecords.Record( 151, "", 30000, 1, 0, 0, 0, 0 )
		}, written.Select( record => record with { Text = "" } ).ToArray() );
		Assert.AreEqual( "Your The Hot Pot ride has broken down.", written[0].Text );

		// Into a list that holds none of these rows, with one tag standing already.
		var loaded = new ParkTags( [new ParkTags.Row( 7, ParkTags.NoTag, 0, 0, 0, 0, 0, 0 ), new ParkTags.Row( 72, 68, 1, 1, 3, 0, 30000, 2 )] );
		loaded.Said( 7, 14, 0, 1140 );
		loaded.Restore( [.. written, new ParkTagRecords.Record( 94, "cheap", 90000, 2, 24, 0, 0, 0 )], 7000 );

		Assert.AreEqual( (5, 5), (loaded.Standing.Count, loaded.Made) );
		CollectionAssert.AreEqual( new[] { (7, 14, 1140, 0, ""), (68, 14, 1140, 1, "Your The Hot Pot ride has broken down."), (68, 13, 1100, 2, "Your Belly Bounce ride has broken down."),
			(151, 0, 0, 3, "Your park is now open."), (94, 0, 0, 4, "cheap") },
			loaded.Standing.Select( tag => (tag.Row.Id, tag.Subject, tag.Item, tag.Slot, tag.Text) ).ToArray() );
		Assert.IsTrue( loaded.Standing.Skip( 1 ).All( tag => tag.Born == 7000 && !tag.Hidden ) );
		CollectionAssert.AreEqual( new[] { 37000L, 37000L, 37000L, 97000L }, loaded.Standing.Skip( 1 ).Select( tag => tag.HideAt ).ToArray() );
		CollectionAssert.AreEqual( new[] { (2, 24, 0) }, loaded.Standing.Skip( 4 ).Select( tag => (tag.Row.Picture, tag.Row.Button1, tag.Row.Button2) ).ToArray() );

		// Written again it is the record read.
		CollectionAssert.AreEqual( written.ToArray(), loaded.Written().Skip( 1 ).Take( 3 ).ToArray() );

		// The mechanic's line about the Hot Pot takes the restored tag of its key, thing and item, and no other.
		loaded.Said( 72, 14, 8000, 1140 );
		CollectionAssert.AreEqual( new[] { (7, 14), (68, 13), (151, 0), (94, 0), (72, 14) }, loaded.Standing.Select( tag => (tag.Row.Id, tag.Subject) ).ToArray() );

		// Ten is the list's most, a file's tags included.
		var full = new ParkTags();
		full.Restore( [.. Enumerable.Repeat( written[0], 12 )], 0 );
		Assert.AreEqual( 10, full.Standing.Count );

		loaded.Tick( 37000 );
		Assert.AreEqual( 2, loaded.Standing.Count( tag => tag.Hidden ), "thirty seconds from the load, not from the line" );
	}

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private static void SetCurrentPlayer( Player? player )
		=> typeof( Players ).GetProperty( nameof( Players.Current ) )!.SetValue( Players.Roster, player );

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	/// <summary>
	/// <b>The park file's writer ends the body on the tags it is handed</b>, the rest of the body as it was, and
	/// leaves the file's own block where it is handed none; the shipped park holds none.
	/// </summary>
	/// <remarks><b>Mutations:</b> the tags not handed to the block's writer; written with none handed over; the file's block not read.</remarks>
	[TestMethod]
	public void TheWriterEndsTheBodyOnTheTags()
	{
		FileSystem = GameData.Required();
		var shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );

		Assert.IsNull( shipped.Tags.Problem );
		Assert.AreEqual( 0, shipped.Tags.Saved.Count );

		var running = new ParkFileWriter.Running( 1234, false, 77, 54321, new ParkCameraModule.View( 95f, 1.5f, 300f, 250f ) );
		var tags = new[] { new ParkTagRecords.Record( 72, "Your Belly Bounce ride", 30000, 1, 3, 0, 13, 1100 ) };

		var plain = ParkFileWriter.Body( shipped, running );
		var tagged = ParkFileWriter.Body( shipped, running with { Tags = tags } );

		Assert.AreEqual( plain.Length + ParkTagRecords.RecordSize, tagged.Length );
		CollectionAssert.AreEqual( plain[..^4], tagged[..(plain.Length - 4)] );
		CollectionAssert.AreEqual( tags, new ParkWorld( tagged, null ).Tags.Saved.ToArray() );
		Assert.IsNull( new ParkWorld( tagged, null ).Problem, "and the world before it reads as it did" );

		// A file that holds one, written with none handed over, keeps it; with an empty list it holds none.
		var holding = new ParkWorld( tagged, null );
		CollectionAssert.AreEqual( tags, new ParkWorld( ParkFileWriter.Body( holding, running ), null ).Tags.Saved.ToArray() );
		Assert.AreEqual( 0, new ParkWorld( ParkFileWriter.Body( holding, running with { Tags = [] } ), null ).Tags.Saved.Count );
	}

	/// <summary>
	/// <b>The level writes the running park's tags and a park made from the file has them back</b>: a line's tag
	/// about the Belly Bounce is in the file with its item and text, nothing is counted, and a state seeded from
	/// that file holds the tag with its whole life from the moment it is made.
	/// </summary>
	/// <remarks><b>Mutations:</b> the level handing the writer no tags; the state not restoring; SAVE_PARK_TAGS still counted.</remarks>
	[TestMethod]
	public void TheLevelWritesTheTagsAndAParkMadeFromTheFileHoldsThem()
	{
		Unimplemented.Forget();
		FileSystem = GameData.Required();
		var shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		var oldSaves = SaveFileSystemOrNull;
		var root = Directory.CreateTempSubdirectory( "opentpw-park-tags-" ).FullName;
		var jungle = Path.Combine( root, "users", "1Test", "jungle" );

		try
		{
			Directory.CreateDirectory( jungle );
			SaveFileSystem = new BaseFileSystem( root );
			SetCurrentPlayer( new Player( 0, "Test", new PlayerFile { InstantAction = true } ) );

			var state = new ParkState( shipped );
			Assert.AreEqual( 0, state.Tags.Standing.Count );
			state.Tags.Strings = new StringFile( ParkTags.StringsPath ).Parts;
			state.Tags.Said( 72, 13, Time.WallMilliseconds - 20000, 1100, "Belly Bounce" );

			Assert.IsNotNull( Level.WritePark( shipped, state, "jungle", "Tagged" ) );
			Assert.AreEqual( 0, Times( "SAVE_PARK_TAGS" ) );

			var written = Read( File.ReadAllBytes( Path.Combine( jungle, "Tagged.TPWS" ) ) );
			CollectionAssert.AreEqual( new[] { new ParkTagRecords.Record( 72,
				"Your Belly Bounce ride has broken down, and a mechanic is on his way to fix it.", 30000, 1, 3, 0, 13, 1100 ) },
				written.Tags.Saved.ToArray() );

			Time.PinWall( 123456 );
			var loaded = new ParkState( written );
			var tag = loaded.Tags.Standing.Single();

			Assert.AreEqual( (72, 13, 1100, written.Tags.Saved[0].Text), (tag.Row.Id, tag.Subject, tag.Item, tag.Text) );
			Assert.AreEqual( (123456L, 153456L), (tag.Born, tag.HideAt), "its whole life, from the load" );
		}
		finally
		{
			Time.PinWall( null );
			SetCurrentPlayer( null );
			SaveFileSystemOrNull = oldSaves;
			Directory.Delete( root, true );
			ParkOrbitCameraMode.Forget();
			Unimplemented.Forget();
		}
	}
}
