using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A track section's collision objects and buoy as <c>TrackRide_AddSection</c> (<c>FUN_0054b2f0</c>) makes them -
/// <see cref="ParkTrackRideTable.Lay"/>. <c>docs/exe/park.md</c>, "A section". The places and sizes are the original's
/// own, read in it after Q581's clicks (<c>q581/orig/A-track.log</c> at 496 s, <c>D-track.log</c> at 171 s).
/// </summary>
[TestClass]
public class ParkTrackSectionObjectsTests
{
	private static int TimesReported( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static ParkTrackRideTable.CollisionObject Obj( int flags, int x, int z, int rx, int rz ) => new( flags, x, z, rx, rz );

	/// <summary>The karts' loop south of its station: (type, cell x, cell y), the station's two flagged.</summary>
	private static readonly (int Type, int X, int Y)[] KartLoop =
	[
		(9 | ParkTrackRideTable.StationSection, 30, 25), (9 | ParkTrackRideTable.StationSection, 32, 25), (7, 34, 25), (10, 34, 27),
		(6, 34, 29), (9, 32, 29), (9, 30, 29), (5, 28, 29), (10, 28, 27), (8, 28, 25)
	];

	private static (ParkTrackRideTable Table, int Handle) Laid( int bumperType, (int Type, int X, int Y)[] loop, int dy = 0 )
	{
		var table = new ParkTrackRideTable();
		var handle = table.Take( bumperType );

		foreach ( var (type, x, y) in loop )
			Assert.IsTrue( table.Lay( handle, type, x * 0xc00, (y + dy) * 0xc00 ) );

		return (table, handle);
	}

	/// <summary>Every section's objects of the karts' ten as <c>A-track.log</c> lists them, the last made first.</summary>
	[TestMethod]
	public void TheKartLoopsObjectsAreTheOriginals()
	{
		var (table, karts) = Laid( -4, KartLoop );

		var want = new[]
		{
			new[] { Obj( 0x3f, 95232, 79872, 3104, 1148 ) },
			new[] { Obj( 0x3f, 101376, 79872, 3104, 1148 ) },
			new[] { Obj( 0x55, 104448, 82944, 1924, 1924 ), Obj( 0x15, 104448, 82944, 4220, 4220 ) },
			new[] { Obj( 0x3f, 107520, 86016, 1148, 3104 ) },
			new[] { Obj( 0x4d, 104448, 89088, 1924, 1924 ), Obj( 0xd, 104448, 89088, 4220, 4220 ) },
			new[] { Obj( 0x3f, 101376, 92160, 3104, 1148 ) },
			new[] { Obj( 0x3f, 95232, 92160, 3104, 1148 ) },
			new[] { Obj( 0x4b, 92160, 89088, 1924, 1924 ), Obj( 0xb, 92160, 89088, 4220, 4220 ) },
			new[] { Obj( 0x3f, 89088, 86016, 1148, 3104 ) },
			new[] { Obj( 0x53, 92160, 82944, 1924, 1924 ), Obj( 0x13, 92160, 82944, 4220, 4220 ) }
		};

		for ( var section = 0; section < want.Length; ++section )
			CollectionAssert.AreEqual( want[section], table.ObjectsOf( karts, section ).ToArray(), $"section {section}" );
	}

	/// <summary>
	/// The karts' eight buoys, the station's two sections laying none, each at <c>A-track.log</c>'s buoys 9 to 16 and
	/// tied to its section's first object made: a bend's outer quarter, a straight's rectangle.
	/// </summary>
	[TestMethod]
	public void TheKartLoopsBuoysAreTheOriginals()
	{
		var (table, karts) = Laid( -4, KartLoop );
		var buoys = table.BuoysOf( karts );

		CollectionAssert.AreEqual( new[]
		{
			(106620, 80772), (107520, 86016), (106620, 91260), (101376, 92160),
			(95232, 92160), (89988, 91260), (89088, 86016), (89988, 80772)
		}, buoys.Select( buoy => (buoy.X, buoy.Z) ).ToArray() );

		Assert.IsTrue( buoys.All( buoy => buoy.Flags == 1 ) );
		Assert.AreEqual( Obj( 0x15, 104448, 82944, 4220, 4220 ), buoys[0].Object, "the bend's buoy is tied to its outer quarter" );
		Assert.AreEqual( Obj( 0x3f, 107520, 86016, 1148, 3104 ), buoys[1].Object, "the straight's to its rectangle" );
	}

	/// <summary>The water ride's loop (<c>D-track.log</c>): its narrower straights, its bends' other radii and its buoys 18 to 25.</summary>
	[TestMethod]
	public void TheWaterLoopsObjectsAndBuoysAreTheOriginals()
	{
		// Q581's water loop is the karts' shape, 12 cells west and 6 north.
		var loop = KartLoop.Select( section => (section.Type, section.X - 12, section.Y) ).ToArray();
		var (table, water) = Laid( -5, loop, -6 );

		CollectionAssert.AreEqual( new[] { Obj( 0x3f, 58368, 61440, 3104, 257 ) }, table.ObjectsOf( water, 0 ).ToArray() );
		CollectionAssert.AreEqual( new[] { Obj( 0x4d, 67584, 70656, 2815, 2815 ), Obj( 0xd, 67584, 70656, 3329, 3329 ) },
			table.ObjectsOf( water, 4 ).ToArray() );
		CollectionAssert.AreEqual( new[] { Obj( 0x3f, 52224, 67584, 257, 3104 ) }, table.ObjectsOf( water, 8 ).ToArray() );
		CollectionAssert.AreEqual( new[]
		{
			(69756, 62340), (70656, 67584), (69756, 72828), (64512, 73728),
			(58368, 73728), (53124, 72828), (52224, 67584), (53124, 62340)
		}, table.BuoysOf( water ).Select( buoy => (buoy.X, buoy.Z) ).ToArray() );
	}

	/// <summary>
	/// A crossing: a round at its middle, <c>ftol( sqrt( (w - r)² + 3072² ) )</c> (3279 for the karts, 3082 for the
	/// water ride, whose is flagged 0x80), its four obstacle corners each put at the list's head, its buoy at the
	/// middle; an add-on's straight is flagged 0x100.
	/// </summary>
	[TestMethod]
	public void ACrossingAndAnAddOnAreMadeAsTheOriginals()
	{
		var (table, karts) = Laid( -4, [(11, 10, 10), (12, 12, 10), (13, 10, 12)] );

		CollectionAssert.AreEqual( new[]
		{
			Obj( 0x53, 36864, 36864, 1924, 1924 ), Obj( 0x55, 30720, 36864, 1924, 1924 ), Obj( 0x4d, 30720, 30720, 1924, 1924 ),
			Obj( 0x4b, 36864, 30720, 1924, 1924 ), Obj( 0x1f, 33792, 33792, 3279, 3279 )
		}, table.ObjectsOf( karts, 0 ).ToArray() );
		Assert.AreEqual( (33792, 33792, 3279), (table.BuoysOf( karts )[0].X, table.BuoysOf( karts )[0].Z, table.BuoysOf( karts )[0].Object!.Value.RadiusX) );
		Assert.AreEqual( Obj( 0x13f, 39936, 33792, 3104, 1148 ), table.ObjectsOf( karts, 1 ).Single() );
		Assert.AreEqual( Obj( 0x13f, 33792, 39936, 1148, 3104 ), table.ObjectsOf( karts, 2 ).Single() );

		var (wet, water) = Laid( -5, [(11, 10, 10)] );

		Assert.AreEqual( Obj( 0x9f, 33792, 33792, 3082, 3082 ), wet.ObjectsOf( water, 0 )[^1] );
		Assert.AreEqual( 2815, wet.ObjectsOf( water, 0 )[0].RadiusX );
	}

	/// <summary>A walk again lets the last walk's buoys go with its sections, and a sale lets every buoy go.</summary>
	[TestMethod]
	public void ClearingAndFreeingLetTheBuoysGo()
	{
		var (table, karts) = Laid( -4, KartLoop );

		table.ClearSections( karts );
		Assert.AreEqual( 0, table.BuoysOf( karts ).Count );

		foreach ( var (type, x, y) in KartLoop )
			table.Lay( karts, type, x * 0xc00, y * 0xc00 );

		Assert.AreEqual( 8, table.BuoysOf( karts ).Count, "eight again, not sixteen" );

		var water = table.Take( -5 );

		table.Lay( water, 11, 0, 0 );
		Assert.AreEqual( 9, table.BuoysInUse );

		table.Free( karts );
		Assert.AreEqual( 1, table.BuoysInUse, "the sale let the karts' eight go, the water ride's kept" );
		Assert.AreEqual( 0, table.BuoysOf( table.Take( -4 ) ).Count, "the slot taken again holds none" );
	}

	/// <summary>
	/// What the original does not survive is counted and laid bare: a type outside 1 to 13 not on the station, the
	/// pool's 2048 objects passed, a template not known here. A station's unknown type makes no buoy, so it is not counted.
	/// </summary>
	[TestMethod]
	public void WhatTheOriginalDoesNotSurviveIsCounted()
	{
		var table = new ParkTrackRideTable();
		var karts = table.Take( -4 );
		var unknown = TimesReported( "TRACK_SECTION_UNKNOWN_TYPE" );

		Assert.IsTrue( table.Lay( karts, 0x10000 | 20, 0, 0 ) );
		Assert.AreEqual( unknown, TimesReported( "TRACK_SECTION_UNKNOWN_TYPE" ), "on the station: no buoy, nothing written through" );
		Assert.IsTrue( table.Lay( karts, 14, 0x1800, 0 ) );
		Assert.AreEqual( unknown + 1, TimesReported( "TRACK_SECTION_UNKNOWN_TYPE" ) );
		Assert.AreEqual( 0, table.BuoysOf( karts ).Count );
		Assert.AreEqual( 0, table.ObjectsOf( karts, 1 ).Count );

		// 409 crossings take 2045 objects; the 410th's five do not fit.
		for ( var at = 0; at < 409; ++at )
			Assert.IsTrue( table.Lay( karts, 11, (at % 60) * 0x1800, (1 + at / 60) * 0x1800 ) );

		var full = TimesReported( "TRACK_SECTION_OUT_OF_OBJECTS" );

		Assert.IsTrue( table.Lay( karts, 11, 0, 20 * 0x1800 ) );
		Assert.AreEqual( full + 1, TimesReported( "TRACK_SECTION_OUT_OF_OBJECTS" ) );
		Assert.AreEqual( 0, table.ObjectsOf( karts, 411 ).Count );
		Assert.AreEqual( 409, table.BuoysOf( karts ).Count, "and lays no buoy" );
		// 2045 and three straights' one each: the pool's 2048 exactly, then none.
		for ( var at = 1; at <= 3; ++at )
			Assert.IsTrue( table.Lay( karts, 9, at * 0x1800, 20 * 0x1800 ) );

		Assert.AreEqual( 1, table.ObjectsOf( karts, 414 ).Count, "the 2048th fits" );
		Assert.IsTrue( table.Lay( karts, 9, 4 * 0x1800, 20 * 0x1800 ) );
		Assert.AreEqual( 0, table.ObjectsOf( karts, 415 ).Count, "the 2049th does not" );
		Assert.AreEqual( full + 2, TimesReported( "TRACK_SECTION_OUT_OF_OBJECTS" ) );

		var other = TimesReported( "TRACK_SECTION_TEMPLATE_7" );
		var lake = table.Take( -7 );

		Assert.IsTrue( table.Lay( lake, 9, 0, 0 ) );
		Assert.AreEqual( other + 1, TimesReported( "TRACK_SECTION_TEMPLATE_7" ) );
	}
}
