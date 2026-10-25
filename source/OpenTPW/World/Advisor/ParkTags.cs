namespace OpenTPW;

/// <summary>
/// The tags a said line leaves on the gadget's aerial: the tag system's hearing of the event a line sends
/// (FUN_00509bf0, type 0x18) and the messages control's list (FUN_0048eb20). A line's response group names a row
/// of the tag table; the row is the tag's picture, the tag it takes the place of and how long it stands.
/// docs/exe/advisor-park.md, "The tag a line leaves on the aerial".
/// <para>
/// The times are the interface's timers', real milliseconds, handed in by the caller.
/// </para>
/// <para>
/// <b>A park file holds the tags standing</b> (<see cref="Written"/>, <see cref="Restore"/>;
/// docs/exe/saves.md, "OpenTPW's writer, the aerial's tags"): each one's id, text, whole life, picture, buttons,
/// thing and item, and a load puts each back with its whole life before it.
/// </para>
/// <para>
/// <b>A tag's left click puts its message on the bar</b> (<see cref="Open"/>, FUN_0048d9c0), which stops the
/// tag's two timers: a tag once opened stands until it is taken off. docs/exe/hud.md, "The message bar".
/// </para>
/// </summary>
internal sealed class ParkTags
{
	/// <summary>
	/// A row of the tag table 0x0075eca8: its id, the id of the tag it takes the place of (+0x04), the part of
	/// msgtag.md2 it shows (+0x08), its string (+0x0c), the message bar's two buttons (+0x10, +0x14), its life in
	/// milliseconds (+0x18) and how its text is made (+0x1c).
	/// </summary>
	internal sealed record Row( int Id, int Key, int Picture, int String, int Button1, int Button2, int Life, int Kind );

	/// <summary>The id no row holds, the key of a tag that takes no other's place (FUN_0050af40).</summary>
	internal const int NoTag = 0x17f;

