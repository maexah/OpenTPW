vertex {
  //
  // Rain and lightning: unlit, camera-facing quads built in world space, tinted per draw.
  //
  // The art these draw with is the original game's own - Data/Generic/Weather/Raindrop.tga and
  // lightning.tga - and Raindrop.tga has no alpha channel at all, just a white drop on black.
  // That only works drawn additively, where black adds nothing, which is why these materials ask
  // for MaterialFlags.Additive and why the tint below is a straight multiply.
  //
  layout( location = 0 ) in vec3 position;
  layout( location = 1 ) in vec3 normal;
  layout( location = 2 ) in vec2 texCoords;

  layout( set = 0, binding = 0 ) uniform ObjectUniformBuffer {
      mat4 g_mModel;
      mat4 g_mView;
      mat4 g_mProj;

      vec4 g_vTint;
  } g_oUbo;

  layout( location = 0 ) out struct VS_OUT {
      vec2 vTexCoords;
  } vs_out;

  void main() {
      vs_out.vTexCoords = texCoords;

      gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * g_oUbo.g_mModel * vec4( position, 1.0 );
  }
}

fragment {
  layout( location = 0 ) in struct VS_OUT {
      vec2 vTexCoords;
  } vs_out;

  layout( location = 0 ) out vec4 fragColor;

  layout( set = 0, binding = 0 ) uniform ObjectUniformBuffer {
      mat4 g_mModel;
      mat4 g_mView;
      mat4 g_mProj;

      vec4 g_vTint;
  } g_oUbo;

  layout( set = 1, binding = 0 ) uniform texture2D Color;
  layout( set = 1, binding = 1 ) uniform sampler s_Color;

  void main()
  {
      fragColor = texture( sampler2D( Color, s_Color ), vs_out.vTexCoords ) * g_oUbo.g_vTint;
  }
}
