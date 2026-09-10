using Veldrid;

namespace OpenTPW;

public partial class ModelEntity : Entity
{
	public Model? Model { get; set; }

	/// <summary>
	/// This mesh's see-through triangles, if it has any, held apart from <see cref="Model"/> so
	/// they can be drawn without writing depth and after everything solid - see
	/// <see cref="Level.Render"/>. A mesh is usually all one or all the other, but it doesn't have
	/// to be: the Space island's antenna is a translucent dish and cone on a solid stalk.
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
			Level.SunLight?.Color ?? Vector3.One, Level.FogDensity );
	}

	protected override void OnRenderTranslucent()
	{
		if ( TranslucentModel == null || Opacity <= 0f || DrawnByOwner )
			return;

		Draw( TranslucentModel, Camera.ViewMatrix, Camera.ProjMatrix, Level.SunLight?.Position ?? Vector3.Zero,
			Level.SunLight?.Color ?? Vector3.One, Level.FogDensity );
	}

	/// <summary>
	/// Draws this model with a view, projection and light of the caller's own, and no fog - for a
	/// model that sits on the screen rather than in the world. Solid half first, then the
	/// see-through half, the same order the scene uses.
	/// </summary>
	public void DrawOverlay( System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor )
	{
		if ( Opacity <= 0f )
			return;

		if ( Model != null )
			Draw( Model, view, projection, lightPosition, lightColor, fogDensity: 0f );

		if ( TranslucentModel != null )
			Draw( TranslucentModel, view, projection, lightPosition, lightColor, fogDensity: 0f );
	}

	private void Draw( Model model, System.Numerics.Matrix4x4 view, System.Numerics.Matrix4x4 projection,
		Vector3 lightPosition, Vector3 lightColor, float fogDensity )
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

			_padding0 = 0,
			_padding1 = 0,
		};

		model.Material.Set( "ObjectUniformBuffer", uniformBuffer );
		model.Draw();
	}
}
