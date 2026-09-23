using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// The three mood changes are read from their own keys - <c>PeepInfo.SmallHappinessChange</c>,
/// <c>MediumHappinessChange</c> and <c>BigHappinessChange</c>, the globals at <c>0x00785058</c>,
/// <c>0x0078505c</c> and <c>0x00785060</c>.
///
/// <para>
/// <b>Why a balance file of its own.</b> Each fallback is the shipped file's value, so the shipped game cannot
/// tell a misspelt key from a right one. A file whose three values match no fallback can. Needs no game.
/// </para>
/// </summary>
[TestClass]
public class ParkMoodChangeTests
{
	[TestMethod]
	public void EachMoodChangeIsReadFromItsOwnKey()
	{
		Log ??= new();

		var before = FileSystem;
		var root = Directory.CreateTempSubdirectory( "opentpw-mood-" );

		try
		{
			Directory.CreateDirectory( Path.Combine( root.FullName, "levels" ) );
			File.WriteAllText( Path.Combine( root.FullName, "levels", "Standard.sam" ),
				"PeepInfo.SmallHappinessChange 7\nPeepInfo.MediumHappinessChange 17\nPeepInfo.BigHappinessChange 27\n" );

			FileSystem = new BaseFileSystem( root.FullName );

			var admission = new ParkAdmission( new ParkBalance( "jungle" ), fee: 0 );

			Assert.AreEqual( 7, admission.SmallHappinessChange, "PeepInfo.SmallHappinessChange" );
			Assert.AreEqual( 17, admission.MediumHappinessChange, "PeepInfo.MediumHappinessChange" );
			Assert.AreEqual( 27, admission.BigHappinessChange, "PeepInfo.BigHappinessChange" );
		}
		finally
		{
			FileSystem = before;
			root.Delete( recursive: true );
		}
	}
}