	/// <summary>The rows the built topics' response groups name, of the table's 382.</summary>
	internal static readonly Row[] Rows =
	[
		new( 3, 4, 2, 7, 24, 0, 30000, 0 ),
		new( 4, 3, 2, 8, 24, 0, 30000, 0 ),
		new( 5, NoTag, 2, 9, 10, 0, 30000, 0 ),
		new( 6, NoTag, 2, 10, 10, 0, 30000, 0 ),
		new( 7, NoTag, 2, 11, 14, 0, 30000, 0 ),
		new( 8, NoTag, 2, 12, 14, 0, 30000, 0 ),
		new( 9, NoTag, 2, 13, 14, 0, 30000, 0 ),
		new( 10, NoTag, 2, 14, 13, 0, 30000, 0 ),
		new( 11, NoTag, 2, 15, 13, 0, 30000, 0 ),
		new( 12, NoTag, 2, 16, 13, 0, 30000, 0 ),
		new( 13, NoTag, 2, 17, 16, 0, 30000, 0 ),
		new( 14, NoTag, 2, 18, 16, 0, 30000, 0 ),
		new( 15, NoTag, 2, 19, 16, 0, 30000, 0 ),
		new( 16, NoTag, 2, 20, 15, 0, 30000, 0 ),
		new( 17, NoTag, 2, 21, 17, 0, 30000, 0 ),
		new( 18, NoTag, 2, 22, 17, 0, 30000, 0 ),
		new( 19, NoTag, 2, 23, 17, 0, 30000, 0 ),
		new( 24, NoTag, 0, 28, 12, 0, 30000, 0 ),
		new( 30, NoTag, 2, 33, 21, 0, 30000, 0 ),
		new( 31, NoTag, 2, 34, 21, 0, 30000, 0 ),
		new( 32, NoTag, 2, 35, 21, 0, 30000, 0 ),
		new( 33, NoTag, 2, 36, 21, 0, 30000, 0 ),
		new( 34, NoTag, 2, 37, 21, 0, 30000, 0 ),
		new( 62, NoTag, 2, 65, 9, 0, 30000, 0 ),
		new( 64, NoTag, 2, 67, 9, 0, 30000, 0 ),
		new( 73, NoTag, 2, 75, 10, 0, 30000, 0 ),
		new( 75, NoTag, 2, 77, 10, 0, 30000, 0 ),
		new( 76, NoTag, 2, 78, 10, 0, 30000, 0 ),
		new( 77, NoTag, 2, 79, 11, 0, 30000, 0 ),
		new( 78, NoTag, 2, 80, 19, 0, 30000, 0 ),
		new( 79, NoTag, 2, 81, 11, 0, 30000, 0 ),
		new( 80, NoTag, 2, 82, 11, 0, 30000, 0 ),
		new( 81, NoTag, 2, 83, 12, 0, 30000, 0 ),
		new( 82, NoTag, 2, 84, 0, 0, 30000, 0 ),
		new( 83, NoTag, 2, 85, 12, 0, 30000, 0 ),
		new( 84, NoTag, 2, 86, 12, 0, 30000, 0 ),
		// Rows 93 to 97's lines, a project finished: the text names the item, the post's second word.
		new( 95, 63, 0, 114, 9, 0, 30000, 2 ),
		new( 96, 65, 0, 115, 19, 0, 30000, 2 ),
		new( 97, 74, 0, 116, 10, 0, 30000, 2 ),
		new( 98, 78, 0, 117, 11, 0, 30000, 2 ),
		new( 99, 82, 0, 118, 12, 0, 30000, 2 ),
		new( 100, NoTag, 1, 119, 0, 0, 30000, 0 ),
		// Rows 133 to 136's lines, a kind's group raised (said in Full Simulation alone).
		new( 155, NoTag, 0, 154, 19, 0, 30000, 0 ),
		new( 156, NoTag, 0, 154, 19, 0, 30000, 0 ),
		new( 157, NoTag, 0, 154, 19, 0, 30000, 0 ),
		new( 158, NoTag, 0, 154, 19, 0, 30000, 0 ),
		new( 68, NoTag, 1, 71, 3, 0, 30000, 2 ),
		new( 70, 68, 1, 72, 3, 0, 30000, 2 ),
		new( 71, 68, 1, 73, 3, 0, 30000, 2 ),
		new( 72, 68, 1, 74, 3, 0, 30000, 2 ),
		new( 135, NoTag, 0, 134, 3, 0, 30000, 0 ),
		new( 150, 151, 1, 149, 0, 0, 30000, 0 ),
		new( 151, 150, 1, 150, 0, 0, 30000, 0 ),
		// The golden tickets' lines, each its own group: Local 0 to 5 are 102 to 107, Global 0 to 3 are 108 to 111, the
		// Secret 112 (113, the Secret's index 1, is never awarded). Standing until taken off, and their text the ticket's
		// threshold where the row's kind names one.
		new( 102, NoTag, 3, 180, 0, 0, 0, 5 ),
		new( 103, NoTag, 3, 181, 0, 0, 0, 6 ),
		new( 104, NoTag, 3, 182, 0, 0, 0, 0 ),
		new( 105, NoTag, 3, 183, 0, 0, 0, 0 ),
		new( 106, NoTag, 3, 184, 0, 0, 0, 7 ),
		new( 107, NoTag, 3, 185, 0, 0, 0, 8 ),
		new( 108, NoTag, 3, 186, 0, 0, 0, 0 ),
		new( 109, NoTag, 3, 187, 0, 0, 0, 0 ),
		new( 110, NoTag, 3, 188, 0, 0, 0, 0 ),
		new( 111, NoTag, 3, 189, 0, 0, 0, 0 ),
		new( 112, NoTag, 3, 190, 0, 0, 0, 0 ),
		new( 113, NoTag, 3, 191, 0, 0, 0, 0 )
	];

