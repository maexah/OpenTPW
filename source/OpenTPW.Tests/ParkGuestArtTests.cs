using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Which picture a person's stored bank number actually resolves to. These read real game files and are
/// skipped where there is no installation: see <see cref="GameData"/>.
///
/// <para>
/// A bank number is an ordinal, not a name - the save records "kids bank 5" and nothing else - so the
/// only thing that makes it mean a particular child is the order the folder was loaded in. That order is
/// not the folder's own: for kids and kid heads the original loads four named banks first and sweeps up
/// the remainder afterwards. Getting it wrong is invisible in every count, every trailer and every
/// reconciliation the other park tests make, and shows up only as the wrong children standing in the
/// park - which is precisely the sort of wrongness that survives review, so it is pinned here.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestArtTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string Kids = "esprites/Generic/Kids";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>Bank names alone, upper-cased, because the assertions below are about order and nothing else.</summary>
	private static string[] Names( string[] banks ) =>
		[.. banks.Select( bank => Path.GetFileNameWithoutExtension( bank ).ToUpperInvariant() )];

	/// <summary>The folder exactly as the archive lists it, which is what a plain sweep would number.</summary>
	private string[] Swept( string folder ) =>
		[.. data.GetFiles( folder ).Where( file => file.EndsWith( ".esp", StringComparison.OrdinalIgnoreCase ) )];

	/// <summary>
	/// The two orders, side by side. The first assertion is as load-bearing as the second: it pins the
	/// archive order this whole correction is stated against, so if a differently built <c>esprites.wad</c>
	/// ever listed its children another way, this would say so rather than silently changing what bank 0
	/// means.
	/// </summary>
	[TestMethod]
	public void TheFourAvatarBanksAreNumberedBeforeTheRestOfTheFolder()
	{
		CollectionAssert.AreEqual(
			new[] { "SPR_BE", "SPR_BI", "SPR_CH", "SPR_FR", "SPR_KI", "SPR_SA", "SPR_SU", "SPR_TA" },
			Names( Swept( Kids ) ),
			"the archive's own order for the kids folder" );

		CollectionAssert.AreEqual(
			new[] { "SPR_BI", "SPR_KI", "SPR_TA", "SPR_SU", "SPR_BE", "SPR_CH", "SPR_FR", "SPR_SA" },
			Names( ParkGuestSprites.BanksIn( data, Kids, 0 ) ),
			"the four avatar banks first, then the rest of the folder as it comes" );
	}

	/// <summary>
	/// Staff folders get no avatar pass, so they are numbered exactly as the archive lists them. This is
	/// the control for the test above: if the reordering leaked into every kind, the mechanic would stop
	/// being who the save says he is.
	/// </summary>
	[TestMethod]
	public void EveryOtherKindIsNumberedJustAsTheArchiveListsIt()
	{
		const string mechanics = "esprites/Generic/Mechanics";

		CollectionAssert.AreEqual( Names( Swept( mechanics ) ),
			Names( ParkGuestSprites.BanksIn( data, mechanics, 6 ) ),
			"mechanics are swept plainly, so bank 1 stays the slim one the shipped park's mechanic wears" );
	}

	/// <summary>
	/// The shipped park's thirteen guests, resolved through the numbering above to the children they are
	/// actually wearing.
	///
	/// <para>
	/// The last assertion is the anti-vacuity guard. Six distinct banks are in play, and under a plain
	/// sweep they would resolve to a different six - the same count, the same shape, all eight files
	/// present either way - so an assertion on the names alone could pass for the wrong reason if the
	/// avatar pass were quietly dropped. Requiring the two to disagree is what makes this test fail if it
	/// ever stops testing anything.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheShippedParksGuestsWearTheChildrenTheSaveNamed()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var guests = world.People.Where( person => person.Model == 1 )
			.Select( person => world.Sprites.First( sprite => sprite.Slot == person.SpriteSlot ) )
			.ToArray();

		Assert.AreEqual( 13, guests.Length, "guests in the shipped park" );

		string[] Worn( string[] banks ) =>
			[.. guests.Select( sprite => Path.GetFileNameWithoutExtension( banks[sprite.Bank + sprite.BankOffset] )
				.ToUpperInvariant() ).Distinct().OrderBy( name => name, StringComparer.Ordinal )];

		var loaded = ParkGuestSprites.BanksIn( data, Kids, 0 );

		CollectionAssert.AreEqual(
			new[] { "SPR_BE", "SPR_BI", "SPR_CH", "SPR_FR", "SPR_SA", "SPR_TA" },
			Worn( loaded ),
			"the six children the park's guests wear" );

		CollectionAssert.AreNotEqual( Worn( Swept( Kids ) ), Worn( loaded ),
			"a plain sweep must resolve the same banks to different children, or this test proves nothing" );
	}
}
