using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The tags a said line leaves on the gadget's aerial (<c>docs/exe/advisor-park.md</c>, "The tag a line leaves on
/// the aerial"): the tag table's rows, the list's rule for a tag of the same thing, its ten, and its timers. These
/// need no game.
/// </summary>
[TestClass]
public class ParkTagsTests
{
	[TestInitialize]
	public void ALog() => Log ??= new();

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( one => one.What == what ).Times;

	/// <summary>
	/// <b>A said line leaves the tag its response's group names</b> (<c>FUN_00509bf0</c>, <c>FUN_0050af40</c>): the
	/// rides' and the door's rows show part 1 for thirty seconds; group 0x17f leaves none; a group whose row is
	/// not listed here is counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> every group a tag; no tag; the life not kept; an unlisted row uncounted; 0x17f counted.</remarks>
	[TestMethod]
	public void ASaidLineLeavesItsGroupsTag()
	{
		var tags = new ParkTags();
		var unlisted = Times( "TAG_ROW_NOT_LISTED" );

		var tag = tags.Said( 72, 13, 5000 );
		Assert.IsNotNull( tag );
		Assert.AreEqual( (72, 1, 13, 5000L, 35000L, false), (tag.Row.Id, tag.Row.Picture, tag.Subject, tag.Born, tag.HideAt, tag.Hidden) );
		Assert.AreEqual( (1, 1), (tags.Standing.Count, tags.Made) );

		Assert.IsNull( tags.Said( ParkTags.NoTag, 13, 5000 ), "the lobby's lines' group" );
		Assert.IsNull( tags.Said( 0x17e, 13, 5000 ) );
		Assert.AreEqual( unlisted, Times( "TAG_ROW_NOT_LISTED" ) );
		Assert.IsNull( tags.Said( 20, 13, 5000 ) );
		Assert.AreEqual( unlisted + 1, Times( "TAG_ROW_NOT_LISTED" ) );
		Assert.AreEqual( 1, tags.Standing.Count );

		CollectionAssert.AreEqual( new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 24, 30, 31, 32, 33, 34, 62, 64, 73, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 95, 96, 97, 98, 99, 100, 155, 156, 157, 158, 68, 70, 71, 72, 135, 150, 151, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113 }, ParkTags.Rows.Select( row => row.Id ).ToArray() );
		CollectionAssert.AreEqual( new[] { 4, 3, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 63, 65, 74, 78, 82, 383, 383, 383, 383, 383, 383, 68, 68, 68, 383, 151, 150, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383, 383 }, ParkTags.Rows.Select( row => row.Key ).ToArray() );
		CollectionAssert.AreEqual( new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 }, ParkTags.Rows.Select( row => row.Picture ).ToArray() );
		Assert.IsTrue( ParkTags.Rows.All( row => row.Life == (row.Id is >= 102 and <= 113 ? 0 : 30000) ) );

		// The shut park's line: part 2, the entry-price button, no thing.
		var waiting = tags.Said( 3, 0, 6000 );
		Assert.IsNotNull( waiting );
		Assert.AreEqual( (3, 4, 2, 7, 0x18, 0, 0, 0, 36000L),
			(waiting.Row.Id, waiting.Row.Key, waiting.Row.Picture, waiting.Row.String, waiting.Row.Button1, waiting.Row.Button2, waiting.Row.Kind, waiting.Subject, waiting.HideAt) );
		Assert.AreEqual( 2, tags.Standing.Count );
		// The unjoined queue's line: part 0, the camera's button, no thing, and it takes no other tag's place.
		var unjoined = tags.Said( 135, 0, 7000 );
		Assert.IsNotNull( unjoined );
		Assert.AreEqual( (135, ParkTags.NoTag, 0, 134, 3, 0, 0, 0, 37000L),
			(unjoined.Row.Id, unjoined.Row.Key, unjoined.Row.Picture, unjoined.Row.String, unjoined.Row.Button1, unjoined.Row.Button2, unjoined.Row.Kind, unjoined.Subject, unjoined.HideAt) );
		Assert.AreEqual( 3, tags.Standing.Count );
		CollectionAssert.AreEqual( new[] { 150, 151, 3, 4, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag }
			// Rows 238 to 257, the shops' and sideshows' value and satisfaction: their twenty lines leave none.
			.Concat( Enumerable.Repeat( ParkTags.NoTag, 20 ) ).Concat( new[] { 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 24, ParkTags.NoTag, 100, 30, 31, 32, 33, 34, 62, 73, 77, 81, 75, 76, 79, 83, 84, 87, 82, 78, 80, 64, 135, 68, 70, 71, 72, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, ParkTags.NoTag, 95, 96, 97, 98, 99, 155, 156, 157, 158, ParkTags.NoTag } )
			// The golden tickets' fifteen: their lines' groups are their responses' own (ParkAdvisorMessages.ResponseOf);
			// then their four hints, whose responses, 541 and 574 to 577, leave none.
			.Concat( Enumerable.Repeat( ParkTags.NoTag, 15 + 4 ) ).ToArray(), ParkAdvisorMessages.ParkTopics.Select( topic => topic.Group ).ToArray(),
			"each built topic's lines' group" );
	}

	/// <summary>
	/// <b>A new tag takes off the tag of its row's key about the same thing</b> (<c>FUN_0048eb20</c>): a mechanic's
	/// line's takes the about-to-break tag of that ride and no other ride's; the door's two take each other's; a row
	/// with no key takes none; and a row that is its own key adds nothing while one stands.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> nothing taken off; a tag of another thing taken off; every older tag taken off; the tag of
	/// the same id taken off; a row its own key adding a second.
	/// </remarks>
	[TestMethod]
	public void ANewTagTakesTheTagOfItsKey()
	{
		var tags = new ParkTags();

		tags.Said( 68, 13, 0 );
		tags.Said( 68, 14, 0 );
		tags.Said( 68, 13, 10 );
		Assert.AreEqual( 3, tags.Standing.Count, "row 68 names no key: a second about the same ride stands too" );

		Assert.IsNotNull( tags.Said( 72, 13, 20 ) );
		CollectionAssert.AreEqual( new[] { (68, 14), (72, 13) }, tags.Standing.Select( tag => (tag.Row.Id, tag.Subject) ).ToArray() );

		tags.Said( 72, 13, 30 );
		Assert.AreEqual( 3, tags.Standing.Count, "72's key is 68, not itself" );

		tags.Said( 150, 0, 40 );
		tags.Said( 151, 0, 50 );
		CollectionAssert.AreEqual( new[] { 68, 72, 72, 151 }, tags.Standing.Select( tag => tag.Row.Id ).ToArray(), "closed takes opened" );
		tags.Said( 150, 0, 60 );
		CollectionAssert.AreEqual( new[] { 68, 72, 72, 150 }, tags.Standing.Select( tag => tag.Row.Id ).ToArray(), "and opened closed" );

		var own = new ParkTags( [new ParkTags.Row( 7, 7, 0, 2, 0, 0, 0, 0 )] );
		Assert.IsNotNull( own.Said( 7, 3, 0 ) );
		Assert.IsNull( own.Said( 7, 3, 10 ), "its own key: the one standing is the line's" );
		Assert.IsNotNull( own.Said( 7, 4, 10 ), "another thing's is new" );
		Assert.AreEqual( (2, long.MaxValue), (own.Standing.Count, own.Standing[0].HideAt), "and a row with no life is never hidden" );
	}

	/// <summary><b>The list holds ten</b> (<c>DAT_00750610</c>): an eleventh line leaves none, and one does again once a place is free.</summary>
	/// <remarks><b>Mutations:</b> no limit; nine; eleven.</remarks>
	[TestMethod]
	public void TheListHoldsTen()
	{
		var tags = new ParkTags();

		for ( var ride = 1; ride <= 10; ++ride )
			Assert.IsNotNull( tags.Said( 68, ride, 0 ), $"tag {ride}" );

		Assert.IsNull( tags.Said( 68, 11, 0 ) );
		Assert.AreEqual( (10, 10), (tags.Standing.Count, tags.Made) );

		tags.Hide( tags.Standing[0], 0 );
		tags.Tick( 500 );
		Assert.IsNotNull( tags.Said( 68, 11, 500 ) );
	}

	/// <summary>
	/// <b>A tag's two timers</b> (<c>FUN_0048eb20</c>, <c>FUN_0048dae0</c>): five seconds before its life's end its
	/// particle, counted once; at the end it is hidden; half a second on it is off the mast.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> shown at once; the particle at the start; at the end; asked for every turn; hidden a tick early; never
	/// hidden; taken off as it is hidden; never taken off; the change not told.
	/// </remarks>
	[TestMethod]
	public void ATagIsHiddenAtItsLifesEndAndThenTakenOff()
	{
		var tags = new ParkTags();
		var tag = tags.Said( 68, 13, 1000 )!;
		Assert.AreEqual( (1500L, false, true), (tag.ShowAt, tag.ShownAt( 1499 ), tag.ShownAt( 1500 )), "shown half a second after it is put on" );

		tags.Tick( 25999 );
		Assert.IsFalse( tag.EndingDue );
		tags.Tick( 26000 );
		Assert.IsTrue( tag.EndingDue, "its particle asked for 25 s in" );
		tag.EndingDue = false;
		tags.Tick( 26500 );
		Assert.IsFalse( tag.EndingDue, "once" );

		var changes = tags.Changes;
		tags.Tick( 30999 );
		Assert.AreEqual( (false, 1, changes), (tag.Hidden, tags.Standing.Count, tags.Changes) );
		tags.Tick( 31000 );
		Assert.AreEqual( (true, 1, changes + 1, false), (tag.Hidden, tags.Standing.Count, tags.Changes, tag.ShownAt( 31000 )), "hidden, its place kept" );
		tags.Tick( 31499 );
		Assert.AreEqual( 1, tags.Standing.Count );
		tags.Tick( 31500 );
		Assert.AreEqual( (0, changes + 2), (tags.Standing.Count, tags.Changes) );
	}

	/// <summary>
	/// <b>A hidden tag goes half a second on, and a delete of all takes the oldest first, a tenth of a second
	/// apart</b> (<c>LAB_004a11e0</c>, 0x100002 and 0x100003), not hidden before they go.
	/// </summary>
	/// <remarks><b>Mutations:</b> all at once; the newest first; hidden at the click; a hide of a hidden tag starting its wait again.</remarks>
	[TestMethod]
	public void AHiddenTagAndADeleteOfAll()
	{
		var tags = new ParkTags();
		var a = tags.Said( 68, 1, 0 )!;
		tags.Said( 68, 2, 0 );
		tags.Said( 68, 3, 0 );

		tags.Hide( a, 1000 );
		tags.Hide( a, 1400 );
		tags.Tick( 1499 );
		Assert.AreEqual( 3, tags.Standing.Count );
		tags.Tick( 1500 );
		Assert.AreEqual( 2, tags.Standing.Count, "500 ms from the first hide" );

		tags.DeleteAll( 2000 );
		Assert.IsTrue( tags.Standing.All( tag => !tag.Hidden ) );
		tags.Tick( 2099 );
		Assert.AreEqual( 2, tags.Standing.Count );
		tags.Tick( 2100 );
		CollectionAssert.AreEqual( new[] { 3 }, tags.Standing.Select( tag => tag.Subject ).ToArray() );
		tags.Tick( 2199 );
		Assert.AreEqual( 1, tags.Standing.Count );
		tags.Tick( 2200 );
		Assert.AreEqual( 0, tags.Standing.Count );
		StringAssert.Contains( tags.Census( 2200 ), "standing=0" );
	}
	/// <summary>
	/// <b>A tag whose message is put on the bar stops its two timers</b> (<c>FUN_0048d9c0</c>): it is the tag on
	/// show, no longer hides at its life's end and starts no particle; a tag whose second button declines keeps
	/// them; the bar's leaving, or the tag's going, leaves no tag on show.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the timers left running; the particle's timer left; a declining tag's stopped; the same tag
	/// opened twice; a tag not standing opened; the tag on show kept after it is taken off; after the bar leaves; the tag
	/// on show at the click, not as the arm starts out; one taken off before the arm starts out put on show.
	/// </remarks>
	[TestMethod]
	public void AnOpenedTagStopsItsTimers()
	{
		var tags = new ParkTags();
		var opened = tags.Said( 72, 13, 1000 )!;
		var left = tags.Said( 150, 0, 1000 )!;

		Assert.IsTrue( opened.TimersRun );
		Assert.IsNull( tags.OnShow );
		var changes = tags.Changes;
		Assert.IsTrue( tags.Open( opened, 3000 ) );
		Assert.AreEqual( (null, opened, false, long.MaxValue), (tags.OnShow, tags.PuttingOnShow, opened.TimersRun, opened.HideAt), "being put on show" );
		Assert.IsTrue( opened.EndingStopped, "its particle to be killed with its timers (FUN_0048d9c0)" );
		tags.Shown( 3000 );
		Assert.AreEqual( (opened, opened, 3000L), (tags.OnShow, tags.PuttingOnShow, opened.OpenedAt), "on show as the arm starts out" );
		tags.FullyOut();
		Assert.IsNull( tags.PuttingOnShow, "and no longer being put on show once out" );
		Assert.AreEqual( changes + 1, tags.Changes );
		Assert.IsFalse( tags.Open( opened, 4000 ), "the tag on show is not opened again" );
		Assert.AreEqual( 3000L, opened.OpenedAt );
		StringAssert.Contains( tags.Census( 5000 ), "tag=72/picture=1/subject=13/age=4000/hidden=0/item=0/slot=0/life=30000/timers=0/open=1/" );
		StringAssert.Contains( tags.Census( 5000 ), "tag=150/picture=1/subject=0/age=4000/hidden=0/item=0/slot=1/life=30000/timers=1/open=0/" );

		tags.Tick( 26000 );
		Assert.AreEqual( (true, false, false), (left.EndingDue, left.EndingStopped, opened.EndingDue), "the tag left alone starts its particle, the opened one none" );
		tags.Tick( 31000 );
		tags.Tick( 31500 );
		Assert.AreEqual( (1, opened, false), (tags.Standing.Count, tags.Standing[0], opened.Hidden), "the tag left alone is gone and the opened one stands" );
		Assert.IsFalse( tags.Open( left, 32000 ), "a tag off the mast is not opened" );
		Assert.AreEqual( opened, tags.OnShow );

		tags.Shut();
		Assert.IsNull( tags.OnShow );
		tags.Tick( 500000 );
		Assert.AreEqual( (1, false), (tags.Standing.Count, opened.Hidden), "its timers stay stopped with the bar gone" );

		// Taken off while on show: a mechanic's line takes the about-to-break tag's place.
		var worn = tags.Said( 68, 14, 600000 )!;
		Assert.IsTrue( tags.Open( worn, 600100 ) );
		tags.Shown( 600100 );
		tags.Said( 72, 14, 600200 );
		Assert.IsNull( tags.OnShow, "the tag on show taken off leaves none" );

		// Taken off while the arm comes in for the bar: not put on show as it goes out.
		var early = tags.Said( 68, 15, 700000 )!;
		Assert.IsTrue( tags.Open( early, 700100 ) );
		tags.Said( 72, 15, 700200 );
		tags.Shown( 700300 );
		Assert.IsNull( tags.OnShow, "a tag taken off before the arm starts out is not put on show" );

		// A tag that declines an offer keeps its timers when opened.
		var offers = new ParkTags( [new ParkTags.Row( 5, ParkTags.NoTag, 1, 0, 0x1a, 0x1b, 30000, 0 )] );
		var offer = offers.Said( 5, 0, 1000 )!;
		Assert.IsTrue( offers.Open( offer, 2000 ) );
		offers.Shown( 2000 );
		Assert.AreEqual( (true, 31000L, false), (offer.TimersRun, offer.HideAt, offer.EndingStopped), "its timers and its particle kept" );
		offers.Tick( 31000 );
		Assert.IsTrue( offer.Hidden );
		Assert.AreEqual( offer, offers.OnShow, "hidden and still on show until it is off the mast" );
		offers.Tick( 31500 );
		Assert.AreEqual( (0, null), (offers.Standing.Count, offers.OnShow) );
	}

	/// <summary>
	/// <b>A golden ticket's line leaves a tag of its own group that stands until it is taken off</b> (rows 102 to 113 of
	/// the tag table; <c>FUN_00509e10</c>): picture 3, no buttons, no life, so no timers and no particle; its text the
	/// row's string with the Local ticket's threshold where the kind names one, as the number's size, kinds 5, 6 and 7
	/// as parameter 1, kind 8 the recent visitors as 1 and their months as 2.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a ticket row unlisted; a life given; kinds 5 and 6 swapped; kind 7 the visitors; kind 8's
	/// two swapped; a negative number written with its sign; a parameter's number ignored.
	/// </remarks>
	[TestMethod]
	public void ATicketLinesTagStandsWithItsThreshold()
	{
		var strings = Enumerable.Range( 0, 192 ).Select( _ => new StringPart[] { new( "plain", 0 ) } ).ToArray();
		strings[180] = [new( "getting ", 0 ), new( null, 1 ), new( " visitors", 0 )];
		strings[181] = [new( "having ", 0 ), new( null, 1 ), new( " people", 0 )];
		strings[184] = [new( "exceeding $", 0 ), new( null, 1 ), new( " profit", 0 )];
		strings[185] = [new( "getting ", 0 ), new( null, 1 ), new( " visitors in the last ", 0 ), new( null, 2 ), new( " months", 0 )];
		strings[186] = [new( "a high roller coaster", 0 ), new( null, 1 )];
		var tags = new ParkTags
		{
			Strings = strings,
			Thresholds = new( Visitors: 1500, PeopleInPark: 320, Happiness: 70, AtLeastThisManyHappyPeople: 90,
				ProfitYear: -45000, RecentVisitors: 2600, RecentVisitorMonths: 7, CoasterHeight: 40, GokartExcitement: 50,
				WaterLength: 60, MinCellsCovered: 80 )
		};
		var unlisted = Times( "TAG_ROW_NOT_LISTED" );
		var kinds = Times( "TAG_TEXT_KIND" );

		var said = Enumerable.Range( 102, 12 ).Select( group => tags.Said( group, 0, 1000 ) ).ToArray();
		Assert.AreEqual( (unlisted, kinds), (Times( "TAG_ROW_NOT_LISTED" ), Times( "TAG_TEXT_KIND" )) );
		Assert.IsTrue( said.Take( 10 ).All( tag => tag is { Row.Picture: 3, Row.Button1: 0, Row.Button2: 0, Row.Life: 0, HideAt: long.MaxValue, TimersRun: false } ) );
		Assert.AreEqual( (10, 10), (tags.Standing.Count, tags.Made), "the list's ten, the eleventh and twelfth refused" );
		CollectionAssert.AreEqual( new[] { "getting 1500 visitors", "having 320 people", "plain", "plain", "exceeding $45000 profit",
			"getting 2600 visitors in the last 7 months", "a high roller coaster", "plain", "plain", "plain" },
			said.Take( 10 ).Select( tag => tag!.Text ).ToArray() );

		tags.Tick( 10_000_000 );
		Assert.AreEqual( (10, false), (tags.Standing.Count, tags.Standing.Any( tag => tag.EndingDue )), "no life, so no end" );

		var least = new ParkTags { Strings = strings, Thresholds = default( ParkGoldenTickets.Thresholds ) with { ProfitYear = int.MinValue } };
		Assert.AreEqual( "exceeding $-2147483648 profit", least.Said( 106, 0, 0 )!.Text, "the size of the least int is itself" );
	}

	/// <summary>
	/// <b>Each ticket row names its own string and text kind</b> (rows 102 to 113 of the table at <c>0x0075eca8</c>, read from
	/// the executable): strings 180 to 191 in order, kinds 5, 6, 0, 0, 7, 8 and then six nought, so that only the four
	/// strings that carry a number are given one. Every row is said on a list of its own, past the ten-tag cap, from a
	/// table whose every string is different, so a row given another's string reads another's text.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> rows 104 and 105's strings swapped; two of rows 109 to 113's strings swapped; a kind 5 to 8 given to
	/// a row whose string has no number; a kind 5 to 8 taken from one whose string has.
	/// </remarks>
	[TestMethod]
	public void EachTicketRowNamesItsOwnStringAndKind()
	{
		var rows = ParkTags.Rows.Where( row => row.Id is >= 102 and <= 113 ).OrderBy( row => row.Id ).ToArray();
		CollectionAssert.AreEqual( Enumerable.Range( 180, 12 ).ToArray(), rows.Select( row => row.String ).ToArray() );
		CollectionAssert.AreEqual( new[] { 5, 6, 0, 0, 7, 8, 0, 0, 0, 0, 0, 0 }, rows.Select( row => row.Kind ).ToArray() );

		var strings = Enumerable.Range( 0, 192 ).Select( at => new StringPart[] { new( $"string {at}", 0 ) } ).ToArray();
		var numbered = new[] { 180, 181, 184, 185 };
		foreach ( var at in numbered )
			strings[at] = [new( $"string {at} ", 0 ), new( null, 1 )];

		foreach ( var row in rows )
		{
			var tags = new ParkTags { Strings = strings, Thresholds = new( Visitors: 11, PeopleInPark: 22, Happiness: 0,
				AtLeastThisManyHappyPeople: 0, ProfitYear: 44, RecentVisitors: 55, RecentVisitorMonths: 6, CoasterHeight: 0,
				GokartExcitement: 0, WaterLength: 0, MinCellsCovered: 0 ) };
			var text = tags.Said( row.Id, 0, 0 )!.Text;

			Assert.AreEqual( numbered.Contains( row.String ), row.Kind != 0, $"row {row.Id}: a number only where its string has one" );
			StringAssert.StartsWith( text, $"string {row.String}", $"row {row.Id}" );
		}
	}

	/// <summary>
	/// <b>A park's tags write the ticket rows with its own thresholds</b> (<c>Level.DressTags</c>): Lost Kingdom's
	/// Standard keys, 100 visitors, 200 inside, $15,000 profit, 350 visitors in 6 months, into <c>TAG_SYSTEM.str</c>'s rows
	/// 180, 181, 184 and 185. Needs the game.
	/// </summary>
	/// <remarks><b>Mutations:</b> the thresholds not given; the strings not given.</remarks>
	[TestMethod]
	public void AParksTicketTagsReadItsThresholds()
	{
		GameData.Required();
		var tags = Level.DressTags( new ParkTags(), new ParkBalance( "jungle", easyMode: false ) );
		const string Won = "YOU'VE WON A GOLDEN TICKET!";

		StringAssert.StartsWith( tags.Said( 102, 0, 0 )!.Text, Won );
		StringAssert.EndsWith( tags.Standing[0].Text, "for getting 100 visitors to your park. Keep up the good work!" );
		StringAssert.EndsWith( tags.Said( 103, 0, 0 )!.Text, "for having 200 people in your park at one time. Keep it up!" );
		StringAssert.EndsWith( tags.Said( 106, 0, 0 )!.Text, "for exceeding $15000 profit this year." );
		StringAssert.EndsWith( tags.Said( 107, 0, 0 )!.Text, "for getting 350 visitors in the last 6 months." );
	}
}
