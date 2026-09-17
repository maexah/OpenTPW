using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// When each thing in the park was built - the eight <c>tv_t</c> dwords at file offset 22 - and the
/// effects sub-record the cell walk had always stepped over.
///
/// <para>
/// <b>The weekday is what proves the date, and it is the only assertion here that could.</b> The
/// serialiser writes Day third and DayOfWeek fourth, which is <em>not</em> the order the
/// <c>SYSTEMTIME</c> struct uses - so reading it the struct's way swaps two fields and still produces
/// numbers in plausible ranges. Computing the weekday from the year, month and day and comparing it
/// against the stored fourth dword is what tells those two readings apart, and it agrees on all fourteen
/// objects.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkBuiltDateTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private const int Bus = 15;

	private const int Gates = 11;

	private const int Lights = 12;

	/// <summary>
	/// <b>The discriminating test.</b> Every object's stored day-of-week is the one its own year, month
	/// and day imply. A reading that had Day and DayOfWeek the other way round would put a day number
	/// where a weekday belongs and disagree almost everywhere.
	/// </summary>
	[TestMethod]
	public void EveryStoredWeekdayIsTheOneItsOwnDateImplies()
	{
		var world = Park();
		var checkedAgainst = 0;

		foreach ( var o in world.Objects )
		{
			var built = o.Built;

			Assert.IsTrue( built.IsSet, $"thing {o.ThingId} carries a date at all" );

			var implied = (int)new DateTime( built.Year, built.Month, built.Day ).DayOfWeek;

			Assert.AreEqual( implied, built.DayOfWeek,
				$"thing {o.ThingId} is dated {built.Year}-{built.Month:00}-{built.Day:00}, which is weekday " +
				$"{implied}, but the record stores {built.DayOfWeek}" );

			++checkedAgainst;
		}

		Assert.AreEqual( 14, checkedAgainst, "every object in the park was checked" );
	}

	/// <summary>Each field lands inside the range its meaning allows, which a misaligned read would not.</summary>
	[TestMethod]
	public void EveryPartOfTheDateIsInRange()
	{
		foreach ( var o in Park().Objects )
		{
			var b = o.Built;

			Assert.IsTrue( b.Year is >= 1900 and <= 2100, $"thing {o.ThingId} year {b.Year}" );
			Assert.IsTrue( b.Month is >= 1 and <= 12, $"thing {o.ThingId} month {b.Month}" );
			Assert.IsTrue( b.Day is >= 1 and <= 31, $"thing {o.ThingId} day {b.Day}" );
			Assert.IsTrue( b.DayOfWeek is >= 0 and <= 6, $"thing {o.ThingId} weekday {b.DayOfWeek}" );
			Assert.IsTrue( b.Hour is >= 0 and <= 23, $"thing {o.ThingId} hour {b.Hour}" );
			Assert.IsTrue( b.Minute is >= 0 and <= 59, $"thing {o.ThingId} minute {b.Minute}" );
			Assert.IsTrue( b.Second is >= 0 and <= 59, $"thing {o.ThingId} second {b.Second}" );
			Assert.IsTrue( b.Millisecond is >= 0 and <= 999, $"thing {o.ThingId} millisecond {b.Millisecond}" );
		}
	}

	/// <summary>
	/// The dates say something rather than being one constant repeated - which is what keeps the two tests
	/// above from passing over a field that never varies.
	/// </summary>
	[TestMethod]
	public void TheDatesVaryAndTheBusIsTheOddOneOut()
	{
		var byThing = Park().Objects.ToDictionary( o => o.ThingId, o => o.Built );

		// Eleven objects share the moment the park was laid out.
		Assert.AreEqual( 11, byThing.Values.Count( b => b is { Hour: 15, Minute: 37, Second: 30 } ),
			"the objects built together" );

		// The bus is dated nearly four weeks later, and is the only thing not dated the first of the month.
		Assert.AreEqual( 27, byThing[Bus].Day, "the bus's day" );
		Assert.AreEqual( 1, byThing.Values.Count( b => b.Day != 1 ), "and it is the only one" );

		// The gates and the lights carry midnight, which is what a thing that was never built looks like.
		Assert.AreEqual( 0, byThing[Gates].Hour, "the gates" );
		Assert.AreEqual( 0, byThing[Lights].Hour, "the lights" );

		Assert.IsTrue( byThing.Values.Select( b => (b.Day, b.Hour, b.Minute) ).Distinct().Count() >= 3,
			"at least three distinct stamps, so the field is not one constant" );
	}

	/// <summary>
	/// The effects sub-record, now read rather than stepped over.
	/// </summary>
	/// <remarks>
	/// <b>This is read but NOT confirmed, and the difference matters.</b> Every cell reads nought, and an
	/// all-nought field is equally what a correct read of an unused value and a wrong offset landing in
	/// padding both look like. The one thing actually established is the consistency below: nothing
	/// non-zero ever turns up in a cell that carries no effects record, which a stride error would
	/// produce. Treat the value as unverified until a park is found that uses it.
	/// </remarks>
	[TestMethod]
	public void NothingIsReadFromACellThatCarriesNoEffectsRecord()
	{
		var cells = Park().Cells;

		Assert.AreEqual( 250, cells.Count( c => (c.Status & 0x4) != 0 ),
			"the shipped park's cells carrying an effects record" );

		Assert.AreEqual( 0, cells.Count( c => c.NearbyEffects != 0 && (c.Status & 0x4) == 0 ),
			"no cell without an effects record may yield a value - a stride error would show up here" );

		Assert.AreEqual( 0, cells.Count( c => c.NearbyEffects != 0 ),
			"and in this park the value is nought throughout, which is not itself evidence of the offset" );
	}
}
