namespace OpenTPW.UI;

/// <summary>
/// The line that says a newer weekly build is out (Q941, <see cref="UpdateChecker"/>), on its own line directly above the build label
/// and right-aligned with it (<see cref="VersionLabel.NoticeBox"/>), in the label's font. It acts as one of the game menu's choices
/// does (<see cref="MenuLine"/>): the lobby's gold at rest (<see cref="Resting"/>, Alexah's choice), white under the pointer; it is clickable, and opens the offered release's
/// page in the player's browser. <b>OpenTPW's own, a deviation</b>: the original has no such line, so the words are new English. It
/// is shown when the check has found a newer build, with no way to dismiss it, and it never opens a menu or stops anything. The
/// words do not name the tag (the log does); the link goes to that tag's page.
///
/// <para>
/// In the lobby it is its own panel (<see cref="LobbyPanel"/>) over <see cref="VersionLabel"/>, which takes no pointer, and hands its
/// control to the window stack to hit (<see cref="WindowStack.Notice"/>). In the Escape menu (<see cref="GameMenu"/>) the menu makes one
/// for itself, because the menu's dimmer takes the pointer.
/// </para>
/// </summary>
internal static class UpdateNotice
{
	/// <summary>The control's id: OpenTPW's own, beside the label's (<see cref="VersionLabel.ControlId"/>).</summary>
	internal const int ControlId = 0x8cfd56;

	/// <summary>The line's resting colour: the lobby's gold, the player slots' label (<see cref="PlayerSlots.Label"/>, 0x007540c0).</summary>
	internal static UiColour Resting => PlayerSlots.Label;

	/// <summary>What the line says. The tag is in the log and in the link, not here.</summary>
	internal const string Words = "Update available";

	/// <summary>A control for the line: opens the page of the offer it is given when clicked, and behaves as a menu choice does under the pointer.</summary>
	internal static MenuLine Make()
	{
		var line = new MenuLine
		{
			Id = ControlId,
			RestColour = Resting,
			Font = VersionLabel.LabelFont,
			Text = Words,
			TextShadow = true,
			TextAcross = TextAlign.Start,
			TextDown = TextAlign.Centre
		};

		line.LeftClicked = UpdateChecker.OpenReleasePage;

		return line;
	}

	/// <summary>The lobby's notice: a panel under the windows, bottom right, over the label whose text it is told.</summary>
	internal sealed class LobbyPanel : Panel
	{
		private readonly string _label;

		private MenuLine? _control;

		private UpdateOffer? _offer;

		private float _ticks;

		public LobbyPanel( string label )
		{
			_label = label;
		}

		/// <summary>The control the window stack hits, or null while there is no newer build.</summary>
		internal UiControl? Control => _control;

		protected override void OnUpdate()
		{
			var offer = UpdateChecker.Available;

			// A check that lands while the lobby is up shows at once. The control is made once for an offer and goes with it.
			if ( offer == null )
			{
				_control = null;
				_offer = null;
				return;
			}

			if ( _control == null || !ReferenceEquals( offer, _offer ) )
			{
				_offer = offer;
				_control = Make();
				Log.Info( $"Front end: the update notice reads '{Words}' for {offer.Tag}" );
			}

			if ( VersionLabel.NoticeBox( _label, Words ) is { } box )
				_control.Rect = box;

			_ticks = MathF.Min( _ticks + (Time.Delta * MenuLine.TicksPerSecond), MenuLine.TicksPerSecond );

			for ( ; _ticks >= 1f; _ticks -= 1f )
				_control.Tick();
		}

		protected override void OnRender() => _control?.Draw();

		protected override void OnDelete() => _control?.ReleaseText();
	}
}