	/// <summary>How many the list holds, DAT_00750610.</summary>
	internal const int Most = 10;
	/// <summary>How long before its end a tag starts its particle (timer 0x80081, FUN_0048eb20).</summary>
	internal const int WarningBeforeTheEnd = 5000;
	/// <summary>How long after it is put on a tag is shown: the mast posts it message 6 half a second on (LAB_004a11e0, 0x004a1286).</summary>
	internal const int ShowAfter = 500;
	/// <summary>The effect a tag starts over its middle as it is first shown, P_EFFECT_MessageTag1 (FUN_0048dae0's 0x11, 0x0048dfbc).</summary>
	internal const int FirstShowEffect = 0x5a;
	/// <summary>The effect a tag starts over its middle five seconds before its end, P_EFFECT_EndOfMessage (timer 0x80081, 0x0048de18).</summary>
	internal const int EndingEffect = 0x5b;
	/// <summary>How long after it is hidden a tag is taken off the mast (LAB_004a11e0, 0x100002).</summary>
	internal const int OffAfterHidden = 500;
	/// <summary>How long after the one before it each tag goes when the top is right clicked (LAB_004a11e0, 0x100003).</summary>
	internal const int DeleteAllStep = 100;
	/// <summary>The second button that declines an offer, mb_decline: a tag carrying it keeps its timers when opened (FUN_0048d9c0).</summary>
	internal const int DeclineButton = 0x1b;

	/// <summary>The table the game's running language keeps the tags' strings in (DAT_0078ba90).</summary>
	internal const string StringsPath = "Language/English/TAG_SYSTEM.str";

	internal sealed class Tag
	{
		internal required Row Row { get; init; }
		/// <summary>The thing the line was about, nought for none.</summary>
		internal int Subject { get; init; }
		/// <summary>That thing's item, nought for none.</summary>
		internal int Item { get; init; }
		/// <summary>What the message bar would read, written as the tag is made (FUN_00509e10).</summary>
		internal string Text { get; set; } = string.Empty;
		/// <summary>Which of the list's ten records holds it: the first free as it was made. A file is written in this order.</summary>
		internal int Slot { get; init; }
		internal long Born { get; init; }
		/// <summary>When timer 0x80080 hides it; never for a row with no life.</summary>
		internal long HideAt { get; set; } = long.MaxValue;
		/// <summary>When message 5 takes it off the mast.</summary>
		internal long OffAt { get; set; } = long.MaxValue;
		/// <summary>Hidden and still holding its place on the mast.</summary>
		internal bool Hidden { get; set; }
		/// <summary>When the mast's message 6 shows it, <see cref="ShowAfter"/> after it was put on.</summary>
		internal long ShowAt { get; init; }
		/// <summary>On show on the mast: shown by the mast's message 6 and not hidden since.</summary>
		internal bool ShownAt( long now ) => !Hidden && now >= ShowAt;
		/// <summary>Timer 0x80081 has come.</summary>
		internal bool Warned { get; set; }
		/// <summary>Timer 0x80081 has come and its effect waits to be started where the tag stands, which the gadget knows.</summary>
		internal bool EndingDue { get; set; }
		/// <summary>Opened with its timers stopped: its effect waits to be killed (FUN_0048d9c0).</summary>
		internal bool EndingStopped { get; set; }
		/// <summary>The control's property 9: its <see cref="EndingEffect"/>'s handle, 0 before one is started.</summary>
		internal int Ending { get; set; }
		/// <summary>Where its <see cref="EndingEffect"/> was last put, in the units effects are started at.</summary>
		internal (int X, int Z) EndingAt { get; set; }
		/// <summary>The control's property 0xa: its first show has been and asked for <see cref="FirstShowEffect"/>.</summary>
		internal bool FirstShown { get; set; }
		/// <summary>Whether the gadget last drew it shown, to tell a show or a hide (message 0x11) from no change.</summary>
		internal bool Showing { get; set; }
		/// <summary>The control's +0x134: its two timers run. Set as a tag with a life is made, cleared as it is opened.</summary>
		internal bool TimersRun { get; set; }
		/// <summary>When its message was last put on the bar.</summary>
		internal long OpenedAt { get; set; }
	}

