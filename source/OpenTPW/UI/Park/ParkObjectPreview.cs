using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// How the original sizes and places an object window's turning model inside its panel
/// (<c>FUN_004689f0</c>, docs/exe/park-engine.md, "The object window's preview").
/// </summary>
/// <remarks>
/// Everything is in the original's own screen units, in which the screen is two wide and two high: a panel's
/// width is its layout width / 1024 and its height its layout height / 768. The fields are the record the
/// original keeps at <c>0x007afd08</c>, as it leaves them: already multiplied by <see cref="Scale"/>.
/// </remarks>
/// <param name="Scale">Model units to screen units (<c>FUN_0045bf20</c>'s uniform scale).</param>
/// <param name="HalfX">Half the box's width along the item's own x (<c>+0x5c</c> of the record).</param>
/// <param name="HalfZ">Half its depth along the item's own z (<c>+0x60</c>).</param>
/// <param name="Share">The reach over the reach plus half the footprint's diagonal (<c>+0x64</c>).</param>
/// <param name="Diagonal">From the box's low corner at height nought to its high corner (<c>+0x68</c>).</param>
/// <param name="Reach">From the footprint's middle at height nought to the box's high corner (<c>+0x6c</c>).</param>
internal readonly record struct ParkPreviewFit( float Scale, float HalfX, float HalfZ, float Share, float Diagonal, float Reach )
{
	/// <summary>The share of the panel's width half the footprint's diagonal may fill (<c>0x006fe7d4</c>).</summary>
	public const float WidthShare = 2f / 3f;

	/// <summary>How fast the model turns, in radians a millisecond (<c>0x006fe804</c>, taken off the angle).</summary>
	public const float RadiansPerMillisecond = 0.0004f;

	/// <summary>The sine and cosine of the 45 degrees the model is tipped towards the viewer (<c>0x006fe7e4</c>, <c>0x006fe7e8</c>).</summary>
	private static readonly float Tip = MathF.Sin( MathF.PI / 4f );

	/// <summary>
	/// The fit for a box, min and max in the original's axes (y up), in a panel of this width and height.
	/// The scale is the tighter of two: half the footprint's diagonal in two thirds of the width, or the
	/// box's own diagonal in the height.
	/// </summary>
	public static ParkPreviewFit For( System.Numerics.Vector3 min, System.Numerics.Vector3 max, float width, float height )
	{
		var sizeX = max.X - min.X;
		var sizeZ = max.Z - min.Z;

		var halfDiagonal = MathF.Sqrt( (sizeZ * 0.5f * sizeZ * 0.5f) + (sizeX * 0.5f * sizeX * 0.5f) );

		// Both distances are taken from height nought, not from the box's own floor, to its high corner.
		var middle = new System.Numerics.Vector3( (max.X + min.X) * 0.5f, 0f, (max.Z + min.Z) * 0.5f );
		var corner = new System.Numerics.Vector3( min.X, 0f, min.Z );

		var reach = System.Numerics.Vector3.Distance( middle, max );
		var diagonal = System.Numerics.Vector3.Distance( corner, max );

		var scale = width * WidthShare / halfDiagonal;

		if ( height / diagonal < scale )
			scale = height / diagonal;

		var share = reach / diagonal / ((reach / diagonal) + (halfDiagonal / diagonal));

		return new ParkPreviewFit( scale, scale * sizeX * 0.5f, scale * sizeZ * 0.5f, share, scale * diagonal, scale * reach );
	}

	/// <summary>
	/// Where a point of the model lands in the panel, turned by <paramref name="angle"/>: across from the
	/// panel's middle, up from its middle, and its depth, in the original's screen units
	/// (<c>FUN_00468e50</c>). The point is in the original's axes.
	/// </summary>
	/// <remarks>
	/// The model turns about its own origin and is then moved back by half the box, turned the same: the
	/// footprint's middle stays put only for a box whose low corner is the origin, which is every item's
	/// with a model of its own (docs/exe/park-engine.md).
	/// </remarks>
	public System.Numerics.Vector3 Place( System.Numerics.Vector3 point, float angle )
	{
		var (sin, cos) = MathF.SinCos( angle );

		var across = (point.X * cos) + (point.Z * sin);
		var away = (point.Z * cos) - (point.X * sin);

		var x = 0.75f * ((Scale * across) - ((HalfX * cos) + (HalfZ * sin)));

		var y = (Scale * Tip * (point.Y + away))
			- (0.35f * Share * Reach)
			- (0.7f * ((HalfZ * cos) - (HalfX * sin)));

		return new System.Numerics.Vector3( x, y, 0.1f * Scale * Tip * (away - point.Y) );
	}

	/// <summary>
	/// <see cref="Place"/> as one matrix, from OpenTPW's world axes (z up, the item's own z along y) to clip
	/// space: the panel's middle at (<paramref name="middleX"/>, <paramref name="middleY"/>), one of the
	/// original's screen units being <paramref name="unitX"/> across and <paramref name="unitY"/> up.
	/// </summary>
	public System.Numerics.Matrix4x4 Matrix( float angle, float middleX, float middleY, float unitX, float unitY )
	{
		var origin = Place( System.Numerics.Vector3.Zero, angle );
		var x = Place( System.Numerics.Vector3.UnitX, angle ) - origin;
		var up = Place( System.Numerics.Vector3.UnitY, angle ) - origin;
		var z = Place( System.Numerics.Vector3.UnitZ, angle ) - origin;

		// Depth runs 0 to 1, nearest first; the model's own is a tenth of its size about the middle.
		return new System.Numerics.Matrix4x4(
			x.X * unitX, x.Y * unitY, x.Z, 0f,
			z.X * unitX, z.Y * unitY, z.Z, 0f,
			up.X * unitX, up.Y * unitY, up.Z, 0f,
			middleX + (origin.X * unitX), middleY + (origin.Y * unitY), 0.5f + origin.Z, 1f );
	}
}

