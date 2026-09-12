vertex {
    layout(location = 0) in vec3 position;
    layout(location = 1) in vec3 normal;
    layout(location = 2) in vec2 texCoords;
    layout(location = 3) in int texIndex;
    layout(location = 4) in uint matFlags;

    layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
        mat4 g_mModel;
        mat4 g_mView;
        mat4 g_mProj;
        vec3 g_vLightPos;
        vec3 g_vLightColor;
        vec3 g_vCameraPos;
        float g_flTime;

        vec3 g_vFogColour;
        float g_flOpacity;
        float g_flFogDensity;
        float g_flAmbient;
        float g_flWorldNormals;
    } g_oUbo;

    layout(location = 0) out VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vWorldPosition;
        vec3 vWorldNormal;
    } vs_out;

    layout(location = 5) out flat int outTexIndex;
    layout(location = 6) out flat uint outMatFlags;

    void main() {
        vs_out.vTexCoords = texCoords;
        vs_out.vTexCoords.y = 1.0 - vs_out.vTexCoords.y;
        vs_out.vNormal = normal;
        // Normals come from the file untouched while positions have Y and Z swapped - see LobbyModel -
        // so the same swap goes on before the model matrix turns them. Only read when a draw asks
        // for g_flWorldNormals.
        vs_out.vWorldNormal = mat3(g_oUbo.g_mModel) * vec3(normal.x, normal.z, normal.y);
        vs_out.vPosition = vec3(g_oUbo.g_mModel * vec4(position, 1.0));

        vec4 pos = g_oUbo.g_mModel * vec4(position, 1.0);
        vs_out.vWorldPosition = vec3(g_oUbo.g_mView * pos);
        gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * pos;

        outTexIndex = texIndex;
        outMatFlags = matFlags;
    }
}

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vWorldPosition;
        vec3 vWorldNormal;
    } vs_out;

    layout(location = 5) in flat int texIndex;
    layout(location = 6) in flat uint outMatFlags;

    layout(location = 0) out vec4 fragColor;

    layout(set = 0, binding = 0) uniform ObjectUniformBuffer {
        mat4 g_mModel;
        mat4 g_mView;
        mat4 g_mProj;
        vec3 g_vLightPos;
        vec3 g_vLightColor;
        vec3 g_vCameraPos;
        float g_flTime;

        vec3 g_vFogColour;
        float g_flOpacity;
        float g_flFogDensity;
        float g_flAmbient;
        float g_flWorldNormals;
    } g_oUbo;

    layout( set = 1, binding = 0 ) uniform texture2D Color0;
    layout( set = 1, binding = 1 ) uniform texture2D Color1;
    layout( set = 1, binding = 2 ) uniform texture2D Color2;
    layout( set = 1, binding = 3 ) uniform texture2D Color3;
    layout( set = 1, binding = 4 ) uniform texture2D Color4;
    layout( set = 1, binding = 5 ) uniform texture2D Color5;
    layout( set = 1, binding = 6 ) uniform texture2D Color6;
    layout( set = 1, binding = 7 ) uniform texture2D Color7;
    layout( set = 1, binding = 8 ) uniform texture2D Color8;
    layout( set = 1, binding = 9 ) uniform texture2D Color9;
    layout( set = 1, binding = 10 ) uniform texture2D Color10;
    layout( set = 1, binding = 11 ) uniform texture2D Color11;
    layout( set = 1, binding = 12 ) uniform texture2D Color12;
    layout( set = 1, binding = 13 ) uniform texture2D Color13;
    layout( set = 1, binding = 14 ) uniform texture2D Color14;
    layout( set = 1, binding = 15 ) uniform texture2D Color15;
    layout( set = 1, binding = 16 ) uniform sampler s_Color;

    // The only bit of a material's flag word we act on - see ModelFile.MaterialData.IsTranslucent
    // for how it was established and for what is known about the rest.
    const uint FLAG_TRANSLUCENT = 2;

    // Not from the file: set by LobbyModel when a material's texture turns out to be a cut-out
    // mask rather than a real gradient - see Texture.HasGradedAlpha. The file only ever uses the
    // flag word's low byte, so the high bits are ours to carry this in.
    //
    // Anything that has NOT been classified takes the low reference. This shader is shared with
    // the interface, which marks all of its own vertices see-through and classifies no textures,
    // so the unmarked case has to be the gentle one.
    const uint FLAG_CUTOUT = 0x10000;

    // The two alpha references the original picks between, 16 and 240 of 255, out of the pair at
    // DAT_007012d8. Its ALPHAFUNC is GREATEREQUAL and is set once for the whole run, so a texel
    // below the reference is dropped rather than blended. Cut-out art takes the high one - a
    // frond is either there or it isn't - and a gradient takes the low one so its faint end
    // survives, which is what the Space dish's signal cone is almost entirely made of.
    const float GRADED_ALPHA_REFERENCE = 16.0 / 255.0;
    const float CUTOUT_ALPHA_REFERENCE = 240.0 / 255.0;

    void main()
    {
        vec2 finalTexCoords = vs_out.vTexCoords;

        vec3 N = normalize(g_oUbo.g_flWorldNormals > 0.5 ? vs_out.vWorldNormal : vs_out.vNormal);
        vec3 L = normalize(g_oUbo.g_vLightPos - vs_out.vWorldPosition);
        
        vec3 vDiffuse = max(dot(N, L), 0.0) * g_oUbo.g_vLightColor;
        vec3 vAmbient = vec3(g_oUbo.g_flAmbient > 0.0 ? g_oUbo.g_flAmbient : 0.4);

        vec4 vTextureSample = vec4(1, 0, 1, 1);

        if ( texIndex == 0 ) vTextureSample = texture( sampler2D( Color0, s_Color ), finalTexCoords);
        if ( texIndex == 1 ) vTextureSample = texture( sampler2D( Color1, s_Color ), finalTexCoords);
        if ( texIndex == 2 ) vTextureSample = texture( sampler2D( Color2, s_Color ), finalTexCoords);
        if ( texIndex == 3 ) vTextureSample = texture( sampler2D( Color3, s_Color ), finalTexCoords);
        if ( texIndex == 4 ) vTextureSample = texture( sampler2D( Color4, s_Color ), finalTexCoords);
        if ( texIndex == 5 ) vTextureSample = texture( sampler2D( Color5, s_Color ), finalTexCoords);
        if ( texIndex == 6 ) vTextureSample = texture( sampler2D( Color6, s_Color ), finalTexCoords);
        if ( texIndex == 7 ) vTextureSample = texture( sampler2D( Color7, s_Color ), finalTexCoords);
        if ( texIndex == 8 ) vTextureSample = texture( sampler2D( Color8, s_Color ), finalTexCoords);
        if ( texIndex == 9 ) vTextureSample = texture( sampler2D( Color9, s_Color ), finalTexCoords);
        if ( texIndex == 10 ) vTextureSample = texture( sampler2D( Color10, s_Color ), finalTexCoords);
        if ( texIndex == 11 ) vTextureSample = texture( sampler2D( Color11, s_Color ), finalTexCoords);
        if ( texIndex == 12 ) vTextureSample = texture( sampler2D( Color12, s_Color ), finalTexCoords);
        if ( texIndex == 13 ) vTextureSample = texture( sampler2D( Color13, s_Color ), finalTexCoords);
        if ( texIndex == 14 ) vTextureSample = texture( sampler2D( Color14, s_Color ), finalTexCoords);
        if ( texIndex == 15 ) vTextureSample = texture( sampler2D( Color15, s_Color ), finalTexCoords);

        vec3 vShading = vDiffuse + vAmbient;
        vec3 vOutColor = vTextureSample.xyz * vShading;

        // A material the model marks translucent keeps its texture's alpha; anything else is solid
        // whatever its texture happens to carry. Plenty of opaque art ships with an alpha channel
        // - six of the Space island's materials do - and taking the alpha from all of them ate
        // holes in geometry the game draws whole.
        bool bTranslucent = (outMatFlags & FLAG_TRANSLUCENT) != 0;

        // Most of what the flag marks is cut-out art rather than glass - every palm frond, the
        // grass, the bushes, the bats - so the test has to stay. Which reference it uses is the
        // original's own call, taken from the texture rather than from the model: art whose alpha
        // is effectively binary is cut hard, and a real gradient is cut low enough to keep its
        // faint end.
        //
        // Measured over the lobby, every see-through material lands on the low reference: the
        // .wct codec rings a halo of partly-clear texels around each hard edge, and those are
        // exactly what the original counts. The high reference is reached only by art whose
        // header switches its alpha channel off, which decodes fully opaque and clears any
        // reference at all.
        float flAlphaReference = (outMatFlags & FLAG_CUTOUT) != 0
            ? CUTOUT_ALPHA_REFERENCE
            : GRADED_ALPHA_REFERENCE;

        if (bTranslucent && vTextureSample.a < flAlphaReference) discard;

        // g_flOpacity fades the whole model - see ModelEntity.Opacity. It multiplies the alpha
        // rather than gating the texture, so fading is a real blend rather than a dissolve: the
        // model keeps its shape all the way out instead of eroding.
        float flAlpha = bTranslucent ? vTextureSample.a : 1.0;
        fragColor = vec4(vOutColor, flAlpha * g_oUbo.g_flOpacity);

        // Calculate fog using view space depth. vWorldPosition is view space despite its name -
        // see where it is written - so this is distance from the camera.
        float viewSpaceDepth = length(vs_out.vWorldPosition);
        float fogFactor = exp(viewSpaceDepth * 0.01) * g_oUbo.g_flFogDensity;
        fogFactor = clamp( fogFactor, 0, 1 );

        // Mix with fog
        fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFogColour, fogFactor);
    }
}