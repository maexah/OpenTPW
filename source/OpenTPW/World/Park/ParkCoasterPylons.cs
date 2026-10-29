using System.Numerics;
using Numerics = System.Numerics;

namespace OpenTPW;

/// <summary>
/// A placed coaster's pylons drawn, one on the cell under each station node and each piece of its track
/// (<c>docs/exe/park-engine.md</c>, "A coaster's pylons"; Q816, Q817). A pylon's model is made where its cell stands, its root at the
/// midpoint of the cell's corners (<see cref="ParkCoasterHeights.GroundAt"/>), its floor's listed base vertices stood on the corners
/// (<see cref="ProceduralConform.Conform"/>, as <c>Coaster_AddNode</c> conforms the one it makes, <c>0x00434276</c>), and posed
/// as <see cref="ParkCoasterHeights"/> poses its knot: the loft, rotate, incline and bank clips (<see cref="ParkCoasterPylon.Pose"/>).
///
/// <para>
/// <b>A pylon standing alone on its cell is the bottom and the top of its stack</b>, so <c>Coaster_StandCellPylons</c> (<c>0x00432df0</c>)
/// takes the type's named nodes by both: the part flagged <c>0x400</c> hidden (the Minecart's <c>mc_bridge</c>), <c>0x800</c> shown,
/// <c>0x1000</c> hidden and <c>0x2000</c> shown, each only where the pylon's model has the part. Its floor is shown. <b>Not built:</b>
/// a stacked pylon and a loop pylon (each counted, <see cref="ParkCoasterHeights.Pylons"/>), a conform at a move of the node
/// (<c>Coaster_MoveNodeToCell</c>, <c>0x00442f40</c>: a coaster is not edited here), and the saved node word's <c>0x400</c>
/// (<c>0x80000000</c> held on a floor shown again), which changes nothing drawn.
/// </para>
/// </summary>
public sealed class ParkCoasterPylons : Entity
{
	/// <summary>One standing pylon: where it was put, its model, and what stood on its floor.</summary>
	private sealed record Standing( int Thing, ParkCoasterHeights.PylonPlace Place, LobbyModel Model, Numerics.Vector3 Origin,
		int Conformed, int Hidden, ParkCoasterPylon Type );

	/// <summary>
	/// The footprint a pylon is made on: one cell, so the floor search finds the outline of that cell and lists the floor's four
	/// corners (the original's 4 each, 160 over her park's 40: <c>docs/exe/park-engine.md</c>, "A coaster's pylons").
	/// </summary>
	internal static readonly ItemShape Footprint = new( 1, 1, [[1]] );

	/// <summary>The pylons of the park currently loaded, or null outside one.</summary>
	public static ParkCoasterPylons? Current { get; private set; }

	private readonly List<Standing> _standing = [];
	private readonly string _theme;

	/// <summary>How many pylons stand.</summary>
	public int Count => _standing.Count;

	/// <summary>How many floor vertices the conform moved, over all of them.</summary>
	public int Conformed => _standing.Sum( standing => standing.Conformed );

	public ParkCoasterPylons( string themeName, ParkItemCatalogue? catalogue )
	{
		_theme = themeName.ToLowerInvariant();
		Name = $"{themeName} coaster pylons";

		Build( ParkState.Current, catalogue, ParkGround.Current?.Heightfield );

		Current = this;
	}

	private void Build( ParkState? state, ParkItemCatalogue? catalogue, HeightfieldFile? field )
	{
		var save = state?.Park?.Save;

		if ( state == null || catalogue == null || save == null || field == null )
			return;

		foreach ( var placed in state.Objects )
		{
			if ( !catalogue.TryGet( placed.CatalogueId, out var item ) || item.TrackType != ItemDescriptionFile.CoasterTrack
				|| placed.MeshInstance == 0 )
				continue;

			if ( ParkCoasterHeights.SetUp( save, placed, item, field, FileSystem ) is not { } coaster )
				continue;

			var places = ParkCoasterHeights.Pylons( save.Coasters.FirstPieces, coaster.Pylon, field, coaster.RootHeight, coaster.TrackNode );

			if ( places == null )
				continue;

			var before = _standing.Count;

			foreach ( var place in places )
			{
				if ( Make( placed.ThingId, place, item.Directory, coaster.Pylon, field ) is { } standing )
					_standing.Add( standing );
			}

			Log.Info( $"{_theme}: coaster '{item.Name}' (thing {placed.ThingId}): {_standing.Count - before} of {places.Count} pylons stand, "
				+ $"{_standing.Skip( before ).Sum( standing => standing.Conformed )} floor vertices conformed, "
				+ $"{_standing.Skip( before ).Sum( standing => standing.Hidden )} parts hidden" );
		}
	}

