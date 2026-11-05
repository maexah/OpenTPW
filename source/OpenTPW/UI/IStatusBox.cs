namespace OpenTPW.UI;

/// <summary>
/// A window with a status box over its model, which the overlay pass draws after the HUD (<see cref="ParkObjectWindow"/>,
/// <see cref="ParkShopWindow"/>); <see cref="Level"/> draws the active one's.
/// </summary>
internal interface IStatusBox
{
	void DrawStatus();
}
