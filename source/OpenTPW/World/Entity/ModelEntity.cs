using Veldrid;

namespace OpenTPW;

public partial class ModelEntity : Entity
{
	public Model? Model { get; set; }

	/// <summary>
	/// This mesh's see-through triangles, if it has any, held apart from <see cref="Model"/> so
	/// they can be drawn after everything solid - see <see cref="Level.Render"/>. They write depth
	/// just as the solid half does; what they need the separate pass for is blending over a
	/// finished picture. A mesh is usually all one or all the other, but it doesn't have to be:
	/// the Space island's antenna is a translucent dish and cone on a solid stalk.
	/// </summary>
	public Model? TranslucentModel { get; set; }

	/// <summary>
	/// How visible this model is, 0 to 1. At 1 - which is everything, normally - the shader's
	/// output alpha is whatever its texture said, so nothing changes. At 0 it is skipped entirely
	/// rather than drawn invisibly.
	/// </summary>
	public float Opacity { get; set; } = 1f;

	/// <summary>
	/// Set when this model is drawn by whatever owns it, through <see cref="DrawOverlay"/>, rather
	/// than as part of the scene - so the world passes leave it alone.
	/// </summary>
	public bool DrawnByOwner { get; set; }

	public ModelEntity()
	{
		Spawn();
	}

	public virtual void Spawn()
	{

	}

	protected override void OnRender()
	{
		if ( Model == null || Opacity <= 0f || DrawnByOwner )
			return;

		Draw( Model, Camera.ViewMatrix, Camera.ProjMatrix, Level.SunLight?.Position ?? Vector3.Zero,
			Level.SunLight?.Color ?? Vector3.One, Level.FogDensity, worldNormals: true );
	}

	protected override void OnRenderTranslucent()
	{
		if ( TranslucentModel == null || Opacity <= 0f || DrawnByOwner )
			return;

		Draw( TranslucentModel, Camera.ViewMatrix, Camera.ProjMatrix, Level.SunLight?.Position ?? Vector3.Zero,
			Level.SunLight?.Color ?? Vector3.One, Level.FogDensity, worldNormals: true );
	}

	/// <summary>
	/// Draws one half of this model - its solid half, or with <paramref name="translucent"/> its
	/// see-through half - with a view, projection and light of the caller's own, and no fog, for a
	/// model that sits on the screen rather than in the world.
	///
	/// A caller drawing several of these over each other has to draw every solid half before any
	/// see-through one, the order the scene uses, so that a graded surface blends over finished
	/// geometry rather than into it.
	/// </summary>
	/// <param name="ambient">Light every surface gets regardless of facing; 0 for the world's own.</param>
	/// <param name="worldNormals">Light with normals turned by the model matrix - see ObjectUniformBuffer.</param>
	/// <param name="translucent">Which half to draw: false for the solid one, true for the see-through one.</param>
	public void DrawOverlay( System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor, float ambient = 0f, bool worldNormals = false, bool translucent = false )
	{
		if ( Opacity <= 0f )
			return;

		if ( (translucent ? TranslucentModel : Model) is { } half )
			Draw( half, view, projection, lightPosition, lightColor, 0f, ambient, worldNormals );
	}

	private void Draw( Model model, System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor, float fogDensity, float ambient = 0f, bool worldNormals = false )
	{
		var uniformBuffer = new ObjectUniformBuffer
		{
			g_mModel = ModelMatrix,
			g_mView = view,
			g_mProj = projection,
			g_vLightPos = lightPosition,
			g_vLightColor = lightColor,
			g_vCameraPos = Camera.Position,
			g_flTime = Time.Now,
			g_vFogColour = Level.FogColour,
			g_flOpacity = Opacity,
			g_flFogDensity = fogDensity,
			g_flAmbient = ambient,
			g_flWorldNormals = worldNormals ? 1f : 0f,

			_padding0 = 0,
			_padding1 = 0,
		};

		model.Material.Set( "ObjectUniformBuffer", uniformBuffer );
		model.Draw();
	}
}
