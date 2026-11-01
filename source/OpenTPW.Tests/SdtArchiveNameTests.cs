using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// <see cref="SdtArchive.GetFile"/> by name: a name longer than the field is stored cut to fifteen characters, so the
/// whole name finds the cut one, and a shorter name finds only an equal one.
/// </summary>
[TestClass]
public class SdtArchiveNameTests
{
	/// <summary>A bank of entries with the given names, each holding a one-byte sound of its own index.</summary>
	private static SdtArchive Bank( params string[] names )
	{
		const int headerSize = 40;
		var body = new MemoryStream();
		var writer = new BinaryWriter( body );
		var first = 4 + 4 * names.Length;

		writer.Write( names.Length );
		for ( var i = 0; i < names.Length; i++ )
			writer.Write( first + i * (headerSize + 1) );

		for ( var i = 0; i < names.Length; i++ )
		{
			var field = new byte[16];
			Encoding.ASCII.GetBytes( names[i] ).CopyTo( field, 0 );

			writer.Write( headerSize );
			writer.Write( 1 );
			writer.Write( field );
			writer.Write( (ushort)22050 );
			writer.Write( (byte)16 );
			writer.Write( (byte)36 );
			writer.Write( 0 );
			writer.Write( 0 );
			writer.Write( 0 );
			writer.Write( (byte)i );
		}

		return new SdtArchive( new MemoryStream( body.ToArray() ) );
	}

	private static int? Found( SdtArchive bank, string name )
	{
		var file = bank.GetFile( name );
		return file == null ? null : bank.soundFiles.IndexOf( (MP2File)file );
	}

	[TestMethod]
	public void AWholeNameFindsItsCutEntryAndANearMissFindsNothing()
	{
		using var bank = Bank( "TP SCREECH 11.m", "TP BEAST 10.mp2", "TP BEAST 1.mp2" );

		Assert.AreEqual( 0, Found( bank, "TP SCREECH 11.mp2" ), "the whole name finds the entry cut to fifteen" );
		Assert.AreEqual( 0, Found( bank, "tp screech 11.MP2" ), "in any case" );
		Assert.AreEqual( 0, Found( bank, "TP SCREECH 11.m" ), "the stored name finds itself" );
		Assert.IsNull( Found( bank, "TP SCREECH 1" ), "a shorter name is not the cut one" );
		Assert.AreEqual( 1, Found( bank, "TP BEAST 10.mp2" ), "a whole name of fifteen" );
		Assert.AreEqual( 2, Found( bank, "TP BEAST 1.mp2" ) );
		Assert.IsNull( Found( bank, "TP BEAST 1.mp" ), "a prefix of a name that was not cut finds nothing" );
		Assert.IsNull( Found( bank, "TP BEAST 1.mp2 copy" ), "only a name that fills its field is a cut one" );
		using var opened = new MemoryStream();
		bank.OpenFile( "TP SCREECH 11.mp2" )!.CopyTo( opened );
		CollectionAssert.AreEqual( bank.soundFiles[0].GetData(), opened.ToArray(), "OpenFile goes through the same match" );
	}

	/// <summary>
	/// Every entry of Lost Kingdom's two ambient banks is found by its own stored name, and every one that fills its
	/// field is found by any longer name that begins with it (eighteen entries: four in LobbySfxHD.sdt, fourteen in
	/// AmbientHD.sdt). Where two entries share a stored name the first is found.
	/// </summary>
	[TestMethod]
	public void LostKingdomsAmbientBanksAreFoundByWholeAndCutNames()
	{
		GameData.Required();

		var cut = 0;
		foreach ( var bankName in new[] { "LobbySfxHD.sdt", "AmbientHD.sdt" } )
		{
			var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
			var folder = Directory.EnumerateDirectories( Path.Combine( GameDir.Data, "levels", "jungle" ), "sound", options ).Single();
			using var bank = new SdtArchive( Directory.EnumerateFiles( folder, bankName, options ).Single() );

			foreach ( var entry in bank.soundFiles )
			{
				var expected = bank.soundFiles.First( other => other.Name == entry.Name );
				Assert.AreSame( expected, bank.GetFile( entry.Name ), $"{bankName}: '{entry.Name}'" );

				if ( entry.Name.Length != SdtArchive.StoredNameLength )
					continue;

				cut++;
				Assert.AreSame( expected, bank.GetFile( entry.Name + "p2" ), $"{bankName}: '{entry.Name}p2'" );
				var shorter = (MP2File?)bank.GetFile( entry.Name[..^1] );
				Assert.IsTrue( shorter == null || shorter.Name == entry.Name[..^1],
					$"{bankName}: '{entry.Name[..^1]}' finds only an entry of that name, not '{shorter?.Name}'" );
			}
		}

		Assert.AreEqual( 18, cut );
	}
}
