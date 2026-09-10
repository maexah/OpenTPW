using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

[StructLayout( LayoutKind.Sequential )]
struct ObjectUniformBuffer
{
	/*
	 * These fields are padded so that they're
	 * aligned (as blocks) to multiples of 16.
	 */

	public Matrix4x4 g_mModel; // 64
	public Matrix4x4 g_mView; // 64
	public Matrix4x4 g_mProj; // 64

	public Vector3 g_vLightPos; // 12
	public float _padding0; // 4

	public Vector3 g_vLightColor; // 12
	public float _padding1; // 4

	public Vector3 g_vCameraPos; // 12
	public float g_flTime; // 4

	/// <summary>
	/// What distance fades to. It used to be a constant in the shaders, matching the one sky
	/// colour the lobby ever had; parks set their own SKYCOLOUR, so the distance has to follow
	/// or the fog band stops meeting the sky at the horizon.
	/// </summary>
	public Vector3 g_vFogColour; // 12

	/// <summary>
	/// Fades a model out without touching its textures. 1 is the ordinary case and costs nothing;
	/// below that the model blends, which is how a lobby swarm leaves when its island stops being
	/// the one on show - see <see cref="LobbyFlyer"/>.
	/// </summary>
	public float g_flOpacity; // 4

	/// <summary>
	/// How thick the distance fade is, as one over the distance it takes to fade something to 1/e
	/// of itself.
	///
	/// This used to be a fixed exponential *growth* in the shaders, which reached full strength
	/// about three hundred units out and painted everything past it one flat colour - on an ocean
	/// running to five thousand, that is most of the water, and it put a hard band between the sea
	/// and the sky. A real e-folding hazes over the whole distance instead.
	/// </summary>
	public float g_flFogDensity; // 4

	public float _padding2; // 4
	public float _padding3; // 4
	public float _padding4; // 4
}
