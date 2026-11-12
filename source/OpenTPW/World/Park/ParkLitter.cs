namespace OpenTPW;

/// <summary>
/// A cell's litter as it is drawn: the picture of <c>SPR_LI</c>'s set for its kind, laid flat on the ground at its sprite's
/// place (<c>docs/exe/ride-operation.md</c>, "A cell's litter" and "What it is drawn as"). The sprite's flags are
/// <c>0x80000</c>, so <c>Sprite_BuildQuad</c>'s flat arm lays it out: tested against depth and writing none, never pulled
/// towards the eye and never picked.
/// </summary>
internal static class ParkLitter
{
	/// <summary>The folder the litter bank is read from, <c>Generic\Litter</c> of <c>esprites.wad</c>: one bank, <c>SPR_LI</c>.</summary>
	internal const string Folder = "esprites/Generic/Litter";

	/// <summary>A texel's length across, in world x: the span 20 over the 128 every picture stores, times the 0.75 the flat arm adds.</summary>
	internal const float Across = ParkPersonShadows.Span * ParkPersonShadows.Across;

	/// <summary>A texel's length down, in world y (the original's z): the span alone.</summary>
	internal const float Down = ParkPersonShadows.Span;

	/// <summary>
	/// The four corners of a litter picture whose sprite stands at <paramref name="anchor"/>, flat at <paramref name="height"/>
	/// (the land under the anchor plus the lift, one height for all four). With the picture's signed origin o (the <c>.TPC</c>'s,
	/// which <see cref="ParkGuestSprites.Region"/> holds negated): across from o × 0.1171875 to (width + o) × 0.1171875 and down from
	/// o × 0.15625 to (height + o) × 0.15625 (<c>0x0058922d</c>..<c>0x00589256</c>, <c>0x00589329</c>..<c>0x0058934f</c>). Corner 0
	/// is the picture's top left texel at the smallest x and y, 1 its top right, 2 bottom right, 3 bottom left (the batch add
	/// <c>0x00566220</c>), fixed in the world whatever the camera's turn. White at <paramref name="alpha"/>.
	/// </summary>
	internal static void Corners( Span<Vertex> corners, Vector2 anchor, float height, ParkGuestSprites.Region picture, int alpha = 0xff )
	{
		var left = anchor.X - (picture.OriginX * Across);
		var right = anchor.X + ((picture.Width - picture.OriginX) * Across);
		var top = anchor.Y - (picture.OriginY * Down);
		var bottom = anchor.Y + ((picture.Height - picture.OriginY) * Down);
		var colour = ((uint)Math.Clamp( alpha, 0, 255 ) << 24) | 0x00ffffffu;

		corners[0] = new Vertex( new Vector3( left, top, height ), new Vector2( picture.Left, picture.Top ) ) { MatFlags = colour };
		corners[1] = new Vertex( new Vector3( right, top, height ), new Vector2( picture.Right, picture.Top ) ) { MatFlags = colour };
		corners[2] = new Vertex( new Vector3( right, bottom, height ), new Vector2( picture.Right, picture.Bottom ) ) { MatFlags = colour };
		corners[3] = new Vertex( new Vector3( left, bottom, height ), new Vector2( picture.Left, picture.Bottom ) ) { MatFlags = colour };
	}

	/// <summary>
	/// Every littered cell that has a sprite, with where it stands and the set its kind shows
	/// (<see cref="ParkWorld.LitterSlot"/>), in the map's order.
	/// </summary>
	internal static IEnumerable<(int X, int Y, int Set, Vector2 Anchor)> Littered( ParkState state )
	{
		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = state.CellAt( x, y );

				if ( cell.Litter != 0 && cell.LitterSprite )
					yield return (x, y, ParkWorld.LitterSlot( cell.Litter ), new Vector2( cell.LitterX, cell.LitterY ));
			}
		}
	}
}
