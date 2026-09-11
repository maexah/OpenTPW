using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using Veldrid;

namespace OpenTPW;

public class Entity
{
	public Level Level { get; set; }
	public static List<Entity> All { get; set; } = Assembly.GetCallingAssembly().GetTypes().OfType<Entity>().ToList();

	/// <summary>
	/// Right, Up, Forward (FLU)
	/// </summary>
	public Vector3 Position;

	/// <summary>
	/// Pitch, Yaw, Roll (PYR)
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
	/// <see cref="Level.Render"/>. Almost nothing has anything to draw here.
	/// </summary>
	public void RenderTranslucent()
	{
		OnRenderTranslucent();
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
	protected virtual void OnRenderOverlay() { }
	protected virtual void OnUpdate() { }
	protected virtual void OnDelete() { }

	public bool Equals( Entity x, Entity y ) => x.GetHashCode() == y.GetHashCode();
	public int GetHashCode( [DisallowNull] Entity obj ) => base.GetHashCode();
}
