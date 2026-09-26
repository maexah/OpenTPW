using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The queues a park's guests line up in, one model to a cell, read from the park's own save.
///
/// <para>
/// <b>A queue is not a path, and that is the whole reason this is a separate thing.</b> They sit side by
/// side in the same field - a cell's tile set is 1 for a path and 2 for a queue - which invites drawing
/// them the same way, as art laid on the ground. They are not: a queue is built from small models with
/// railings and torches, each covering one cell and carrying its own flat base, and the theme ships them
/// in a <c>queue.wad</c> of their own beside its terrain.
/// </para>
///
/// <para>
/// <b>A queue cell's tile index names one of those models.</b> The four cells of Lost Kingdom's queue
/// carry indices 5, 2, 2 and 3, and the theme's <c>.tct</c> gives
/// <c>QueueTex</c> only rows 0 to 3, so the index plainly addressed something else. It addresses
/// <see cref="Pieces"/>, a fixed table the executable keeps at 0x76338c and walks twelve bytes at a time
/// as it loads them (FUN_00522900), indexing it by exactly this number when it places one
/// (FUN_005229e0). <c>QueueTex</c> is real but is the art those models are skinned with.
/// </para>
///
/// <para>
/// The four cells read <b>end, straight, straight, bend</b>, which is the shape their own stored
/// neighbour masks make: the cell indexed 5 is where the queue meets the path, the two indexed 2 have
/// mask <c>0x44</c> - east and west, collinear - and the one indexed 3 has mask <c>0x50</c>, south and
/// west, a corner. Four out of four against a table read from the program rather than guessed at.
/// </para>
///
/// <para>
/// <b>A queue piece does not turn the way a built thing turns</b> - it takes 360 minus its saved angle.
/// See <see cref="TurnOf"/>, which carries the evidence, because this is the one place in a park where
/// the art is asymmetric enough to prove a rotation at all.
/// </para>
///
/// <para>
/// Like the paths, these draw <i>instead of</i> the ground rather than on top of it: every queue cell
/// carries a real ground texture index in <c>base.MD2</c>, so <see cref="ParkGround"/> leaves them to
/// this - see <see cref="IsQueue"/>.
/// </para>
/// </summary>
public sealed class ParkQueues : Entity
{
	/// <summary>The theme folder these were loaded from - "jungle", "fantasy", "hallow" or "space".</summary>
	public string ThemeName { get; }

	private readonly List<LobbyModel> _models = [];

	/// <summary>How many queue cells actually stand in the park - for the log, and for anything wanting to check.</summary>
	public int Placed => _models.Count;

	/// <summary>
	/// The tile set a queue cell carries, against 1 for a path and 0 for a cell that draws no tile at
	/// all. Every one of Lost Kingdom's four queue cells reads 2 and nothing else does.
	/// </summary>
	public const int QueueTileSet = 2;

	/// <summary>
	/// Whether a cell is one this draws, which is the same question <see cref="ParkGround"/> asks in
	/// order to leave it alone - the arrangement <see cref="ParkPaths.IsPath"/> already has, and kept
	/// here for the same reason: a cell belongs to whatever draws it, and the two must not be able to
	/// disagree about which.
	/// </summary>
	public static bool IsQueue( ParkWorld.MapCell cell ) => cell.TileSet == QueueTileSet;

	/// <summary>
	/// The models a queue is built from, in the order the executable's own table lists them, which is the
	/// order a cell's tile index counts through. <c>quedead</c> appearing twice is the table's own doing,
	/// not a mistake here - entries 0 and 1 both point at it, and they are also the only two carrying a
	/// set flag byte, so the pair is deliberate.
	///
	/// <para>
	/// Every theme ships all seven under the same names, so this is the game's table rather than the
	/// jungle's.
	/// </para>
	/// </summary>
	private static readonly string[] Pieces =
		["quedead", "quedead", "questra", "quebnd2", "quebnd1", "queend", "quebin1", "quebin2"];