/// <summary>
/// The model an object window shows turning in its panel: a fresh instance of the item's <c>P</c> model
/// where its archive ships one, of its own model otherwise, playing its first <c>M</c> clip round and
/// round (<c>FUN_00486410</c>, <c>FUN_004689f0</c>; docs/exe/park-engine.md, "The object window's preview").
/// </summary>
/// <remarks>
/// <b>It is never the instance standing in the park</b>, so a closed or broken ride previews running.
/// <para>
/// Three things here are OpenTPW's own, each said where it is: the light, the clock the turn and the clip
/// run on, and the angle the turn starts from.
/// </para>
/// </remarks>
internal sealed class ParkObjectPreview
{
	/// <summary>The role an item's running clips are filed under, <c>M</c>.</summary>
	private const int RunningRole = 5;

	/// <summary>How the preview is lit. <b>Chosen, not measured</b> - the original lights it from its own scene.</summary>
	private static readonly Vector3 Light = new( -400f, -600f, 400f );

	private const float Ambient = 0.55f;

	private readonly LobbyModel _model;
	private readonly RideAnimations _animations;
	private readonly System.Numerics.Vector3 _boxMin, _boxMax;

	private AnimationFile? _posed;
	private double _clock;
	private float? _held;
	private bool _said;

	/// <summary>Which thing this is the preview of.</summary>
	public int ThingId { get; }

	/// <summary>The model file shown.</summary>
	public string ModelPath { get; }

	/// <summary>Whether that is the item's <c>P</c> model.</summary>
	public bool IsPModel { get; }

	/// <summary>Whether the item's own name board was carried onto it.</summary>
	public bool CarriesSign { get; }

	/// <summary>How far round it has turned, in radians.</summary>
	public float Angle { get; private set; }

	private ParkObjectPreview( int thingId, string modelPath, bool isPModel, bool carriesSign, LobbyModel model,
		RideAnimations animations, ItemHeightMapFile heights )
	{
		ThingId = thingId;
		ModelPath = modelPath;
		IsPModel = isPModel;
		CarriesSign = carriesSign;

		_model = model;
		_animations = animations;
		_boxMin = heights.BoxMin.GetSystemVector3();
		_boxMax = heights.BoxMax.GetSystemVector3();

		// The scene must not draw these where they stand, at the world's origin: the window does.
		foreach ( var entity in model.Entities )
			entity.DrawnByOwner = true;

		StartRunning( _animations );

		// The original's first angle is the answer of an x87 intrinsic whose operands are not traced
		// (FUN_0067b24a, 0x00468e2f); this one starts from nought.
		Unimplemented.Report( "OBJECT_PREVIEW_START_ANGLE" );
	}

	/// <summary>
	/// Starts the preview's clip: role 5, entry 0, looped, at speed 1.0, on the first channel
	/// (<c>0x00468e11</c>). An item with no M clip holds its rest pose.
	/// </summary>
	public static void StartRunning( RideAnimations animations )
		=> animations.Trigger( RunningRole, 0, AnimTimeControl.LoopFlag, 1f, 0 );

