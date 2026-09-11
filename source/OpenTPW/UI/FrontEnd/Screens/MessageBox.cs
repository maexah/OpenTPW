namespace OpenTPW.UI;

/// <summary>
/// A question with a cross and a tick - what Quit Game asks.
///
/// <para>
/// 0x0047f020 opens it, modal, from the layout stream at 0x0074f920: the plain dialog, w_dialog.md2,
/// with the text centred and wrapped in white in font 4 between x 610 and 1345, and three button
/// places down its right side. Each caller says what each place holds - b_exit, b_okay or nothing -
/// and what each does; a place with nothing to do just closes the box.
/// </para>
/// <para>
/// Quit Game (0x004a61d0) puts the cross in the bottom place, the tick above it, and leaves the top
/// one empty, with "QUIT GAME / Are you sure you want to quit the game ?" (UITEXT 9).
/// </para>
/// <para>
/// It takes no keys. Its callback (0x0047eed0) answers its buttons and its own closing and nothing
/// else; Enter and Escape reach the new player dialog only because its name box sends them.
/// </para>
/// </summary>
internal sealed class MessageBox : UiWindow
{
	private const int MessageFont = 4;

	private readonly Action _onTick;

	public MessageBox( FrontEnd frontEnd, string text, Action onTick ) : base( frontEnd )
	{
		_onTick = onTick;
		Modal = true;

		Root = Backdrop();

		var box = Root.Add( new UiControl
		{
			Id = 0x9873c7,
			Rect = new UiRect( 500, 233, 1516, 830 ),
			Mesh = UiMesh.Get( "w_dialog" ),
			TextRect = new UiRect( 610, 276, 1345, 753 ),
			Text = text,
			Font = MessageFont,
			TextWraps = true
		} );

		box.Add( new UiButton
		{
			Id = 0x9873c8,
			Rect = new UiRect( 1387, 610, 1470, 694 ),
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => FrontEnd.Close( this )
		} );

		box.Add( new UiButton
		{
			Id = 0x9873c9,
			Rect = new UiRect( 1379, 522, 1462, 605 ),
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = Tick
		} );
	}

	private void Tick()
	{
		FrontEnd.Close( this );
		_onTick();
	}
}
