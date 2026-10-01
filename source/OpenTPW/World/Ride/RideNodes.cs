using Numerics = System.Numerics;

namespace OpenTPW;

/// <summary>
/// The nodes a ride script finds by id on its thing's own model, and where the engine keeps each one in the world -
/// the far side of <c>FUN_00556b90</c>, which the walk family asks for both ends of every leg.
///
/// <para>
/// <b>A position is the node's STORED matrix, not the model as drawn.</b> The engine reads the translation row of a
/// matrix its pose walk keeps per lookup record. This keeps the pose the build stores: each node composed from the
/// root down, the root's own file rows turned by the placement and its translation replaced by where the thing
/// stands, every element kept as a float as the engine's are, so a distance that sits on a whole unit rounds the way
/// the engine's does (docs/exe/ride-operation.md, "How long a leg lasts, and where its ends are").
/// </para>
/// <para>
/// <b>It deviates for a node that rides a clip-driven ancestor</b>: the engine stores such a node where the last
/// drawn frame left it, and nothing here poses one, so it stands at rest (<see cref="NodeEnd.RestPose"/>). Every
/// shipped walk comes at a pose that gives the rest-pose legs - the Aztec Mayhem's measured, the Lookout's and the
/// Totem's worked out - but a ride left off screen mid-cycle keeps the engine's snapshot and not this. A head on a
/// face of a morphing mesh is the other deviation (<see cref="NodeEnd.OnAFace"/>).
/// </para>
/// <para>
/// Everything is in the model's own axes, <b>y up</b>, which are the engine's world axes too; the park's are z up.
/// </para>
/// </summary>
public sealed class RideNodes
{
	/// <summary>The space a walk node is found in - <c>WALKON</c>'s walk node and every off-to node.</summary>
	public const uint WalkSpace = 0x800;

	/// <summary>The space a head is found in, where the walk's action is 4.</summary>
	public const uint HeadSpace = 0x80;

	/// <summary>A record whose flags carry either bit has a matrix at all (<c>0x0044a904</c>).</summary>
	private const uint HasMatrix = 0x30;

	/// <summary>The flags the item loader turns into runtime bit 8, which makes the pose walk store a childless node.</summary>
	private const uint StoredWhenChildless = 0x580f00;

	/// <summary>The flags that take a record's position from a face of its parent mesh while a morph plays it (<c>0x0044abf2</c>).</summary>
	private const uint FromAFace = 0x40040;

	/// <summary>How many entries the engine's sine table has (<c>FUN_004708d0</c>).</summary>
	private const int TableSize = 0x1000;

	/// <summary>The table's scale, entries per radian: 4096 × 0.15915494f, exact in a float.</summary>
	private const float TableScale = TableSize * 0.15915494f;

	private readonly ModelFile _model;
	private readonly ModelFile.Node[] _nodes;
	private readonly bool[] _hasChild;
	private readonly bool[] _moved;
	private readonly bool[] _morphed;
	private readonly bool _doHeadProcessing;

	private Numerics.Vector3 _origin;
	private float _cos = 1f;
	private float _sin;

	/// <summary>The model's own name, for the log.</summary>
	public string Stem { get; }

	/// <param name="model">The thing's own model.</param>
	/// <param name="stem">Its name.</param>
	/// <param name="doHeadProcessing">The item's <see cref="ItemDescriptionFile.DoHeadProcessing"/>.</param>
	/// <param name="clips">Every clip the thing carries, so the nodes they turn, move or morph are known.</param>
	public RideNodes( ModelFile model, string stem, bool doHeadProcessing, IEnumerable<AnimationFile> clips )
	{
		Stem = stem;
		_model = model;
		_nodes = [.. model.Nodes];
		_doHeadProcessing = doHeadProcessing;
		_hasChild = new bool[_nodes.Length];
		_moved = new bool[_nodes.Length];
		_morphed = new bool[_nodes.Length];

		foreach ( var node in _nodes )
		{
			if ( node.ParentIndex >= 0 && node.ParentIndex < _nodes.Length )
				_hasChild[node.ParentIndex] = true;
		}

		// A clip's rotation, position and path tracks are what move a node. Its scale channel (0x80) would too, and
		// AnimationFile does not read it; every node a shipped walk finds that hangs from a scaled node, a clip also
		// turns or moves that node. The morph tracks it reads leave out the ones it skips as unreadable.
		foreach ( var clip in clips )
		{
			foreach ( var track in clip.RotationTracks )
				Mark( _moved, track.TargetIndex );

			foreach ( var track in clip.PositionTracks )
				Mark( _moved, track.TargetIndex );

			foreach ( var track in clip.PathTracks )
				Mark( _moved, track.TargetIndex );

			foreach ( var track in clip.MorphTracks )
				Mark( _morphed, track.TargetIndex );
		}

		static void Mark( bool[] marks, int node )
		{
			if ( node >= 0 && node < marks.Length )
				marks[node] = true;
		}
	}