	/// <summary>
	/// The path of the model the preview of <paramref name="item"/> shows: <c>P&lt;stem&gt;.MD2</c> where the
	/// archive ships one, the item's own otherwise.
	/// </summary>
	/// <remarks>
	/// The original's loader asks for the <c>P</c> model for every item but the fixed ones
	/// (<c>0x00462bd1</c>, behind bit <c>0x20000</c> of its flags), and none of those ships one.
	/// </remarks>
	public static string ModelPathFor( ParkItemCatalogue.Item item, Func<string, bool> exists, out bool isPModel )
	{
		var p = $"{item.Directory}/P{item.Stem}.MD2";

		isPModel = exists( p );

		return isPModel ? p : $"{item.Directory}/{item.Stem}.MD2";
	}

	/// <summary>
	/// Whether a name board painted for the item's own model goes onto its <c>P</c> model: only where
	/// each of the two models names both halves (<c>FUN_00468950</c>, <c>0x00468a64</c>).
	/// </summary>
	public static bool CarriesSignOnto( ModelFile own, ModelFile preview )
		=> CarriesSignOnto( MaterialsOf( own ), MaterialsOf( preview ) );

	/// <summary>The same, by the material names each model's meshes carry.</summary>
	public static bool CarriesSignOnto( IEnumerable<string> own, IEnumerable<string> preview )
		=> Names( own, "sign1" ) && Names( own, "sign2" ) && Names( preview, "sign1" ) && Names( preview, "sign2" );

	private static IEnumerable<string> MaterialsOf( ModelFile file )
		=> file.Meshes.SelectMany( mesh => mesh.Materials ?? [] ).Select( one => one.Name ?? "" );

	private static bool Names( IEnumerable<string> materials, string material )
		=> materials.Any( one => string.Equals( one, material, StringComparison.OrdinalIgnoreCase ) );

	/// <summary>Builds the preview of a thing standing in the park, or says in the log why it cannot.</summary>
	public static ParkObjectPreview? For( int thingId )
	{
		if ( Level.Current is not { ParkState: { } state, Catalogue: { } catalogue }
			|| ParkObjects.Current is not { } objects
			|| !state.TryObject( thingId, out var placed )
			|| !catalogue.TryGet( placed.CatalogueId, out var item ) )
		{
			Log.Info( $"Object preview: thing {thingId} is no catalogue object standing in a park" );
			return null;
		}

		// The box the model is fitted by is the .hmp's (record +0xcc); the original has no other.
		if ( ParkItemHeights.For( item ) is not { } heights )
		{
			Log.Info( $"Object preview: '{item.Name}' has no .hmp to be fitted by" );
			return null;
		}

		try
		{
			var path = ModelPathFor( item, Exists, out var isPModel );
			var stem = isPModel ? $"P{item.Stem}" : item.Stem;

			var sign = objects.SignFor( thingId );

			if ( sign != null && isPModel && objects.ModelFor( thingId ) is { } standing
				&& !CarriesSignOnto( standing.Source, new ModelFile( path ) ) )
				sign = null;

			var animations = RideAnimations.Load( item.Directory, stem, FileSystem );

			var model = new LobbyModel( path, $"{item.Directory}/textures", Vector3.Zero,
				textureOverrides: sign,
				sharedTextureDirectory: $"levels/{objects.ThemeName.ToLowerInvariant()}/sharetex",
				clips: animations.AllClips );

			// A fresh instance of the item's own model is the built thing, with what its building left put away.
			if ( !isPModel )
				objects.PoseAsBuilt( model, item );

			var preview = new ParkObjectPreview( thingId, path, isPModel, sign != null, model, animations, heights );

			Log.Info( $"Object preview: thing {thingId} '{item.Name}' shows {path}" +
				$" ({(isPModel ? "its P model" : "its own model")}), M clips {animations.EntryCount( RunningRole )}," +
				$" name board {(preview.CarriesSign ? "carried" : "none")}," +
				$" box ({heights.BoxMin.X:F2},{heights.BoxMin.Y:F2},{heights.BoxMin.Z:F2})" +
				$"..({heights.BoxMax.X:F2},{heights.BoxMax.Y:F2},{heights.BoxMax.Z:F2})" );

			return preview;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Object preview: '{item.Name}' would not load - {e.Message}" );
			return null;
		}
	}

