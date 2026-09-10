using Veldrid;

namespace OpenTPW;

public partial class ModelEntity : Entity
{
	public Model? Model { get; set; }

	/// <summary>
	/// How visible this model is, 0 to 1. At 1 - which is everything, normally - the shader's
	/// output alpha is whatever its texture said, so nothing changes. At 0 it is skipped entirely
	/// rather than drawn invisibly.
	/// </summary>
	public float Opacity { get; set; } = 1f;

	public ModelEntity()
	{
		Spawn();
	}

	public virtual void Spawn()
	{

	}

	protected override void OnRender()
	{
		if ( Model == null || Opacity <= 0f )
			return;

		var uniformBuffer = new ObjectUniformBuffer
		{
			g_mModel = ModelMatrix,
			g_mView = Camera.ViewMatrix,
			g_mProj = Camera.ProjMatrix,
			g_vLightPos = Level.SunLight?.Position ?? Vector3.Zero,
			g_vLightColor = Level.SunLight?.Color ?? Vector3.One,
			g_vCameraPos = Camera.Position,
			g_flTime = Time.Now,
			g_vFogColour = Level.FogColour,
			g_flOpacity = Opacity,
			g_flFogDensity = Level.FogDensity,

			_padding0 = 0,
			_padding1 = 0,
		};

		Model.Material.Set( "ObjectUniformBuffer", uniformBuffer );
		Model.Draw();
	}
}
