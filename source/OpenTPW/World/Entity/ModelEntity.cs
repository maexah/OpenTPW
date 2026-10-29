using NeoVeldrid;

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
	/// With <c>w</c> 1, the colour every vertex of a park draw takes in place of its lit colour, as the original writes a
	/// pulsing model's vertex colours each frame (<c>Model_PulseTick</c>, <c>0x00469840</c>); a material drawn unlit stays
	/// white. Nought, the ordinary case, keeps the light.
	/// </summary>
	public System.Numerics.Vector4 VertexColour { get; set; }

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

	/// <summary>
	/// Gives up both halves of this model as the entity ends.
	///
	/// <para>
	/// This is the whole of a scene's release. <see cref="Level.Unload"/> deletes every entity as one
	/// scene gives way to the next, and this lets go of what each was drawing with, so models and
	/// materials do not accumulate from one scene build to the next.
	/// </para>
	/// </summary>
	protected override void OnDelete()
	{
		Model?.Delete();
		Model = null;

		TranslucentModel?.Delete();
		TranslucentModel = null;
	}

	protected override void OnRender()
	{
		if ( Model == null || Opacity <= 0f || DrawnByOwner )
			return;

		Draw( Model, SceneUniforms() );
	}

	protected override void OnRenderTranslucent()
	{
		if ( TranslucentModel == null || Opacity <= 0f || DrawnByOwner )
			return;

		Draw( TranslucentModel, SceneUniforms() );
	}

	/// <summary>
	/// What a draw of this model in the world hands its shader: the scene's camera, sun, haze, park light and first
	/// person's fog (<see cref="Level.LinearFog"/>).
	/// </summary>
	internal ObjectUniformBuffer SceneUniforms() => Uniforms( Camera.ViewMatrix, Camera.ProjMatrix,
		Level?.SunLight?.Position ?? Vector3.Zero, Level?.SunLight?.Color ?? Vector3.One, Level.FogDensity, worldNormals: true,
		parkLight: Level?.ParkLight, linearFog: Level.LinearFog );

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
	/// <param name="parkLight">
	/// The original's per-vertex lighter in place of the shader's own (<see cref="WithParkLight"/>): its ambient and the
	/// direction its light travels, <paramref name="lightColor"/> the light's colour. The advisor's draw alone passes one.
	/// </param>
	/// <param name="transform">
	/// Where to put it, instead of where the entity itself stands.
	///
	/// <para>
	/// <b>It exists so a thing can be shown somewhere without being moved.</b> No caller
	/// passes one: the object window's preview draws a fresh model of its own, never the instance
	/// standing in the park, and places it through the projection it hands over.
	/// </para>
	/// </param>
	public void DrawOverlay( System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor, float ambient = 0f, bool worldNormals = false,
		bool translucent = false, System.Numerics.Matrix4x4? transform = null, ParkLight? parkLight = null )
	{
		if ( Opacity <= 0f )
			return;

		if ( (translucent ? TranslucentModel : Model) is { } half )
			Draw( half, Uniforms( view, projection, lightPosition, lightColor, 0f, ambient, worldNormals, transform, parkLight ) );
	}

	private static void Draw( Model model, ObjectUniformBuffer uniformBuffer )
	{
		model.Material.Set( "g_oUbo", uniformBuffer );
		model.Draw();
	}

	private ObjectUniformBuffer Uniforms( System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor, float fogDensity, float ambient = 0f, bool worldNormals = false,
		System.Numerics.Matrix4x4? transform = null, ParkLight? parkLight = null, System.Numerics.Vector4 linearFog = default )
	{
		return WithParkLight( new ObjectUniformBuffer
		{
			// Where the caller says, or where this entity stands. See DrawOverlay's transform.
			g_mModel = transform ?? ModelMatrix,
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

			g_vLinearFog = linearFog,
			g_vVertexColour = VertexColour,

			_padding0 = 0,
			_padding1 = 0,
		}, parkLight );
	}

	/// <summary>
	/// <paramref name="uniforms"/> with the park's light written in: a park lights the scene per vertex as the original
	/// does, and at low quality draws every vertex white (<see cref="ParkLight.FullLight"/>); the lobby, and anything
	/// drawn on the screen through <see cref="DrawOverlay"/> but the advisor, pass no light and keep the shader's own lighting.
	/// </summary>
	internal static ObjectUniformBuffer WithParkLight( ObjectUniformBuffer uniforms, ParkLight? parkLight )
	{
		uniforms.g_vAmbientColour = parkLight?.Ambient ?? Vector3.Zero;
		uniforms.g_flParkLight = parkLight != null ? 1f : 0f;
		uniforms.g_vLightTravels = parkLight?.Travels ?? Vector3.Zero;
		uniforms.g_flFullLight = ParkLight.FullLight( parkLight );
		return uniforms;
	}
}