	/// <summary>
	/// Reads a thing's own model, <c>&lt;stem&gt;.md2</c> beside its script, or answers null where there is none. A
	/// model that will not read throws, for the caller to say why.
	/// </summary>
	public static RideNodes? Load( string directory, string stem, BaseFileSystem files, bool doHeadProcessing,
		IEnumerable<AnimationFile> clips )
	{
		using var stream = files.OpenRead( $"{directory}/{stem}.MD2" );

		return stream == null ? null : new RideNodes( new ModelFile( stream ), stem, doHeadProcessing, clips );
	}

	/// <summary>
	/// Stands the model where its thing stands: <paramref name="origin"/> in the park's axes (z up), as
	/// <see cref="ParkObjects.OriginFor"/> gives it, turned <paramref name="degrees"/> as the save does - one turn from
	/// the file's own rows.
	/// </summary>
	public void Place( Vector3 origin, int degrees )
	{
		_origin = new Numerics.Vector3( origin.X, origin.Z, origin.Y );

		// The two table indices as FUN_00467030 forms them (0x0046717c): the turn in radians as a float, times the
		// table's scale as a float, rounded to the nearest; the cosine's from the turn plus a quarter, rounded apart.
		var radians = (float)(degrees * (double)0.017453292f);
		var sine = (float)((double)TableScale * radians);
		var cosine = (float)(((double)radians + 1.5707963705062866) * TableScale);

		_sin = Table( (int)Math.Round( sine, MidpointRounding.ToEven ) );
		_cos = Table( (int)Math.Round( cosine, MidpointRounding.ToEven ) );
	}

	/// <summary>
	/// One entry of the engine's sine table (<c>FUN_004708d0</c>): <c>sin( i / 4096 × 6.2831855f )</c>, stored as a float,
	/// the index wrapping as the engine masks it.
	/// </summary>
	public static float Table( int index )
		=> (float)Math.Sin( (index & (TableSize - 1)) / (double)TableSize * (double)6.2831855f );

	/// <summary>
	/// Where the node <paramref name="id"/> in <paramref name="space"/> stands, in the model's own axes, and how that
	/// was found. <see cref="NodeEnd.Posed"/>, <see cref="NodeEnd.RestPose"/> and <see cref="NodeEnd.OnAFace"/> give a
	/// position.
	/// </summary>
	public NodeEnd Find( int id, uint space, out Numerics.Vector3 position )
	{
		position = default;

		// A negative id takes the centre of the thing's cells instead (FUN_00466b70); no shipped walk names one.
		if ( id < 0 )
			return NodeEnd.NegativeId;

		var node = _model.FindNode( id, space );

		if ( node < 0 )
			return NodeEnd.Missing;

		var flags = _nodes[node].IdFlags;

		// No matrix, or one the pose walk never stores: the engine reads (0, 0, 0) for either while nothing is attached to
		// the record. Every head a shipped walk finds belongs to an item that sets DoHeadProcessing.
		if ( (flags & HasMatrix) == 0
			|| (!_hasChild[node] && (flags & StoredWhenChildless) == 0 && !_doHeadProcessing) )
			return NodeEnd.Unposed;

		position = Posed( node );

		var parent = _nodes[node].ParentIndex;

		if ( (flags & FromAFace) != 0 && parent >= 0 && parent < _nodes.Length && _morphed[parent] )
			return NodeEnd.OnAFace;

		return RidesAClip( node ) ? NodeEnd.RestPose : NodeEnd.Posed;
	}

	/// <summary>
	/// How many head slots the script loader gives a script on this model, <c>+0x4c</c>: the run of ids from 1 that the
	/// head space finds, stopping at the first it does not (<c>FUN_005587f0</c>, <c>0x00558d8a</c>..<c>0x00558db7</c>).
	/// </summary>
	public int HeadCount()
	{
		var count = 0;

		while ( _model.FindNode( count + 1, HeadSpace ) >= 0 )
			++count;

		return count;
	}

	/// <summary>
	/// Where a head <c>ADDHEAD</c> hung on head node <paramref name="id"/> stands, as <see cref="Find"/> answers, and
	/// the node's whole stored matrix, in the model's own axes (y up). A node with a head on it is stored whatever its
	/// children (<c>docs/exe/ride-operation.md</c>, "How long a leg lasts": a record with something attached), so only a
	/// node with no matrix at all is <see cref="NodeEnd.Unposed"/>.
	/// </summary>
	public NodeEnd FindHead( int id, out Numerics.Matrix4x4 world )
	{
		world = Numerics.Matrix4x4.Identity;

		var node = _model.FindNode( id, HeadSpace );

		if ( node < 0 )
			return NodeEnd.Missing;

		var flags = _nodes[node].IdFlags;

		if ( (flags & HasMatrix) == 0 )
			return NodeEnd.Unposed;

		world = Stored( node );

		var parent = _nodes[node].ParentIndex;

		if ( (flags & FromAFace) != 0 && parent >= 0 && parent < _nodes.Length && _morphed[parent] )
			return NodeEnd.OnAFace;

		return RidesAClip( node ) ? NodeEnd.RestPose : NodeEnd.Posed;
	}