	/// <summary>
	/// How many pieces the game's own table holds, which is what a queue cell's tile index has to
	/// address. <see cref="ParkPathBuilding"/> asks so that an index landing outside it is counted where
	/// it is computed rather than only where it fails to draw.
	/// </summary>
	internal static int PieceCount => Pieces.Length;

	/// <summary>
	/// How far round a piece of queue stands, which is <b>not</b> how far round a built thing stands: the
	/// executable turns a queue piece by <c>360 - angle</c>, folding 360 back to 0 (FUN_005229e0), where an
	/// item takes its saved angle as it is (<see cref="ParkObjects.Turn"/>).
	///
	/// <para>
	/// <b>The art proves this and nothing else could.</b> Every piece is one cell square, so the footprint
	/// cells that settle an item's turn say nothing at all here - a square covers the same square whichever
	/// way it is facing. What settles it is the one asymmetric detail in the set: <c>queend</c> carries two
	/// torches along a single edge of its plate, and that edge has to be the one the queue is entered from.
	/// Lost Kingdom's end piece is at (49,22), the path it serves is at (48,22) due west, and its saved
	/// angle is 270 - and only this reading puts the torches on the western edge. Taking the angle the way
	/// an item takes it puts them on the eastern one, standing them in the middle of the run.
	/// </para>
	/// <para>
	/// A wrong turn shows by looking: the torches in the middle of the queue and the bend turning the
	/// wrong way off the ride, which are the same 180 degrees seen on the only two pieces whose art is not
	/// symmetric. A straight looks identical either way.
	/// </para>
	/// </summary>
	private static int TurnOf( ParkWorld.MapCell cell ) => (360 - cell.TileAngle) % 360;

	/// <summary>
	/// The queues of the park currently loaded, or null outside one - the arrangement
	/// <see cref="ParkPaths.Current"/> and <see cref="ParkGround.Current"/> already have.
	///
	/// <para>
	/// <b>It is how a changed cell reaches the queues:</b> <see cref="ParkSurfaces.Rebuild"/> asks them to
	/// lay themselves again through it, and the models are otherwise reachable only through
	/// <c>Entity.All</c>.
	/// </para>
	/// </summary>
	public static ParkQueues? Current { get; private set; }

	/// <summary>The park this was built from, so that it can be built again when a cell changes.</summary>
	private readonly ParkWorld? _world;

	/// <param name="world">
	/// The park's own save, already walked, or null where the theme ships none. It is read once by
	/// <see cref="Level"/> and shared with the ground, the paths and the objects.
	/// </param>
	public ParkQueues( string themeName, ParkWorld? world )
	{
		ThemeName = themeName;
		_world = world;
		Name = $"{themeName} queues";

		Build( world );

		Current = this;
	}

	/// <summary>
	/// Stands the queue pieces again, because a cell has changed - a queue built, a queue deleted.
	///
	/// <para>
	/// <b>A queue is not one model, so this is not the ground's rebuild.</b> The ground and the paths
	/// are each a single mesh that is dropped and rebuilt; a queue is one <see cref="LobbyModel"/> a
	/// cell, and <see cref="LobbyModel"/> has no teardown of its own - so each one's entities are
	/// deleted individually, which is the shape <see cref="ParkObjects.Remove"/> already uses for a
	/// placed thing. Skipping that leaves every old piece standing in the park beside its replacement.
	/// </para>
	/// </summary>
	public void Rebuild()
	{
		TakeDown();

		Build( _world );
	}

