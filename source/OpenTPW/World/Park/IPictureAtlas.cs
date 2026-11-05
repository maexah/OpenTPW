namespace OpenTPW;

/// <summary>
/// The one texture <see cref="ParkGuestSprites"/> packs every picture into, as far as the pool knows it: something it can
/// let go. <see cref="Texture"/> is the game's; a test's stand-in records when it is let go (<see cref="ParkGuestSprites.GuestArt"/>).
/// </summary>
internal interface IPictureAtlas
{
	void Delete();
}