	/// <summary>The index of head node <paramref name="id"/> in the model's nodes, or -1 - the drawn model's index too.</summary>
	public int HeadIndex( int id ) => _model.FindNode( id, HeadSpace );

	/// <summary>The name of head node <paramref name="id"/>, or null where the model has none - for the census.</summary>
	public string? HeadName( int id ) => _model.FindNode( id, HeadSpace ) is var node && node >= 0 ? _nodes[node].Name : null;

	/// <summary>Whether the node, or anything it hangs from, is turned or moved by one of the thing's clips.</summary>
	private bool RidesAClip( int node )
	{
		for ( var guard = _nodes.Length; node >= 0 && node < _nodes.Length && guard > 0; --guard )
		{
			if ( _moved[node] )
				return true;

			node = _nodes[node].ParentIndex;
		}

		return false;
	}

	/// <summary>The node's translation as the pose walk stores it.</summary>
	private Numerics.Vector3 Posed( int node )
	{
		var world = Stored( node );

		return new Numerics.Vector3( world.M41, world.M42, world.M43 );
	}

	/// <summary>The node's matrix as the pose walk stores it: root first, each matrix local times parent.</summary>
	private Numerics.Matrix4x4 Stored( int node )
	{
		var chain = new List<int>();

		for ( var at = node; at >= 0 && at < _nodes.Length && chain.Count <= _nodes.Length; at = _nodes[at].ParentIndex )
			chain.Add( at );

		chain.Reverse();

		var world = Root( _nodes[chain[0]].LocalTransform );

		for ( var i = 1; i < chain.Count; ++i )
			world = Compose( _nodes[chain[i]].LocalTransform, world );

		return world;
	}

	/// <summary>
	/// The root as the placement leaves it: its file rows turned about y, x' = x·c + z·s and z' = z·c − x·s
	/// (<c>FUN_0046f650</c>), and its translation where the thing stands.
	/// </summary>
	private Numerics.Matrix4x4 Root( Numerics.Matrix4x4 file )
	{
		(float X, float Z) Turn( float x, float z )
			=> ((float)((double)x * _cos + (double)z * _sin), (float)((double)z * _cos - (double)x * _sin));

		var (x1, z1) = Turn( file.M11, file.M13 );
		var (x2, z2) = Turn( file.M21, file.M23 );
		var (x3, z3) = Turn( file.M31, file.M33 );

		return new Numerics.Matrix4x4(
			x1, file.M12, z1, 0f,
			x2, file.M22, z2, 0f,
			x3, file.M32, z3, 0f,
			_origin.X, _origin.Y, _origin.Z, 1f );
	}

	/// <summary>
	/// <paramref name="local"/> × <paramref name="parent"/> as an affine product in the engine's order (<c>0x004702ea</c>):
	/// each element the third term, then the second, then the first, and the parent's translation last, worked in doubles
	/// and stored as a float. The build's FPU precision there is not measured.
	/// </summary>
	private static Numerics.Matrix4x4 Compose( Numerics.Matrix4x4 local, Numerics.Matrix4x4 parent )
	{
		float At( int row, int column )
		{
			var sum = ((double)local[row, 2] * parent[2, column] + (double)local[row, 1] * parent[1, column])
				+ (double)local[row, 0] * parent[0, column];

			return (float)(row == 3 ? sum + parent[3, column] : sum);
		}

		return new Numerics.Matrix4x4(
			At( 0, 0 ), At( 0, 1 ), At( 0, 2 ), 0f,
			At( 1, 0 ), At( 1, 1 ), At( 1, 2 ), 0f,
			At( 2, 0 ), At( 2, 1 ), At( 2, 2 ), 0f,
			At( 3, 0 ), At( 3, 1 ), At( 3, 2 ), 1f );
	}
}

/// <summary>What <see cref="RideNodes.Find"/> found.</summary>
public enum NodeEnd
{
	/// <summary>Found, and where the engine stores it.</summary>
	Posed,

	/// <summary>Found, but it rides a clip-driven ancestor, so this is its rest pose - see <see cref="RideNodes"/>.</summary>
	RestPose,

	/// <summary>
	/// Found at rest, but the engine takes it from a face of its parent mesh once a morph has played that mesh; no Lost
	/// Kingdom walk item carries one.
	/// </summary>
	OnAFace,

	/// <summary>No record has that id in that space: the engine carries on with whatever position it held.</summary>
	Missing,

	/// <summary>Found with no stored matrix: the engine reads (0, 0, 0) while nothing is attached to it.</summary>
	Unposed,

	/// <summary>A negative id, which the engine answers from the centre of the thing's cells.</summary>
	NegativeId
}