	/// <summary>
	/// Lets go of <see cref="Current"/>, but only if it is still this one - the guard the ground and the
	/// paths both use, for a scene that builds its replacement before tearing down its predecessor.
	/// </summary>
	protected override void OnDelete()
	{
		base.OnDelete();

		TakeDown();

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// Takes every piece of queue out of the park.
	///
	/// <para>
	/// <b>One statement of it, shared by <see cref="Rebuild"/> and <see cref="OnDelete"/>.</b> Written
	/// twice these would be free to drift, and only the rebuild path is ever exercised by a test - the
	/// same argument that has the debug console's <c>drop</c> reach <c>ParkHand.LetGo</c> rather than a
	/// copy of it.
	/// </para>
	/// <para>
	/// Deleting the same entity twice is harmless - <see cref="Entity.Delete"/> returns at once when it
	/// has already run - which matters because <see cref="Level.Unload"/> deletes every entity in the
	/// scene, these among them, and then this runs as well.
	/// </para>
	/// </summary>
	private void TakeDown()
	{
		foreach ( var model in _models )
		{
			foreach ( var entity in model.Entities )
				entity.Delete();
		}

		_models.Clear();
	}

	private void Build( ParkWorld? world )
	{
		if ( world == null || world.Cells.Count == 0 )
		{
			Log.Info( $"{ThemeName}: no saved park, so it has no queues of its own" );
			return;
		}

		// The ground read the heightfield already, and a queue stands on exactly the same grid at exactly
		// the same heights, so asking it is both cheaper and safer than reading base.MD2 again.
		var field = ParkGround.Current?.Heightfield;

		if ( field == null )
		{
			Log.Warning( $"{ThemeName}: the ground is not built, so its queues have nothing to stand on" );
			return;
		}

		var unnamed = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				// The running park's answer, not the file's - see ParkPaths.Build, which reads the same
				// overlay for the same reason.
				var cell = ParkState.CellFor( world, x, y );

				if ( !IsQueue( cell ) )
					continue;

				if ( cell.TileIndex < 0 || cell.TileIndex >= Pieces.Length )
				{
					++unnamed;
					continue;
				}

				Place( x, y, cell );
			}
		}

		// A park of a shape this has not seen would show up here rather than by drawing the wrong piece.
		if ( unnamed > 0 )
			Log.Warning( $"{ThemeName}: {unnamed} queue cells name a piece outside the game's own table of {Pieces.Length}" );

		if ( Placed > 0 )
			Log.Info( $"{ThemeName}: {Placed} queue cells stand in the park" );
	}

	/// <summary>
	/// Stands one piece of queue on one cell, or says why it could not.
	///
	/// <para>
	/// A piece is one cell square and authored with that cell's corner at its own origin - its base plate
	/// runs 0 to 10 in both ground axes, the same way an item's floor does - so it is stood by exactly the
	/// rule <see cref="ParkObjects.OriginFor"/> works out, turned by the same negated angle. Sharing those
	/// rather than repeating them is deliberate: the direction of the turn is the part that is easy to get
	/// backwards, and there should only ever be one statement of it.
	/// </para>
	/// </summary>
	private void Place( int x, int y, ParkWorld.MapCell cell )
	{
		var theme = ThemeName.ToLowerInvariant();
		var directory = $"levels/{theme}/queue";
		var piece = Pieces[cell.TileIndex];

		try
		{
			// Queue pieces keep their own art beside them - the jungle's six jpa_que textures - and fall
			// back to the theme's shared archive for anything else, the way a built item does.
			var model = new LobbyModel( $"{directory}/{piece}.MD2", $"{directory}/textures", Vector3.Zero,
				sharedTextureDirectory: $"levels/{theme}/sharetex" );

			// The same angle to both, or the model is turned about one point and stood at another. For a
			// one-cell piece either angle covers the same cell, so a mismatch does not show as a piece in
			// the wrong place - it shows as one sitting slightly off its own square.
			var turn = TurnOf( cell );

			model.SetTransform( ParkObjects.OriginFor( x, y, turn ), ParkObjects.Turn( turn ) );

			_models.Add( model );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{ThemeName}: queue piece '{piece}' would not load, so ({x},{y}) is missing from the park - {e.Message}" );
		}
	}

	protected override void OnUpdate()
	{
		// Game time, not frame time - see GameClock.
		foreach ( var model in _models )
			model.Update( GameClock.Delta );
	}
}