	private static bool Exists( string path )
	{
		try
		{
			return FileSystem.GetSize( path ) > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	/// <summary>Holds the turn at an angle, or lets it go again - the debug console's, for a repeatable frame.</summary>
	public void Hold( float? angle ) => _held = angle;

	/// <summary>The fit for a panel of this size on the screen.</summary>
	public ParkPreviewFit FitFor( PixelRect panel )
		=> ParkPreviewFit.For( _boxMin, _boxMax,
			panel.Width / (VirtualScreen.Width * 0.5f * VirtualScreen.Scale),
			panel.Height / (VirtualScreen.Height * 0.5f * VirtualScreen.Scale) );

	/// <summary>One line for the debug console: what is shown, how it is fitted and where its turn and clip stand.</summary>
	public string Census( PixelRect panel )
	{
		var fit = FitFor( panel );
		var channel = _animations.Channel( 0 );

		return $"thing {ThingId} {ModelPath} {(IsPModel ? "P model" : "own model")}" +
			$" name board {(CarriesSign ? "carried" : "none")}" +
			$" scale {fit.Scale:F6} half ({fit.HalfX:F5},{fit.HalfZ:F5}) share {fit.Share:F5}" +
			$" diagonal {fit.Diagonal:F5} reach {fit.Reach:F5}" +
			$" angle {Angle:F3}{(_held != null ? " held" : "")}" +
			$" clip {(channel is { IsIdle: false } ? $"{RideAnimations.LetterFor( channel.AnimID )}{channel.SubAnim} frame {channel.AnimFrame:F1}" : "none")}";
	}

	/// <summary>
	/// Turns the model, moves its clip on and draws it inside <paramref name="panel"/>.
	/// </summary>
	/// <remarks>
	/// <b>A declared deviation: the turn and the clip run on the frame clock</b>, so both stand still while
	/// the debug console holds it; the original's read the millisecond clock each frame
	/// (<c>FUN_00468e50</c>).
	/// </remarks>
	public void Draw( PixelRect panel )
	{
		if ( panel.Width <= 0f || panel.Height <= 0f )
			return;

		_clock += Time.Delta * 1000.0;

		Angle = _held ?? Angle + (Time.Delta * 1000f * ParkPreviewFit.RadiansPerMillisecond);

		Pose();

		var fit = FitFor( panel );

		if ( !_said )
		{
			_said = true;

			Log.Info( $"Object preview: {Census( panel )}" );
		}

		var unitX = VirtualScreen.Width * 0.5f * VirtualScreen.Scale * 2f / Screen.Width;
		var unitY = VirtualScreen.Height * 0.5f * VirtualScreen.Scale * 2f / Screen.Height;

		var projection = fit.Matrix( Angle,
			((panel.X + (panel.Width * 0.5f)) * 2f / Screen.Width) - 1f,
			1f - ((panel.Y + (panel.Height * 0.5f)) * 2f / Screen.Height),
			unitX, unitY );

		var command = global::Global.Render.CommandList;

		// The original gives the model a view region over the panel's rectangle (FUN_00486410).
		command.SetScissorRect( 0, (uint)MathF.Max( 0f, panel.X ), (uint)MathF.Max( 0f, panel.Y ),
			(uint)MathF.Max( 0f, panel.Width ), (uint)MathF.Max( 0f, panel.Height ) );

		var view = System.Numerics.Matrix4x4.Identity;

		// Every solid half before any see-through one, the order the scene uses.
		foreach ( var entity in _model.Entities )
		{
			if ( entity.Model is not null && entity.Opacity > 0f )
				entity.DrawOverlay( view, projection, Light, Vector3.One, Ambient, worldNormals: true );
		}

		foreach ( var entity in _model.Entities )
		{
			if ( entity.TranslucentModel is not null && entity.Opacity > 0f )
				entity.DrawOverlay( view, projection, Light, Vector3.One, Ambient, worldNormals: true, translucent: true );
		}

		command.SetFullScissorRects();
	}

	/// <summary>Moves the clip on and shows the frame it has reached, as <see cref="ParkObjects.Sweep"/> does for a standing thing.</summary>
	private void Pose()
	{
		_animations.Advance( (int)_clock );

		if ( _animations.Channel( 0 ) is not { IsIdle: false } channel
			|| _animations.Clip( channel.AnimID, channel.SubAnim ) is not { } clip )
			return;

		if ( !ReferenceEquals( clip, _posed ) )
		{
			if ( _posed is { } previous )
				_model.Rest( previous );

			_posed = clip;
		}

		_model.Pose( clip, channel.AnimFrame );
	}

	/// <summary>Lets go of the instance, as the original does when the window shows another thing or closes (<c>FUN_004690a0</c>).</summary>
	public void Delete()
	{
		foreach ( var entity in _model.Entities )
			entity.Delete();
	}
}
