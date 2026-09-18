using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a ride script's variables sit, and why the common names are a list to look up rather than a
/// layout to index with.
///
/// <para>
/// <b>For a ride script the enum's values ARE its indices</b> - all six checked here declare the same
/// twelve names first and in the same order, then append their own. What makes a fixed index unsafe is
/// the other scripts: a ride's archive can hold companions, and those declare none of the common set.
/// <c>child.RSE</c> in <c>monkey.wad</c> declares only <c>VAR_TEMP</c>; <c>EventMap.RSE</c> in
/// <c>wateride.wad</c> declares ten <c>VAR_EVT</c> slots and a <c>VAR_PAR0</c>.
/// </para>
/// <para>
/// <b>Both halves are asserted, because either alone would mislead.</b> Without the ride half this would
/// suggest the common names move about, which they do not. Without the companion half it would suggest
/// any <c>.RSE</c> can be indexed by the enum, which it cannot.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptVariableIndexTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>One script out of a ride's archive, which stands in for a directory of its own name.</summary>
	private RideScript Script( string ride, string script )
	{
		using var stream = data.OpenRead( $"levels/jungle/rides/{ride}/{script}" );

		return new RideScript( new RideScriptFile( stream ) );
	}

	private static readonly string[] Rides =
		["bouncy", "monkey", "mumbo", "wateride", "spider", "incagod"];

	/// <summary>
	/// <b>Every ride script declares the twelve common names at the enum's own values.</b>
	/// </summary>
	[TestMethod]
	public void EveryRideScriptDeclaresTheCommonNamesAtTheEnumsOwnValues()
	{
		foreach ( var ride in Rides )
		{
			var script = Script( ride, $"{ride}.RSE" );

			foreach ( var name in Enum.GetNames<RideVariables>() )
			{
				var expected = (int)Enum.Parse<RideVariables>( name );

				Assert.AreEqual( expected, script.IndexOf( name ),
					$"{ride} should declare {name} as variable {expected}" );
			}
		}
	}

	/// <summary>
	/// Each of them then adds its own on the end, which is what says the twelve are a shared prefix rather
	/// than the whole list.
	/// </summary>
	[TestMethod]
	public void EachRideAddsItsOwnVariablesAfterTheCommonTwelve()
	{
		var common = Enum.GetNames<RideVariables>().Length;

		var bouncy = Script( "bouncy", "bouncy.RSE" );

		Assert.AreEqual( common, bouncy.IndexOf( "VAR_TEMP" ),
			"Belly Bounce's first own variable sits straight after the common set" );

		Assert.AreEqual( common + 1, bouncy.IndexOf( "VAR_SCREAMING" ), "and its second after that" );

		Assert.IsTrue( Script( "wateride", "wateride.RSE" ).IndexOf( "VAR_BOATCOUNT" ) >= common,
			"and the water ride's is past it too" );

		// Nothing outside its own archive: Belly Bounce has no VAR_BOATCOUNT.
		Assert.AreEqual( -1, Script( "bouncy", "bouncy.RSE" ).IndexOf( "VAR_BOATCOUNT" ) );
	}

	/// <summary>
	/// <b>The half that makes a fixed index unsafe.</b> The companion scripts in those same archives
	/// declare none of the common names at all.
	/// </summary>
	[TestMethod]
	public void TheCompanionScriptsDeclareNoneOfTheCommonNames()
	{
		var companions = new[]
		{
			("monkey", "child.RSE", "VAR_TEMP"),
			("mumbo", "effects.RSE", "VAR_RAND"),
			("wateride", "EventMap.RSE", "VAR_EVT0")
		};

		foreach ( var (ride, file, own) in companions )
		{
			var script = Script( ride, file );

			// It declares its own name, so the script really was read rather than coming back empty.
			Assert.IsTrue( script.IndexOf( own ) >= 0, $"{file} should declare {own}" );

			foreach ( var name in Enum.GetNames<RideVariables>() )
				Assert.AreEqual( -1, script.IndexOf( name ), $"{file} should not declare {name}" );
		}
	}

	/// <summary>
	/// The enum indexer reads the same variable the name does - which is what it is for, now that it
	/// resolves by name rather than by its own value.
	/// </summary>
	[TestMethod]
	public void TheEnumIndexerAgreesWithTheNameLookup()
	{
		var bouncy = Script( "bouncy", "bouncy.RSE" );

		foreach ( var variable in Enum.GetValues<RideVariables>() )
		{
			Assert.AreEqual( bouncy[variable.ToString()], bouncy[variable],
				$"the two ways of reading {variable} should agree" );
		}
	}

	/// <summary>A name the script does not declare is -1, and reading it gives nought rather than throwing.</summary>
	[TestMethod]
	public void ANameTheScriptDoesNotDeclareIsMinusOne()
	{
		var bouncy = Script( "bouncy", "bouncy.RSE" );

		Assert.AreEqual( -1, bouncy.IndexOf( "VAR_NOT_A_REAL_NAME" ) );
		Assert.AreEqual( 0, bouncy["VAR_NOT_A_REAL_NAME"] );

		// VAR_EVT0 is real, but it belongs to the water ride's event map - not to a ride script.
		Assert.AreEqual( -1, bouncy.IndexOf( "VAR_EVT0" ) );
		Assert.IsTrue( Script( "wateride", "EventMap.RSE" ).IndexOf( "VAR_EVT0" ) >= 0 );
	}
}
