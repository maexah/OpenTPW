namespace OpenTPW.UI;

/// <summary>
/// What the lobby's front end has the advisor say, and when.
///
/// <para>
/// The original's front end queues these lines on him itself, at its own call sites, through AdvisorQueue_Add
/// (0x005d6110): the greeting as the player slots open (FrontEnd_ShowPlayerSlots, 0x004a6580), how to fill in the
/// new player dialog as it opens (0x004a6e40), and the tour of the lobby or the Instant Action line once a player is
/// made (FrontEnd_ClosePlayerSlots, 0x004a6a50). The advisor only says what he is handed - see
/// <see cref="Advisor"/>.
/// </para>
/// <para>
/// <b>Engine and content.</b> This is content: which samples the lobby uses, the rule that picks its greeting, and
/// when in the tour the key is handed over. Saying a line - the queue, the lead-in, the clips he talks through, the
/// cue, the cry and the duck - is the advisor's, and is engine.
/// </para>
/// </summary>
internal static class FrontEndLines
{
	/// <summary>
	/// The samples behind the lobby's response ids, from the original's response table
	/// (0x00768fb8). Which line is which was confirmed by listening, not inferred from the ids.
	/// </summary>
	private static class Samples
	{
		/// <summary>Response 390: "Welcome to Sim Theme Park! I'm the advisor around here..."</summary>
		public const int Welcome = 465;

		/// <summary>Response 391: "...I don't even know your name! ...click on the New Player button."</summary>
		public const int AskForName = 466;

		/// <summary>Response 398: "Welcome to Sim Theme Park! Don't I know you?..."</summary>
		public const int WelcomeBack = 471;

		/// <summary>
		/// Response 570: "First type your name into the text box, then click a button to choose an
		/// Instant Action or a Full Simulation game. When you're finished, click the checked button..."
		/// </summary>
		public const int NewPlayerDialog = 588;

		/// <summary>
		/// Response 393: "...this area is called the lobby... You need golden keys to enter the parks.
		/// Here's one now to get you started. This key will get you into the Halloween World and Lost
		/// Kingdom parks right away..."
		/// </summary>
		public const int LobbyTour = 468;

		/// <summary>
		/// Response 394, what a new Instant Action player hears in place of the tour: "...just click on the gate
		/// of the park you want to play in first and the fun can begin" (transcribed).
		/// </summary>
		public const int InstantActionStart = 469;
	}

	/// <summary>What a player with no saved game hears, in order - responses 390 and 391.</summary>
	private static readonly int[] NewPlayerLines = { Samples.Welcome, Samples.AskForName };

	/// <summary>
	/// When the key in his tour of the lobby is handed over, counted from the moment he is given the
	/// line.
	///
	/// Response 393's row of the gesture table at 0x0076dc18 (row 12) holds no clips - he talks through
	/// random ones as usual - but one timed event, flag 0x400000 with 17000ms, counted from the clock
	/// minus 200 that the line was given at (0x00598bf0). When it comes due, Advisor_Update
	/// (0x00599880) plays goldkey (effect 198 of the interface's sounds), starts a burst of particles
	/// (effect 87 of data\Particle\Tp2.plb) and refreshes the lobby panel (0x004b9340), which is the
	/// moment the new key shows up on it - some way into "Here's one now to get you started". What
	/// the cue does is the caller's; the front end does all three.
	/// </summary>
	private const float TourKeySeconds = 16.8f;

	/// <summary>
	/// What FrontEnd_ShowPlayerSlots says, going by how many of the slots hold a player
	/// (Players_CountUsedSlots, 0x005c7bb0): with none, response 390 and then 391 behind it; otherwise the
	/// welcome back, response 398. It says it as the lobby opens, and again when Select New Player brings
	/// the slots back.
	/// </summary>
	internal static void Greet( int usedSlots )
	{
		if ( Advisor.Current is not { } advisor )
			return;

		if ( usedSlots == 0 )
		{
			advisor.Add( NewPlayerLines[0], flush: true );

			for ( int i = 1; i < NewPlayerLines.Length; ++i )
				advisor.Add( NewPlayerLines[i], flush: false );
		}
		else
		{
			advisor.Add( Samples.WelcomeBack, flush: true );
		}
	}

	/// <summary>
	/// What the new player dialog opens with (0x004a6e40): how to fill it in, cutting in on whatever he
	/// was saying.
	/// </summary>
	internal static void ExplainNewPlayer() => Advisor.Current?.Add( Samples.NewPlayerDialog, flush: true );

	/// <summary>
	/// What FrontEnd_ClosePlayerSlots (0x004a6a50) has him say once a new player has been made: his
	/// tour of the lobby, and the golden key he hands over, which <paramref name="keyHandedOver"/> is
	/// called for - see <see cref="TourKeySeconds"/>.
	/// </summary>
	internal static void GiveLobbyTour( Action keyHandedOver )
		=> Advisor.Current?.Add( Samples.LobbyTour, flush: true, TourKeySeconds, keyHandedOver );

	/// <summary>What FrontEnd_ClosePlayerSlots has him say instead to a new Instant Action player, who is given no key (response 394).</summary>
	internal static void ExplainInstantAction() => Advisor.Current?.Add( Samples.InstantActionStart, flush: true );
}
