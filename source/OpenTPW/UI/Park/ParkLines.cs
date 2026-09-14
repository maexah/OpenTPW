namespace OpenTPW.UI;

/// <summary>
/// What a park has the advisor say, and when.
///
/// <para>
/// The advisor only says what he is handed - see <see cref="Advisor"/> - so this is a park's side of
/// that, standing beside <see cref="FrontEndLines"/> the way <see cref="ParkFrontEnd"/> stands beside
/// <see cref="FrontEnd"/>. Saying a line is the advisor's and is engine; which lines a park uses, and
/// when, is here and is content.
/// </para>
/// <para>
/// <b>The samples are read out of the original's response table (0x00768fb8), and which line is which
/// was confirmed from its transcript rather than inferred from the ids.</b> That caution is not
/// ceremony. The table's own message-group column looks as though it should line up with the advisor's
/// compiled section list, and it does for the first twenty-odd - message group 24 really is
/// StaffGuardBuildCamera, "if you build some security cameras your guards would be more efficient" -
/// and then it slips: by one around StaffTrainHandymen, by three through the rides, and by five from
/// RidesBreakdownNoMechanics onwards. The drift accumulates in steps rather than jumping once, so
/// there is no offset that recovers it. <b>No id in this file was arrived at by counting.</b>
/// </para>
/// <para>
/// <b>Why there is only one line.</b> Nearly all of data\Advisor\Advisor.sam is about staff, visitors,
/// rides, research or money, and a park has none of those yet. Of what is left, two roads that look
/// open are not:
/// <list type="bullet">
/// <item>
/// <b>The original's own Welcome is a stub.</b> Its four responses - 399, 400, 401 and 402 - all carry
/// sample 1, and sample 1 is an OpenPark line: "people want to come in but your park is closed, you
/// should think about opening up". A park has no open or closed state here, and saying that on arrival
/// would be worse than saying nothing, so it is not said.
/// </item>
/// <item>
/// <b>The line a screen posts as it opens cannot be recovered.</b> FUN_00486b00 is handed a message id,
/// not a response id: the disassembly puts the caller's argument at +0x0c of the message record, which
/// is the field the accept gate keys its per-message counters on, and message ids are resolved through
/// the table at 0x0076e300 - filled at runtime, and all zeros in the image. So there is no static road
/// from a screen to its line, and guessing one would be inventing speech.
/// </item>
/// </list>
/// </para>
/// </summary>
internal static class ParkLines
{
	/// <summary>
	/// The samples behind the park's response ids, from the original's response table (0x00768fb8).
	/// </summary>
	private static class Samples
	{
		/// <summary>
		/// Response 404, transcribed: "see the control panel over there at the bottom left part of the
		/// screen - the buttons on it do a bunch of different things, click them and I'll tell you more.
		/// That meter on the left side of the panel shows how happy the visitors are, if there are any
		/// visitors that is."
		///
		/// <para>
		/// It describes what <see cref="ParkGadget"/> already is, corner for corner: the panel in the
		/// bottom left, the row of buttons on it, and the meter down its left side. <b>And the line is
		/// honest about the park it is describing</b> - it ends "if there are any visitors that is",
		/// which is exactly the state the gauge rests in, having no happiness to show.
		/// </para>
		/// <para>
		/// A real recording rather than one of the bank's 80 silent stubs: it decodes to 15.4 seconds,
		/// and lips.WAD carries its sp_377.LIP, so his mouth moves through it.
		/// </para>
		/// </summary>
		public const int ControlPanel = 377;
	}

	/// <summary>
	/// The line a park opens with: what the gadget in the corner is, said once.
	///
	/// <para>
	/// Queued rather than flushed - there is nothing for it to cut off on arrival, and flushing is for a
	/// line that has to be heard now.
	/// </para>
	/// <para>
	/// <b>A departure, and a small one.</b> The original's tutorial messages are MessageGroups[1] in
	/// Advisor.sam - <c>SayOnlyOnce 1</c>, so one is said to a player once and never again, and
	/// <c>DiscardAfterSlaps 3</c>, so slapping him three times is how you get rid of it.
	/// </para>
	/// <para>
	/// <b>That THIS line is one of them is inferred from what it says, not read from the tables, and an
	/// earlier draft of this remark stated it as fact until a review caught it.</b> Response 404 carries
	/// message group 383, which is the value meaning no group at all, so nothing files it anywhere that
	/// can be read back.
	/// </para>
	/// <para>
	/// Either way the two settings have nowhere to live here - nothing writes a park back, and a player's
	/// saved options do not carry what he has already been told - so it is said once a visit, which is the
	/// closest thing that keeps its meaning, and should become once ever when there is somewhere to keep
	/// that.
	/// </para>
	/// </summary>
	internal static void ExplainGadget() => Advisor.Current?.Add( Samples.ControlPanel, flush: false );
}
