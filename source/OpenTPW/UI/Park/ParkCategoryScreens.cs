namespace OpenTPW.UI;

/// <summary>
/// The three category buttons on the management gadget, and which screen each opens - the original's
/// <c>FUN_004a0940( n )</c>.
///
/// <para>
/// <b>Three of the HUD's six buttons are CATEGORY PICKERS, and that is why the gadget's button
/// count and the shortcut table never reconciled.</b> Each of the three category buttons remembers
/// the screen it was last left on and re-opens that one, out of three globals the original seeds to
/// <b>1, 3 and 10</b>. So the Money button opens on the <i>entry price</i> and the Information button
/// on <i>park status</i> until a player moves them.
/// </para>
///
/// <para>
/// <b>A gate that will look like a bug and is not.</b> <c>FUN_004a0940</c> does nothing at all while
/// the in-game menu is on screen - <c>FUN_0048c8d0</c> tests that the menu object exists AND is
/// shown. So a category button refusing while the pause menu is up is correct behaviour rather than
/// an unbuilt path. <see cref="UiWindow.Modal"/> is how this project keeps the same click away: the
/// game menu is modal, and nothing behind a modal window can be pressed.
/// </para>
/// </summary>
internal static class ParkCategoryScreens
{
	/// <summary>The buy/hire pair, seeded to <b>1</b> - already built, and reached by its own button.</summary>
	public const int Building = 1;

	/// <summary>Park status, staff, items and visitors. Seeded to <b>3</b>, park status.</summary>
	public const int Information = 2;

	/// <summary>Finances, loans, staff costs and the entry price. Seeded to <b>10</b>, the entry price.</summary>
	public const int Finance = 3;

	/// <summary>
	/// Which screen each category is currently left on, in the original's own numbering:
	/// 1 buy, 2 hire, 3 park status, 4 all staff, 5 all items, 6 all visitors, 7 finances, 8 loans,
	/// 9 staff costs, 10 entry price.
	/// </summary>
	/// <remarks>
	/// <b>Information is seeded to 4 where the original seeds 3, and that is a DECLARED deviation.</b>
	/// <c>FUN_004a0810</c> is called with (2,3) for park status, which needs the top three thoughts, an
	/// arrival rate, a park rating and a multi-year history - none of which exists here. Seeded to the
	/// original's 3, the Information button would open a screen that reports itself unbuilt and shows
	/// nothing, which is worse than the button doing nothing at all. It is seeded to the staff list
	/// instead, and <b>this line goes back to 3 the day park status is built</b>. The other two are the
	/// original's own numbers.
	/// </remarks>
	private static readonly Dictionary<int, int> LastLeftOn = new()
	{
		[Building] = 1,
		[Information] = 4,
		[Finance] = 10
	};

	/// <summary>Opens whichever screen a category was last left on.</summary>
	public static void Open( WindowStack stack, int category )
	{
		if ( !LastLeftOn.TryGetValue( category, out var screen ) )
			return;

		Show( stack, category, screen );
	}

	/// <summary>
	/// Opens one screen by the original's own number, and <b>remembers it</b> as the category's screen
	/// so the button re-opens it next time.
	/// </summary>
	public static void Show( WindowStack stack, int category, int screen )
	{
		LastLeftOn[category] = screen;

		switch ( screen )
		{
			case 4:
				stack.Open( new ParkStaffScreen( stack ) );
				return;

			case 5:
				stack.Open( new ParkItemsScreen( stack ) );
				return;

			case 6:
				stack.Open( new ParkVisitorsScreen( stack ) );
				return;

			case 10:
				stack.Open( new ParkEntryPriceScreen( stack ) );
				return;
		}

		// Everything else is counted rather than drawn. Each is named, because "not built" tells
		// nobody what to build - and each names what it is waiting on, because five of these are not
		// screens this project is choosing to skip: they rest on simulation that does not exist.
		var (name, waiting) = screen switch
		{
			3 => ("PARK_STATUS_SCREEN", "the top three thoughts, the arrival rate, a park rating and a multi-year history"),
			// Dead by CODE: screens 4 and 6 are opened above and return before this switch.
			4 => ("ALL_STAFF_SCREEN", "nothing - it is built and opened above"),
			6 => ("ALL_VISITORS_SCREEN", "nothing - it is built and opened above"),
			7 => ("FINANCE_SCREEN", "the monthly cash-in and cost ring buffers, and the year graph"),
			8 => ("LOANS_SCREEN", "the mLoans[] records, which ParkWorld does not read"),
			9 => ("STAFF_COSTS_SCREEN", "training budgets, other costs and loan repayments"),
			_ => ("RESEARCH_SCREEN", "researchers, research groups and per-group research points")
		};

		Unimplemented.Report( name );

		Log.Info( $"Park gadget: {name} is not built - it waits on {waiting}" );
	}
}
