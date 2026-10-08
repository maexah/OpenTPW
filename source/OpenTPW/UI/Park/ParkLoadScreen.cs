namespace OpenTPW.UI;

/// <summary>
/// Load Park, the park menu's first row: the player's saved parks for this theme, one row each, and a click on a row
/// loads it over the running park with no question (<c>FUN_0049efb0</c>; <c>docs/exe/saves.md</c>, "One screen, two
/// uses" to "What the handlers answer").
///
/// <para>
/// <b>The layout</b> is <see cref="ParkFileScreen"/>'s, which Save Park shares; the stream's OK button and name box
/// Load hides (<c>0x0049f02a</c>, <c>0x0049f043</c>) and this does not build.
/// </para>
/// <para>
/// <b>It takes no keys</b>: the handler <c>FUN_0049e880</c> answers the cancel button, a row's click and its own
/// closing, and hands the rest to the modal base, which drops them.
/// </para>
/// </summary>
internal sealed class ParkLoadScreen : ParkFileScreen
{
	public ParkLoadScreen( WindowStack stack, string theme ) : base( stack, theme, UIStrings.LoadPark )
	{
		Fill( "Load Park" );
	}

	/// <summary>
	/// A click on a row (<c>0x400</c>): the folder is read again, the entry at the row's place is loaded, and the
	/// screen closes. A place the folder no longer has loads nothing.
	/// </summary>
	/// <remarks>
	/// <b>A deviation.</b> The original takes the running park down where it stands and reads the file over it, with
	/// no loading screen and no change of scene (<c>FUN_00414d40</c>; <c>docs/exe/saves.md</c>, "The load"). Here the
	/// park's scene is ended and built again from the file behind the loading screen, as Restart Park's is.
	/// </remarks>
	protected override void RowClicked( int row )
	{
		var parks = Folder();

		Stack.Close( this );

		if ( row < 0 || row >= parks.Count )
		{
			Log.Info( $"Load Park: row {row} is no longer in the folder, so nothing is loaded" );
			return;
		}

		Log.Info( $"Load Park: loading '{parks[row].Name}' from {parks[row].Path}" );
		Game.RequestParkLoad( Theme, parks[row].Path );
	}
}