	private readonly List<Tag> _tags = [];
	private readonly Row[] _rows;

	internal ParkTags( IEnumerable<Row>? rows = null ) => _rows = (rows ?? Rows).ToArray();

	/// <summary>The tags' strings, each as its parts (<see cref="StringsPath"/>); with none a tag's text is empty.</summary>
	internal StringPart[][]? Strings { get; set; }

	/// <summary>The park's golden-ticket thresholds, which the ticket rows' kinds 5 to 8 write (the globals 0x00785790 to 0x007857a8).</summary>
	internal ParkGoldenTickets.Thresholds Thresholds { get; set; }

	/// <summary>The tags on the mast, the oldest first: the oldest stands lowest.</summary>
	internal IReadOnlyList<Tag> Standing => _tags;
	/// <summary>One up at every change of the list or of a tag's showing.</summary>
	internal int Changes { get; private set; }
	/// <summary>How many tags the list has taken, for the new tag's sound.</summary>
	internal int Made { get; private set; }
	/// <summary>The tag whose message the bar shows (DAT_007ca090), or none: the one put on show as the arm starts out with the bar.</summary>
	internal Tag? OnShow { get; private set; }

	/// <summary>The tag last left clicked to the bar (DAT_007ca094), until the arm is fully out with it; its right click is refused.</summary>
	internal Tag? PuttingOnShow { get; private set; }

	/// <summary>
	/// A line of response group <paramref name="group"/> about <paramref name="subject"/>, a thing of item
	/// <paramref name="item"/> called <paramref name="itemName"/>, was said. A tag of the row's key about the same
	/// thing and item is taken off at once, or with a row that is its own key takes the new text and is left as
	/// the line's tag; otherwise the list takes a new tag while it holds fewer than ten.
	/// </summary>
	internal Tag? Said( int group, int subject, long now, int item = 0, string? itemName = null )
	{
		var row = Array.Find( _rows, candidate => candidate.Id == group );
		if ( row == null )
		{
			// A group with a row the table holds and this list does not.
			if ( group != NoTag && group != NoTag - 1 )
				Unimplemented.Report( "TAG_ROW_NOT_LISTED" );
			return null;
		}

		return Take( row, subject, item, TextOf( row, item, itemName ), now );
	}

	/// <summary>
	/// The messages control's list taking a tag (FUN_0048eb20): the standing tags of <paramref name="row"/>'s key
	/// about the same thing and item, then the new tag in the first free record.
	/// </summary>
	private Tag? Take( Row row, int subject, int item, string text, long now )
	{
		var fresh = true;
		foreach ( var tag in _tags.ToArray() )
		{
			if ( tag.Row.Id != row.Key || tag.Subject != subject || tag.Item != item ) continue;
			if ( row.Id == row.Key )
			{
				tag.Text = text;
				fresh = false;
			}
			else
				TakeOff( tag );
		}

		if ( !fresh || _tags.Count >= Most ) return null;

		var slot = Enumerable.Range( 0, Most ).First( free => _tags.All( tag => tag.Slot != free ) );
		var made = new Tag { Row = row, Subject = subject, Item = item, Text = text, Slot = slot, Born = now, ShowAt = now + ShowAfter };
		if ( row.Life > 0 )
		{
			made.HideAt = now + row.Life;
			made.TimersRun = true;
		}

		_tags.Add( made );
		++Changes;
		++Made;
		Log.Info( $"Park tags: tag {row.Id} picture {row.Picture} subject {subject} life {row.Life} standing {_tags.Count}" );
		return made;
	}

