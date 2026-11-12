using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// A park file's voice list read (FileFormats <c>saves.md</c>, "The voice list"; <c>docs/exe/audio.md</c>, "A park file's
/// voices"): the shipped park's ten as the original puts them back, and the three places its reader stops.
/// </summary>
[TestClass]
public class ParkVoicesTests
{
	/// <summary>
	/// <b><c>Easymode.TPWI</c>'s ten voices, oldest first, with the handles and classes the original's memory held on
	/// entering it</b> (Q560, run b): the music's, the two rectangle voices, the level's three placed one-shots, the gates'
	/// held chain of the jungle ambient's 177 (its first effect) at (450, 0, 160) with its child's variation 8 sample, the
	/// Fountain's 107 and the Litter Bin's 93, and the crowd's kids 91.
	/// </summary>
	[TestMethod]
	public void TheShippedParksTenVoicesReadAsTheOriginalPutsThemBack()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );
		var voices = new ParkWorld( reader.ReadFile(), reader.Preamble ).Voices;

		Assert.IsNull( voices.Problem );
		Assert.IsNull( voices.Stopped );
		Assert.AreEqual( (10, 10, 1), (voices.Count, voices.Saved.Count, voices.Blocks) );
		Assert.AreEqual( 19, voices.Categories.Count, "the shipped file's nineteen slots" );
		CollectionAssert.AreEqual( new[] { 1, 5, 5, 3, 3, 3, 6, 3, 5, 1 }, voices.Saved.Select( voice => voice.Type ).ToArray() );
		CollectionAssert.AreEqual(
			new[] { 0x0126005a, 0x0127005b, 0x0128005f, 0x01290059, 0x012a005d, 0x012b0056, 0x012e0055, 0x01320063, 0x01330061, 0x01fb0060 },
			voices.Saved.Select( voice => voice.Handle ).ToArray() );

		var gates = voices.Saved[6];

		Assert.AreEqual( 0x55, gates.Slot );
		Assert.AreEqual( @"\data\levels\jungle\sound\cat_ambient", voices.CategoryOf( gates.Record )?.ToLowerInvariant() );
		Assert.AreEqual( (450f, 0f, 160f, 0f), (gates.Record.X, gates.Record.Height, gates.Record.Z, gates.Record.Range) );
		Assert.AreEqual( (0, 7, 0), (gates.Record.Effect, gates.Record.Variation, gates.Record.Sample), "177, its eighth variation's first sample" );
		CollectionAssert.AreEqual( new byte[] { 18, 0, 0, 0 }, gates.Record.Keys, "its effect's key" );
		CollectionAssert.AreEqual( new byte[] { 0, 0, 0, 0 }, gates.Record.Values );
		Assert.IsFalse( gates.Record.Rectangle );
		Assert.AreEqual( ParkVoices.HeldChildType, gates.Record.Child?.Type );
		Assert.AreEqual( (7, 0), (gates.Record.Child!.Record.Variation, gates.Record.Child.Record.Sample), "the child's sample variation 8's" );
		Assert.IsNull( gates.Record.Child.Record.Child );

		var rectangle = voices.Saved[1];

		Assert.IsTrue( rectangle.Record.Rectangle, "the global ambient's 2 follows the listener in its rectangle" );
		Assert.AreEqual( @"\data\global\sound\cat_ambient", voices.CategoryOf( rectangle.Record )?.ToLowerInvariant() );
		Assert.AreEqual( (97f, 97f), (voices.Saved[4].Record.Range, voices.Saved[5].Record.Range), "the level's placed 8 and 7" );
		Assert.AreEqual( 45, voices.Saved[7].Record.Effect, "the Fountain's 107, the jungle rides' 46th" );
		Assert.AreEqual( @"\data\levels\jungle\music\cat_music", voices.CategoryOf( voices.Saved[0].Record )?.ToLowerInvariant() );
	}

	/// <summary>
	/// <b>A voice taken out of the shipped park's list leaves the other nine to read whole</b>, the list's length and count
	/// made less by it, and every byte after the module where it was (<see cref="ParkVoices.Without"/>): what a save writes
	/// for a chain put back and let go since. A handle the list does not hold leaves the body as it is.
	/// </summary>
	[TestMethod]
	public void AVoiceTakenOutLeavesTheRestWhole()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );
		var body = reader.ReadFile();
		var before = new ParkVoices( body );
		var gates = before.Saved[6];
		var written = ParkVoices.Without( body, new HashSet<int> { gates.Handle, 0x7777 } );
		var after = new ParkVoices( written );

		Assert.IsNull( after.Problem );
		Assert.IsNull( after.Stopped );
		Assert.AreEqual( (9, 9), (after.Count, after.Saved.Count) );
		CollectionAssert.AreEqual( before.Saved.Where( voice => voice != gates ).Select( voice => voice.Handle ).ToArray(),
			after.Saved.Select( voice => voice.Handle ).ToArray() );
		Assert.AreEqual( before.Saved[7].Record, after.Saved[6].Record with { }, "the Fountain's record read the same after it" );

		var cut = body.Length - written.Length;

		Assert.IsTrue( cut > 100, $"{cut} bytes: the chain's record and its child's" );
		CollectionAssert.AreEqual( body[^200..], written[^200..], "the cheats, the advisor scoring and the tags as they were" );
		Assert.AreSame( body, ParkVoices.Without( body, new HashSet<int> { 0x7777 } ) );
		Assert.IsNotNull( new ParkWorld( written, reader.Preamble ).Voices.Saved.SingleOrDefault( voice => voice.Handle == 0x01fb0060 ) );

		// And the writer leaves out what it is told was let go, and only that.
		var shipped = new ParkWorld( body, reader.Preamble );
		var saved = new ParkWorld( ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0,
			shipped.Camera.Saved!.Value, VoicesLetGo: new HashSet<int> { gates.Handle } ) ), reader.Preamble );
		var kept = new ParkWorld( ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 13, 0,
			shipped.Camera.Saved!.Value ) ), reader.Preamble );

		Assert.AreEqual( (9, 10), (saved.Voices.Saved.Count, kept.Voices.Saved.Count) );
		Assert.IsFalse( saved.Voices.Saved.Any( voice => voice.Handle == gates.Handle ) );
	}

	/// <summary>
	/// <b>A park that ran with no sound device saves without each held chain a script named at the load and none names
	/// now</b> (Q655): nothing was put back, so nothing is let go, and the chain of a Loudspeaker sold since the load would
	/// otherwise come back at a load with sound under a handle nothing names (<see cref="ParkFileWriter.WithoutUnnamedChains"/>).
	/// The shipped park's gates' chain, 177, is named by no record (their <c>EVENT</c> keeps none), so it stays, as a park
	/// with sound keeps it. Made to be held by the Fountain's script as a Loudspeaker's <c>ADDOBJ 4 -1 177 10</c> holds its
	/// own, it stays while the script stands and goes once the script is taken out as a sale takes it; the other nine stay.
	/// </summary>
	[TestMethod]
	public void ASaveWithNoSoundDeviceLeavesOutTheHeldChainsNoScriptNamesAnyMore()
	{
		var data = GameData.Required();

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );
		var body = reader.ReadFile();
		var shipped = new ParkWorld( body, reader.Preamble );
		var gates = shipped.Voices.Saved.Single( voice => voice.Type == ParkVoices.HeldChainType );
		var fountain = shipped.ScriptStates.ByHandle.Values.Single( script => (script.Effects ?? []).Any( effect => effect.Handle == 0x01320063 ) );

		Assert.AreEqual( 0x012e0055, gates.Handle );
		Assert.IsFalse( shipped.ScriptStates.SoundHandles.Contains( gates.Handle ), "the gates' EVENT keeps no record" );
		Assert.IsTrue( shipped.ScriptStates.SoundHandles.Contains( 0x01320063 ), "the Fountain's 107" );
		Assert.IsFalse( shipped.ScriptStates.SoundHandles.Contains( 0x01fb0060 ), "the crowd's voice is no script's" );

		ParkVoices Saved( ParkWorld loaded, bool noDevice ) => new ParkWorld( ParkFileWriter.Body( loaded, new ParkFileWriter.Running(
			loaded.GameTick, false, 13, 0, shipped.Camera.Saved!.Value, VoicesNotPutBack: noDevice ) ), reader.Preamble ).Voices;

		// Never named: the gates' chain stays.
		Assert.AreEqual( 10, Saved( shipped, noDevice: true ).Saved.Count );

		// Named by a standing script's record: it stays.
		var heldBody = shipped.ScriptStates.Splice( body, [], [], new Dictionary<int, SavedEffect[]>
		{
			[fountain.Handle] = [new SavedEffect( 4, gates.Handle, -1, -1, 10 ), .. fountain.Effects!]
		} );
		var held = new ParkWorld( heldBody, reader.Preamble );

		Assert.IsTrue( held.ScriptStates.SoundHandles.Contains( gates.Handle ) );
		Assert.AreEqual( 10, Saved( held, noDevice: true ).Saved.Count );

		// Its script gone, as a sale takes it: the chain goes, and only it.
		var soldBody = held.ScriptStates.Splice( heldBody, [], [fountain.Handle] );

		Assert.AreEqual( 10, new ParkVoices( soldBody ).Saved.Count, "the splice leaves the voice list alone" );

		var without = new ParkVoices( ParkFileWriter.WithoutUnnamedChains( held.ScriptStates.SoundHandles, soldBody ) );

		Assert.IsNull( without.Problem );
		Assert.AreEqual( (9, 9), (without.Count, without.Saved.Count) );
		CollectionAssert.AreEqual( shipped.Voices.Saved.Where( voice => voice != gates ).Select( voice => voice.Handle ).ToArray(),
			without.Saved.Select( voice => voice.Handle ).ToArray() );

		// A park that ran with sound leaves out only what it let go.
		Assert.AreEqual( 10, Saved( held, noDevice: false ).Saved.Count );
	}

	/// <summary>A list of two held chains reads whole; the shape the three refusals below are made from.</summary>
	[TestMethod]
	public void AListOfTwoReadsWhole()
	{
		var voices = new ParkVoices( Body( (0x01000003, 6, true), (0x01000004, 6, false) ) );

		Assert.IsNull( voices.Problem );
		Assert.IsNull( voices.Stopped );
		Assert.AreEqual( 2, voices.Saved.Count );
		Assert.AreEqual( (3, 1, 2, 0), (voices.Saved[0].Record.Category, voices.Saved[0].Record.Effect, voices.Saved[0].Record.Variation, voices.Saved[0].Record.Sample) );
		Assert.AreEqual( (12f, 34f, 56f), (voices.Saved[0].Record.X, voices.Saved[0].Record.Height, voices.Saved[0].Record.Z) );
		Assert.AreEqual( 2, voices.Saved[0].Record.Child?.Record.Variation );
		Assert.IsNull( voices.Saved[1].Record.Child );
		Assert.AreEqual( @"\data\global\sound\cat_kids", voices.CategoryOf( voices.Saved[0].Record ) );
	}

	/// <summary>
	/// <b>A voice of type 10, the class of flag <c>0x100</c>, stops the list there</b>: the original's switch has no case
	/// for it (<c>0x006b6f13</c>), so it reads its handle and type and not its record. The voice before it stands.
	/// </summary>
	[TestMethod]
	public void ATypeTenStopsTheList()
	{
		var voices = new ParkVoices( Body( (0x01000003, 6, true), (0x01000004, 10, false), (0x01000005, 6, false) ) );

		Assert.IsNull( voices.Problem );
		Assert.AreEqual( 1, voices.Saved.Count );
		Assert.AreEqual( (0x01000004, 10), voices.StoppedAtVoice );
		StringAssert.Contains( voices.Stopped, "type 10" );
	}

	/// <summary>
	/// <b>A handle whose slot is taken, or past the pool's blocks, stops the list there</b>: the original finds no free
	/// slot of that number (<c>0x006b6efd</c>) and leaves the record unread.
	/// </summary>
	[DataTestMethod]
	[DataRow( 0x02000003 )]
	[DataRow( 0x01000064 )]
	public void AHandleWithNoFreeSlotStopsTheList( int second )
	{
		var voices = new ParkVoices( Body( (0x01000003, 6, true), (second, 6, false) ) );

		Assert.IsNull( voices.Problem );
		Assert.AreEqual( 1, voices.Saved.Count );
		Assert.AreEqual( (second, 6), voices.StoppedAtVoice );
	}

	/// <summary>A record whose mark is not there (the writer's bare voice of no loaded category) stops the list.</summary>
	[TestMethod]
	public void ARecordWithoutItsMarkStopsTheList()
	{
		var body = Body( (0x01000003, 6, true), (0x01000004, 6, false) );
		var second = LastMark( body );

		body[second] ^= 0xff;

		var voices = new ParkVoices( body );

		Assert.AreEqual( 1, voices.Saved.Count );
		StringAssert.Contains( voices.Stopped, "no mark" );
	}

	/// <summary>A list whose length word does not end it at the module's tag is not taken: no voices.</summary>
	[TestMethod]
	public void AListThatDoesNotEndAtTheTagIsRefused()
	{
		var body = Body( (0x01000003, 6, true) );
		var length = Array.IndexOf( body, (byte)'S', 0 ) + 16 + 84 + 12;

		BitConverter.GetBytes( BitConverter.ToInt32( body, length ) + 4 ).CopyTo( body, length );

		var voices = new ParkVoices( body );

		Assert.IsNotNull( voices.Problem );
		Assert.AreEqual( 0, voices.Saved.Count );
	}

	/// <summary>
	/// A body ending in a sound module of the voices given (handle, type, with a held child or not), each a record of
	/// category slot 3, effect 1, variation 2, sample 3 at (12, 34, 56) with a key, then the cheats and the advisor
	/// scoring's tags.
	/// </summary>
	internal static byte[] Body( params (int Handle, int Type, bool Child)[] voices )
		=> Body( voices.Select( voice => (voice.Handle, voice.Type, voice.Child, 3, 1) ).ToArray() );

	/// <summary>
	/// The same with each voice's category slot and effect place given: slot 1 the global ambient, 2 the global rides,
	/// 3 the kids, 4 the speech, which no script plays in, 5 the staff.
	/// </summary>
	internal static byte[] Body( params (int Handle, int Type, bool Child, int Slot, int Effect)[] voices )
	{
		var list = new MemoryStream();
		var writer = new BinaryWriter( list );
		var names = new[] { "", @"\data\global\sound\cat_ambient", @"\data\global\sound\cat_rides", @"\data\global\sound\cat_kids",
			@"\data\global\speech\cat_speech", @"\data\global\sound\cat_staff" };
		var library = new MemoryStream();
		var libraryWriter = new BinaryWriter( library );

		libraryWriter.Write( names.Length );
		libraryWriter.Write( new byte[4 * names.Length] );

		foreach ( var name in names )
		{
			var bytes = name.Length == 0 ? [] : Encoding.ASCII.GetBytes( name + "\0" );
			libraryWriter.Write( (byte)bytes.Length );
			libraryWriter.Write( bytes );
		}

		writer.Write( voices.Length );
		writer.Write( (int)library.Length - 4 );
		writer.Write( library.ToArray() );
		writer.Write( 1 );

		foreach ( var (handle, type, child, slot, effect) in voices )
		{
			writer.Write( handle );
			writer.Write( type );
			Record( writer, handle, child ? 9 : 0, slot, effect );

			if ( child )
			{
				writer.Write( 9 );
				Record( writer, 0, 0, slot, effect );
			}
		}

		var module = new MemoryStream();
		var output = new BinaryWriter( module );

		output.Write( Encoding.ASCII.GetBytes( "EMAKSVDA" ) );
		output.Write( 1 );
		output.Write( 1 );
		output.Write( 84 );
		output.Write( new byte[84] );
		output.Write( new byte[12] );
		output.Write( (int)list.Length + 4 );
		output.Write( list.ToArray() );
		output.Write( Encoding.ASCII.GetBytes( "NUOS" ) );
		output.Write( new byte[2] );
		output.Write( Encoding.ASCII.GetBytes( "STHC" ) );
		output.Write( 277 );
		output.Write( Encoding.ASCII.GetBytes( "CSDA" ) );
		output.Write( 0 );

		return module.ToArray();
	}

	private static void Record( BinaryWriter writer, int handle, int child, int slot, int effect )
	{
		writer.Write( 0x0012d591 );
		writer.Write( 12f );
		writer.Write( 34f );
		writer.Write( 56f );
		writer.Write( 0f );
		writer.Write( 0f );
		writer.Write( slot );
		writer.Write( effect );
		writer.Write( 2 );
		writer.Write( 0 );
		writer.Write( 0x0012d591 );
		writer.Write( 1 );
		writer.Write( new byte[] { 18, 0, 0, 0, 0, 0, 0, 0 } );
		writer.Write( new byte[16] );
		writer.Write( 0x0012d591 );
		writer.Write( (byte)0 );
		writer.Write( handle );
		writer.Write( child );
	}

	/// <summary>Where the last voice's opening mark stands.</summary>
	private static int LastMark( byte[] body )
	{
		var mark = BitConverter.GetBytes( 0x0012d591 );
		var marks = new List<int>();

		for ( var at = 0; at + 4 <= body.Length; ++at )
		{
			if ( body.AsSpan( at, 4 ).SequenceEqual( mark ) )
				marks.Add( at );
		}

		// Each record holds three marks; the last voice has no child.
		return marks[^3];
	}
}