	/// <summary>
	/// The type's named parts a pylon alone on its cell hides or shows, by node (<c>Coaster_StandCellPylons</c>, <c>0x00432df0</c>: top of
	/// the stack, <c>0x400</c> hides its part and <c>0x800</c> shows its; bottom, <c>0x1000</c> hides and <c>0x2000</c> shows), only for the
	/// parts the pylon's model has.
	/// </summary>
	internal static IEnumerable<(int Node, bool Visible)> PartsOf( ParkCoasterPylon type )
	{
		foreach ( var (which, visible) in new[] { (1, false), (2, true), (3, false), (4, true) } )
		{
			if ( type.NamedNode( which ) is var node and >= 0 )
				yield return (node, visible);
		}
	}

	/// <summary>Makes one pylon where its cell stands, or null where its model would not load.</summary>
	private Standing? Make( int thing, ParkCoasterHeights.PylonPlace place, string directory, ParkCoasterPylon type, HeightfieldFile field )
	{
		var origin = new Numerics.Vector3( place.CellX * field.CellSizeX, place.CellY * field.CellSizeY,
			ParkCoasterHeights.GroundAt( field, place.CellX, place.CellY ) );
		var conformed = 0;

		try
		{
			var model = new LobbyModel( $"{directory}/{type.Stem}.MD2", $"{directory}/textures", Vector3.Zero,
				sharedTextureDirectory: $"levels/{_theme}/sharetex", clips: type.Clips,
				stand: file => conformed = ProceduralConform.Conform( file, Footprint, origin, Quaternion.Identity, field ) );

			model.SetTransform( origin, Quaternion.Identity );
			model.PoseNodes( poser => type.Pose( poser, place.Loft, place.Yaw, place.Incline, place.Bank ) );
			type.Morph( model, place.Loft, place.Incline, place.Bank );

			// Alone on its cell it is the bottom and the top of the stack (0x00432df0).
			var hidden = 0;

			foreach ( var (node, visible) in PartsOf( type ) )
			{
				model.SetMeshVisible( node, visible );
				hidden += visible ? 0 : 1;
			}

			return new Standing( thing, place, model, origin, conformed, hidden, type );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_theme}: a coaster pylon ({place.Which}) would not load, so ({place.CellX},{place.CellY}) is missing - {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// The console's <c>pylons</c>: each pylon's node, cell, loft and clip settings, where its knot is drawn against where
	/// <see cref="ParkCoasterHeights.OnPylon"/> puts it, and its floor's listed base vertices and how many stand on a corner.
	/// </summary>
	public IEnumerable<string> Census()
	{
		var field = ParkGround.Current?.Heightfield;

		foreach ( var standing in _standing )
		{
			var place = standing.Place;
			var drawn = standing.Model.Posed( standing.Type.KnotNode ) is { } world
				? new Numerics.Vector3( standing.Origin.X + world.Translation.X, standing.Origin.Z + world.Translation.Y, standing.Origin.Y + world.Translation.Z )
				: (Numerics.Vector3?)null;
			var asked = field == null ? (Numerics.Vector3?)null
				: ParkCoasterHeights.OnPylon( place.CellX, place.CellY, place.Loft, standing.Type, field );
			var floor = ProceduralConform.Measure( standing.Model.Source, Footprint, standing.Origin, Quaternion.Identity, field );
			var shown = Enumerable.Range( 0, standing.Model.Entities.Length )
				.Where( mesh => standing.Model.Entities[mesh].Opacity == 0f ).Select( mesh => standing.Type.NodeName( mesh ) );

			yield return $"thing {standing.Thing} {place.Which} ({place.CellX},{place.CellY}) loft {place.Loft:F3} yaw {place.Yaw:F3} incline {place.Incline:F3} bank {place.Bank:F3} "
				+ $"knot drawn {Format( drawn )} asked {Format( asked )} off {(drawn is { } d && asked is { } a ? Numerics.Vector3.Distance( d, a ) : float.NaN):F4} "
				+ $"floor listed {floor.Listed} on a corner {floor.OnCorner} largest off {floor.LargestOff:F4} moved {standing.Conformed} hidden [{string.Join( ",", shown )}]";
		}
	}

	private static string Format( Numerics.Vector3? v ) => v is { } p ? $"({p.X:F2},{p.Y:F2},{p.Z:F2})" : "-";

	private void TakeDown()
	{
		foreach ( var standing in _standing )
		{
			foreach ( var entity in standing.Model.Entities )
				entity.Delete();
		}

		_standing.Clear();
	}

	protected override void OnDelete()
	{
		base.OnDelete();

		TakeDown();

		if ( Current == this )
			Current = null;
	}
}
