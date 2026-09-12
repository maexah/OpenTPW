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
	/// How thick the distance fade is - the multiplier on the shaders' own <c>exp( depth * 0.01 )</c>,
	/// which used to be hard-coded at 0.025. It is separate so a place can turn the fade off, and
	/// so its strength is not spread across two shaders.
	/// </summary>
	public float g_flFogDensity; // 4

	/// <summary>
	/// How much light a surface gets whichever way it faces. Zero means the shader's own 0.4, which
	/// is what everything in the world is lit with - so nothing that doesn't set this changes.
	/// </summary>
	public float g_flAmbient; // 4

	/// <summary>
	/// 1 to light with the model's normals turned by its model matrix, which is what every draw of a
	/// model in the world asks for. The untransformed normals are in the file's Y-up space while the
	/// world is Z-up, so a flat ground normal was lit as though it faced sideways, and any mesh the
	/// node tree turns was lit at the orientation it was authored in. Measured over the four island
	/// models, the normal moves by 78 to 101 degrees on average - Jungle 92.6, Fantasy 101.1, Hallow
	/// 77.6, Space 80.3 - with one mesh, Fantasy's Blade04, at 140.7.
	///
	/// 0 is left for the interface, which does not draw through <see cref="ModelEntity"/> at all -
	/// see <see cref="UI.UiMesh"/>. Its model matrix is a screen projection whose third row is zero,
	/// and mat3 of that collapses 730 of ui.wad's 10231 normals to nothing, which normalize() would
	/// turn into a NaN. Ten meshes lose every normal that way, LOLIGHT - the dimmer behind every
	/// dialog - among them. So this stays a switch rather than becoming the shader's only path.
	/// </summary>
	public float g_flWorldNormals; // 4
	public float _padding4; // 4
}
