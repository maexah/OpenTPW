using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The ride ids a player bought with golden tickets, at the file's end (<c>docs/exe/saves.md</c>, the "ride ids" rows):
/// a dword count and a word for each.
/// </summary>
[TestClass]
public class PlayerFileRideIdsTests
{
	/// <summary>
	/// A `gms.dat` the original wrote (Alexah's Full Simulation profile, 2026-09-22): tickets spent 1, keys given 1, one
	/// ride bought, Jurassic Tours (1170, <c>0x0492</c>), as its last six bytes <c>01 00 00 00 92 04</c>.
	/// </summary>
	private static readonly byte[] Original = Convert.FromBase64String(
		"DAAAAAEAAAAAAAEAAAABAAAAAAEABAAAAAcAAABmYW50YXN5AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAYAAABoYWxsb3cAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABgAAAGp1bmdsZQEBAQAAAAFqAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFAAAAc3BhY2UAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAEsAAAABAAAAPAAAAAEAAABLAAAAAQAAAGQAAAABAQEBAAEBAQAAAJIE" );

	private static byte[] Written( PlayerFile file )
	{
		using var stream = new MemoryStream();
		file.Write( stream );
		return stream.ToArray();
	}

	/// <summary>
	/// The original's file reads to its spent count and its one ride id, and writes back to the same bytes: the ids come
	/// out as a count and little-endian words, in the order held.
	/// </summary>
	/// <remarks><b>Mutations:</b> the ids written as dwords; the count not written; the ids read as bytes; the spent count dropped.</remarks>
	[TestMethod]
	public void TheOriginalsRideIdsReadAndWriteBackAsTheyWere()
	{
		using var input = new MemoryStream( Original );
		var file = PlayerFile.Read( input );

		Assert.IsTrue( file.ReadWhole );
		Assert.AreEqual( (1, 1), (file.TicketsSpent, file.KeysGiven) );
		CollectionAssert.AreEqual( new ushort[] { 1170 }, file.RideIds );
		CollectionAssert.AreEqual( Original, Written( file ) );
		CollectionAssert.AreEqual( new byte[] { 1, 0, 0, 0, 0x92, 0x04 }, Written( file )[^6..] );
	}

	/// <summary>A purchase's ids, as <see cref="GoldenTickets.SpendForRide"/> leaves them, come back through a write and a read.</summary>
	/// <remarks><b>Mutations:</b> the ids written in another order than held; the spent count not written.</remarks>
	[TestMethod]
	public void ARideBoughtWithTicketsComesBackFromTheFile()
	{
		var file = new PlayerFile { RecordTickets = [1, 1, 1, 1], TicketsSpent = 2 };
		var tours = new ParkItemCatalogue.Item( 1170, "Tours", "", "ride", 0, 1, null, GoldenTicketCost: 1 );
		Assert.IsTrue( GoldenTickets.SpendForRide( file, tours, 2 ) );

		var bytes = Written( file );
		CollectionAssert.AreEqual( new byte[] { 1, 0, 0, 0, 0x92, 0x04 }, bytes[^6..] );

		using var input = new MemoryStream( bytes );
		var back = PlayerFile.Read( input );

		Assert.AreEqual( 3, back.TicketsSpent );
		CollectionAssert.AreEqual( new ushort[] { 1170 }, back.RideIds );
	}
}