	/// <summary>
	/// A tag's text by its row's kind (FUN_00509e10): the row's string with the kind's variables where their parameters
	/// stand (<see cref="Arguments"/>). Any other kind, and a kind 2 about no item (which the original logs and leaves
	/// its buffer as it stood), is counted and written empty.
	/// </summary>
	internal string TextOf( Row row, int item, string? itemName )
	{
		if ( Strings is not { } strings || row.String < 0 || row.String >= strings.Length )
			return string.Empty;

		if ( Arguments( row.Kind, item, itemName ) is not { } arguments )
		{
			Unimplemented.Report( "TAG_TEXT_KIND" );
			return string.Empty;
		}

		var text = new System.Text.StringBuilder();

		foreach ( var part in strings[row.String] )
		{
			if ( part.Text != null )
				text.Append( part.Text );
			else
				text.Append( arguments.FirstOrDefault( argument => argument.Parameter == part.Parameter ).Value );
		}

		return text.ToString();
	}

	/// <summary>
	/// The variables a kind hands the formatter, each with its parameter's number: kind 0 none; kind 2 the item's name
	/// as parameter 0; kinds 5, 6 and 7 the Local tickets' Visitors, PeopleInPark and ProfitYear as parameter 1; kind 8
	/// RecentVisitors as parameter 1 and RecentVisitorMonths as 2 (FUN_004a77a0 makes a variable of its number). Null
	/// for a kind not built.
	/// </summary>
	private (int Parameter, string? Value)[]? Arguments( int kind, int item, string? itemName ) => kind switch
	{
		0 => [],
		2 when item != 0 => [(0, itemName)],
		5 => [(1, Number( Thresholds.Visitors ))],
		6 => [(1, Number( Thresholds.PeopleInPark ))],
		7 => [(1, Number( Thresholds.ProfitYear ))],
		8 => [(1, Number( Thresholds.RecentVisitors )), (2, Number( Thresholds.RecentVisitorMonths ))],
		_ => null
	};

	/// <summary>
	/// A number variable as its writer letters it (0x006add40): "%d" of the value word, which holds the number's size,
	/// the sign word unread, so a negative number is written without its sign.
	/// </summary>
	private static string Number( int value ) => (value < 0 ? unchecked( -value ) : value).ToString( System.Globalization.CultureInfo.InvariantCulture );

	/// <summary>
	/// The tags as a park file holds them (FUN_0048ee50): every record in use, a hidden tag's too, in the order of
	/// the list's ten records, each with its whole life and not what is left of it.
	/// </summary>
	internal IReadOnlyList<ParkTagRecords.Record> Written()
		=> [.. _tags.OrderBy( tag => tag.Slot ).Select( tag => new ParkTagRecords.Record( tag.Row.Id, tag.Text,
			tag.Row.Life, tag.Row.Picture, tag.Row.Button1, tag.Row.Button2, tag.Subject, tag.Item ) )];

	/// <summary>
	/// A park file's tags put on the aerial (FUN_0048ef70): each handed to the list in the file's order as a new
	/// tag whose key is <see cref="NoTag"/>, so it takes no other's place, with the file's text, picture, buttons,
	/// thing and item, and its whole life from <paramref name="now"/>. The picture and the life are the file's, so
	/// a tag of a row this list does not hold stands as any other.
	/// </summary>
	internal void Restore( IReadOnlyList<ParkTagRecords.Record> saved, long now )
	{
		foreach ( var record in saved )
		{
			var row = new Row( record.Id, NoTag, record.Picture, -1, record.Button1, record.Button2, record.Life, 0 );
			Take( row, record.Thing, record.Item, record.Text, now );
		}

		if ( saved.Count > 0 )
			Log.Info( $"Park tags: {saved.Count} of the file's put back, standing {_tags.Count}" );
	}

	/// <summary>
	/// A tag's message asked for on the bar (FUN_0048d9c0): its two timers are stopped, unless its second button is
	/// <see cref="DeclineButton"/>, so it no longer hides at its life's end, and it is the tag being put on show. It
	/// is on show once the arm starts out with the bar (<see cref="Shown"/>). False, and nothing done, for the tag
	/// already on show or one not standing.
	/// </summary>
	internal bool Open( Tag tag, long now )
	{
		if ( tag == OnShow || !_tags.Contains( tag ) ) return false;

		if ( tag.TimersRun && tag.Row.Button2 != DeclineButton )
		{
			tag.TimersRun = false;
			tag.HideAt = long.MaxValue;
			tag.EndingStopped = true;
		}

		PuttingOnShow = tag;
		Log.Info( $"Park tags: tag {tag.Row.Id} subject {tag.Subject} opened, timers {(tag.TimersRun ? 1 : 0)}" );
		return true;
	}

