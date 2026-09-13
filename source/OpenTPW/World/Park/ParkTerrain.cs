using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A park's fixed scenery - the one big model every theme ships as <c>base.MD2</c> inside its own
/// <c>terrain.wad</c>. For the jungle that is 272 meshes and 20,405 triangles: the entrance road and
/// its verges, the arrival and departure bus shelters, the ticket booths, the hoardings that fence off
/// land the player has not bought, the palms, the river, the cliffs and the volcano.
///
/// <para>
/// It is <b>not</b> the ground. The playable surface is a 96x85 cell heightfield stored in a block
/// inside the same file, which is a separate job - see ParkHeightfield when it exists. This entity is
/// only the scenery standing on that ground, and it is first because it needs no new file format at
/// all: <see cref="ModelFile"/> reads it unmodified (it carries the same 0x1CD15D46 magic as every
/// other model in the game), and all 57 of the material names it asks for resolve as
/// <c>levels/&lt;theme&gt;/terrain/textures/&lt;name&gt;.wct</c>.
/// </para>
///
/// <para>
/// The model already carries world positions, so it is loaded at the origin rather than placed:
/// its vertices span roughly -555..1511 across and -621..1445 deep, which is the whole scenic island,
/// while the park the player can build on is only the 950x840 units nearest the origin. That is why
/// the mesh's extent must never be mistaken for the playable extent - dividing one by the other is
/// what produced a wrong cell size early on. The cell is 10 units, and the centre of grid cell
/// <c>(gx, gy)</c> is at <c>(gx * 10 + 5, gy * 10 + 5)</c>.
/// </para>
///
/// <para>
/// <b>Not animated yet.</b> Each theme ships a companion animation file beside this one -
/// <c>basem.MD2</c> for the jungle, which reports <c>IsAnimation</c> and carries the same bounding box -
/// but <see cref="LobbyModel"/> looks for companions with an <c>M1</c>, <c>M2</c>... suffix and this one
/// has a bare <c>m</c>, so nothing picks it up. Whatever it drives (the river and the falls are the
/// likely candidates) stands still for now.
/// </para>
/// </summary>
public sealed class ParkTerrain : Entity
{
	/// <summary>The theme folder this was loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	private readonly LobbyModel _model;

	public ParkTerrain( string themeName )
	{
		ThemeName = themeName;
		Name = $"{themeName} terrain scenery";

		// terrain.wad stands in for a directory of its own name, so everything inside it is reached
		// as levels/<theme>/terrain/... - the same way lobby.wad gives the lobby its lobby/ paths.
		var terrain = $"levels/{themeName.ToLowerInvariant()}/terrain";

		_model = new LobbyModel( $"{terrain}/base.MD2", $"{terrain}/textures", Vector3.Zero );
	}

	protected override void OnUpdate() => _model.Update( Time.Delta );
}
