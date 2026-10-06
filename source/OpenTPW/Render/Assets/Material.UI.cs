namespace OpenTPW;

partial class Material
{
	/// <summary>
	/// Default shader for 2D UI objects
	/// </summary>
	public static Material UI = new( "content/shaders/ui.shader", MaterialFlags.DisableDepth );

	/// <summary>
	/// <b>Dead by CODE: nothing draws with it.</b> It is built from <c>3d.shader</c>, which nothing else names, the
	/// first time <see cref="Material"/> is touched, and kept with its compiled shader for the life of the process. Its one reader is the guard
	/// in <see cref="Delete"/>, which can fire only if something holds it, and nothing does: the park's ground, the
	/// lobby's models, the paths and the interface's meshes build their own <c>test.shader</c> materials, and the sea
	/// its <c>water.shader</c>. Kept and labelled, not deleted.
	/// </summary>
	public static Material Default = new Material<ObjectUniformBuffer>( "content/shaders/3d.shader" );
}
