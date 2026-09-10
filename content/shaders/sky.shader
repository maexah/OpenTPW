vertex {
  //
  // The lobby sky: unlit geometry textured, modulated by a colour ramp, and tinted per draw.
  //
  // This is the original's own arrangement rather than an invention. Its sky mesh is a 16x16
  // grid whose vertex colours come from a 256-entry ramp - normally a 16x16 downsample of
  // sky_rgb.tga, and in the lobby flooded flat with a single colour instead. A 16x16 ramp
  // sampled by grid position is that same table, and interpolating it across a quad is exactly
  // what Gouraud shading its four corner colours did; a flooded ramp is a 1x1 texture.
  //
  // g_vUv holds the layer's texture scale and scroll offset, so four cloud layers can share one
  // static mesh and differ only by uniforms. g_vRamp maps a world position onto the ramp: xy is
  // the grid's near corner, z is one over the grid's full span, and w is the half-texel that
  // lands vertex (col,row) on ramp texel (col,row) rather than between two of them.
  //
  layout( location = 0 ) in vec3 position;
  layout( location = 1 ) in vec3 normal;
  layout( location = 2 ) in vec2 texCoords;

  layout( set = 0, binding = 0 ) uniform ObjectUniformBuffer {
      mat4 g_mModel;
      mat4 g_mView;
      mat4 g_mProj;

      vec4 g_vTint;
      vec4 g_vUv;
      vec4 g_vRamp;
      vec4 g_vFog;
      vec4 g_vHaze;
  } g_oUbo;

  layout( location = 0 ) out struct VS_OUT {
      vec2 vTexCoords;
      vec2 vRampCoords;
      float vHaze;
  } vs_out;

  void main() {
      vs_out.vTexCoords = texCoords * g_oUbo.g_vUv.xy + g_oUbo.g_vUv.zw;

      // The world is Z-up, so the grid lies in xy and the ramp is indexed by both of them.
      vs_out.vRampCoords = ( position.xy - g_oUbo.g_vRamp.xy ) * g_oUbo.g_vRamp.z + g_oUbo.g_vRamp.w;

      vec4 pos = g_oUbo.g_mModel * vec4( position, 1.0 );

      // Haze by height rather than by distance. g_vHaze is the eye's own height and how far above
      // it the haze clears; this is linear in the vertex's height and heights interpolate
      // linearly, so working it out here is exact and costs the fragment nothing.
      vs_out.vHaze = ( g_oUbo.g_vHaze.x + g_oUbo.g_vHaze.y - pos.z ) / g_oUbo.g_vHaze.y;

      gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * pos;
  }
}

fragment {
  layout( location = 0 ) in struct VS_OUT {
      vec2 vTexCoords;
      vec2 vRampCoords;
      float vHaze;
  } vs_out;

  layout( location = 0 ) out vec4 fragColor;

  layout( set = 0, binding = 0 ) uniform ObjectUniformBuffer {
      mat4 g_mModel;
      mat4 g_mView;
      mat4 g_mProj;

      vec4 g_vTint;
      vec4 g_vUv;
      vec4 g_vRamp;
      vec4 g_vFog;
      vec4 g_vHaze;
  } g_oUbo;

  layout( set = 1, binding = 0 ) uniform texture2D Color;
  layout( set = 1, binding = 1 ) uniform sampler s_Color;

  layout( set = 2, binding = 0 ) uniform texture2D Ramp;
  layout( set = 2, binding = 1 ) uniform sampler s_Ramp;

  void main()
  {
      // sky.tga carries its clouds entirely in the alpha channel - its colour is flat white - so
      // what the layer actually paints is the ramp, and the texture only says where.
      vec4 vSky = texture( sampler2D( Color, s_Color ), vs_out.vTexCoords );
      vec4 vRamp = texture( sampler2D( Ramp, s_Ramp ), vs_out.vRampCoords );

      fragColor = vSky * vRamp * g_oUbo.g_vTint;

      // Haze toward the horizon, and the reason it is keyed to height rather than to distance is
      // that height is what the horizon *is*. The sky drops to eye level exactly where it meets
      // the sea, so hazing it fully at eye level makes both sides of that join the fog colour by
      // construction, whatever colour that happens to be and wherever the camera sits. Distance
      // cannot do that: the dome reaches the horizon anywhere between 600 and 740 units out
      // depending on how high the camera is, so any curve steep enough to be finished by 600 has
      // already eaten ten degrees of cloud above it.
      //
      // It also buries the grid's own far edge, which sits well below eye level and is where the
      // cloud texture stretches to nothing and the triangles start to show.
      fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFog.xyz, clamp( vs_out.vHaze, 0.0, 1.0 ));
  }
}
