using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NeoVeldrid;

namespace OpenTPW;

public class Entity
{
	public Level Level { get; set; }
	/// <summary>
	/// Every entity alive, which each one joins in its own constructor.
	/// </summary>
	public static List<Entity> All { get; set; } = new();

	/// <summary>
	/// Forward, Left, Up (FLU)
	/// </summary>
	public Vector3 Position;

	/// <summary>
	/// Orientation about the entity's own origin, as a quaternion.
	/// </summary>
	public Quaternion Rotation;

	public Vector3 Scale = Vector3.One;

	public string Name { get; set; }

	/// <summary>
	/// Replaces <see cref="Scale"/> when a transform can't be expressed as scale and rotation.
	/// A little over 1% of .md2 nodes are sheared - their axes aren't perpendicular - and
	/// decomposing those to a TRS loses the shear, which visibly skews the mesh.
	/// <see cref="Rotation"/> still applies on top, so an animation can turn the mesh about its
	/// own origin either way.
	/// </summary>
	public Matrix4x4? LinearTransform;

	public Matrix4x4 ModelMatrix
	{
		get
		{
			var matrix = LinearTransform ?? Matrix4x4.CreateScale( Scale.GetSystemVector3() );
			matrix *= Matrix4x4.CreateFromQuaternion( Rotation );
			matrix *= Matrix4x4.CreateTranslation( Position.GetSystemVector3() );

			return matrix;
		}
	}

	public Entity()
	{
		Level = Level.Current;
		All.Add( this );
		Name = $"{this.GetType().Name} {All.Count}";
	}

	public void Render()
	{
		OnRender();
	}

	/// <summary>
	/// Second render pass, after every entity has drawn its solid geometry - see
	/// <see cref="Level.Render"/>. A model's see-through half draws here, and so do the park's people.
	/// </summary>
	public void RenderTranslucent()
	{
		OnRenderTranslucent();
	}

	/// <summary>
	/// The sprite pass, after every entity has drawn its see-through half - see <see cref="RenderWorld"/>. The
	/// world's particles draw here: they write no depth, so a see-through surface drawn after one would paint
	/// over it even from behind.
	/// </summary>
	public void RenderSprites()
	{
		OnRenderSprites();
	}

	/// <summary>
	/// Draws the world, a pass at a time over every entity in the order they were made: the solid geometry,
	/// then everything see-through, then the sprites. The original hands its particles' quads to the batches
	/// after the scene's models and draws the batches furthest first (FUN_00576a00; park.md, "The sprite pass");
	/// nothing here is sorted, so the sprites take a pass of their own, last.
	/// </summary>
	internal static void RenderWorld()
	{
		All.ForEach( entity => entity.Render() );

		// Everything see-through comes after everything solid, so a graded surface blends over a
		// finished picture rather than into a half-drawn one.
		//
		// This pass is not sorted within itself. These surfaces write depth, so two of them resolve by
		// distance rather than by the order their entities were created in.
		//
		// What a sort would still buy is blend order between two genuinely graded surfaces that
		// overlap. The original does sort for exactly that, per triangle and back to front, and
		// only for its graded and additive batches (FUN_00565590). Nothing in the lobby needs it:
		// the graded surfaces here are the shoreline ripples and the Space dish's cone, each a
		// single layer that does not overlap another.
		All.ForEach( entity => entity.RenderTranslucent() );

		All.ForEach( entity => entity.RenderSprites() );
	}

	/// <summary>
	/// Third render pass, over the finished world with depth cleared - see
	/// <see cref="Level.Render"/>. For things that belong to the screen rather than the scene; the
	/// advisor is the one that uses it.
	/// </summary>
	public void RenderOverlay()
	{
		OnRenderOverlay();
	}

	public void Update()
	{
		// Deleted earlier in the same walk: still in All until the walk is over, but finished with.
		if ( IsDeleted )
			return;

		OnUpdate();
	}

	/// <summary>Whether <see cref="Delete"/> has been called. It stays in <see cref="All"/> until <see cref="ApplyDeletions"/>.</summary>
	public bool IsDeleted { get; private set; }

	/// <summary>Entities deleted since the deletions were last applied.</summary>
	private static readonly List<Entity> Deleted = new();

	/// <summary>
	/// Ends this entity: <see cref="OnDelete"/> runs now, once, and the entity leaves <see cref="All"/> the next
	/// time <see cref="ApplyDeletions"/> runs - between passes, never during one. The passes walk All with
	/// List.ForEach, which throws if the list changes under it, so taking an entity out at once would break
	/// whatever walk it was deleted from. Deleting it again does nothing.
	/// </summary>
	public void Delete()
	{
		if ( IsDeleted )
			return;

		IsDeleted = true;
		OnDelete();
		Deleted.Add( this );
	}

	/// <summary>Takes every deleted entity out of <see cref="All"/>. Only between passes - see <see cref="Delete"/>.</summary>
	internal static void ApplyDeletions()
	{
		if ( Deleted.Count == 0 )
			return;

		foreach ( var entity in Deleted )
			All.Remove( entity );

		Deleted.Clear();
	}

	protected virtual void OnRender() { }
	protected virtual void OnRenderTranslucent() { }
	protected virtual void OnRenderSprites() { }
	protected virtual void OnRenderOverlay() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public bool Equals( Entity x, Entity y ) => x.GetHashCode() == y.GetHashCode();
	public int GetHashCode( [DisallowNull] Entity obj ) => base.GetHashCode();
}