	/// <summary>
	/// The arm starts out with the bar (the bar's 0x1001d9): the tag being put on show is the one on show, swelling
	/// from <paramref name="now"/>; one taken off the mast meanwhile leaves none on show.
	/// </summary>
	internal void Shown( long now )
	{
		OnShow = PuttingOnShow is { } tag && _tags.Contains( tag ) ? tag : null;

		if ( OnShow != null )
			OnShow.OpenedAt = now;

		++Changes;
	}

	/// <summary>The arm fully out with the bar (the bar's 0x1d with 1): no tag is being put on show.</summary>
	internal void FullyOut() => PuttingOnShow = null;

	/// <summary>The bar taken off the arm (the bar's 0x1001d8): no tag is on show.</summary>
	internal void Shut()
	{
		if ( OnShow == null ) return;
		OnShow = null;
		++Changes;
	}

	/// <summary>
	/// The timers' turn: a tag's particle five seconds before its end, asked of the gadget (<see cref="Tag.EndingDue"/>),
	/// its hiding at its end, and its going.
	/// </summary>
	internal void Tick( long now )
	{
		foreach ( var tag in _tags.ToArray() )
		{
			if ( tag.TimersRun && !tag.Warned && now >= tag.Born + tag.Row.Life - WarningBeforeTheEnd )
			{
				tag.Warned = true;
				tag.EndingDue = true;
				Log.Info( $"Park tags: tag {tag.Row.Id} subject {tag.Subject} timer 0x80081 at age {now - tag.Born}" );
			}

			if ( !tag.Hidden && now >= tag.HideAt ) Hide( tag, now );
			if ( now >= tag.OffAt ) TakeOff( tag );
		}
	}

	/// <summary>A tag's own right click, and its life's end: hidden at once, off the mast half a second on.</summary>
	internal void Hide( Tag tag, long now )
	{
		if ( tag.Hidden || !_tags.Contains( tag ) ) return;
		tag.Hidden = true;
		tag.OffAt = Math.Min( tag.OffAt, now + OffAfterHidden );
		++Changes;
	}

	/// <summary>The top's right click: every tag goes, the oldest first, a tenth of a second apart.</summary>
	internal void DeleteAll( long now )
	{
		for ( var i = 0; i < _tags.Count; ++i )
			_tags[i].OffAt = Math.Min( _tags[i].OffAt, now + DeleteAllStep * (i + 1) );
	}

	private void TakeOff( Tag tag )
	{
		_tags.Remove( tag );
		if ( tag == OnShow ) OnShow = null;
		++Changes;
		Log.Info( $"Park tags: tag {tag.Row.Id} subject {tag.Subject} off, standing {_tags.Count}" );
	}

	/// <summary>The tags standing, one line: a text's line breaks are written as \n.</summary>
	internal string Census( long now ) => $"tags: standing={_tags.Count} "
		+ string.Join( " ", _tags.Select( tag =>
			$"tag={tag.Row.Id}/picture={tag.Row.Picture}/subject={tag.Subject}/age={now - tag.Born}/hidden={(tag.Hidden ? 1 : 0)}"
			+ $"/item={tag.Item}/slot={tag.Slot}/life={tag.Row.Life}/timers={(tag.TimersRun ? 1 : 0)}/open={(tag == OnShow ? 1 : 0)}/text=\"{tag.Text.Replace( "\n", "\\n" )}\""
			+ $"/shown={(tag.ShownAt( now ) ? 1 : 0)}/ending={tag.Ending:x}" ) );
}
