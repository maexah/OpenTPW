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
        float g_flScrollU;

        vec3 g_vAmbientColour;
        float g_flParkLight;
        vec3 g_vLightTravels;
        float g_flFullLight;
        vec4 g_vLinearFog;
    } g_oUbo;

    layout(location = 0) out VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vViewPosition;
        vec3 vWorldNormal;
    } vs_out;

    layout(location = 5) out flat int outTexIndex;
    layout(location = 6) out flat uint outMatFlags;
    layout(location = 7) out vec3 vParkLight;
    layout(location = 8) out float vFog;

    // Set by ParkGround on the corners of a ground cell outside the park (ParkGround.HalfLight).
    const uint FLAG_HALF_LIGHT = 0x40000;

    void main() {
        // Moved across by ObjectUniformBuffer.g_flScrollU, nought for every draw but a research pump's run.
        vs_out.vTexCoords = texCoords + vec2(g_oUbo.g_flScrollU, 0.0);
        vs_out.vTexCoords.y = 1.0 - vs_out.vTexCoords.y;
        vs_out.vNormal = normal;
        // The loader works these out from the file's own positions - ModelFile.CalculateNormals - so
        // they are in the file's Y-up space while the positions drawn here have Y and Z swapped (see
        // LobbyModel), and the same swap goes on before the model matrix turns them. Read by every
        // draw of a model in the world; the interface leaves g_flWorldNormals at 0, because its model
        // matrix is a screen projection whose third row is zero and would collapse a good many of its
        // normals to nothing - see ObjectUniformBuffer.
        vs_out.vWorldNormal = mat3(g_oUbo.g_mModel) * vec3(normal.x, normal.z, normal.y);
        vs_out.vPosition = vec3(g_oUbo.g_mModel * vec4(position, 1.0));

        vec4 pos = g_oUbo.g_mModel * vec4(position, 1.0);

        // View space. The fog at the end of the fragment stage wants distance
        // from the camera, which is what this is; the lighting wants world space, and takes
        // vPosition just above instead.
        vs_out.vViewPosition = vec3(g_oUbo.g_mView * pos);
        gl_Position = g_oUbo.g_mProj * g_oUbo.g_mView * pos;

        outTexIndex = texIndex;
        outMatFlags = matFlags;

        // First person's fog (FirstPersonFog, Level.LinearFog): linear in the eye depth from the band's start to its
        // end, worked out here at the vertex and blended across the triangle, as the original's Fog_VertexSpecular writes it
        // into each vertex's colours (docs/exe/park-engine.md, "First person's fog"). Nought off it.
        vFog = 0.0;
        if (g_oUbo.g_vLinearFog.z > 0.5)
            vFog = clamp((-vs_out.vViewPosition.z - g_oUbo.g_vLinearFog.x) / (g_oUbo.g_vLinearFog.y - g_oUbo.g_vLinearFog.x), 0.0, 1.0);

        // A park is lit per vertex, as the original lights it (FUN_005741b0, docs/exe/park-engine.md,
        // "The lighting model"): the ambient colour plus the sun's colour times -(d.n) where that is not
        // negative, d being the direction the light travels taken into the model's space and normalised,
        // n the normal as the model carries it. Each channel is clamped and rounded to a byte, and the
        // card blends the result across the triangle. A sun-facing face therefore saturates to its
        // texture's own brightness.
        vParkLight = vec3(0.0);
        if (g_oUbo.g_flParkLight > 0.5)
        {
            // Low quality's GraphicalOptions.LIGHTING 0 (render-state 0x0800): every vertex takes the channel mask,
            // white, in place of the sum, so each face shows its texture's own colour and no half light
            // (Light_ModelVertexColours 0x00574660, Light_GroundVertexColours 0x00574530; docs/exe/park-engine.md,
            // "What GraphicalOptions.LIGHTING 0 turns off").
            if (g_oUbo.g_flFullLight > 0.5)
            {
                vParkLight = vec3(1.0);
                return;
            }

            vec3 d = normalize(inverse(mat3(g_oUbo.g_mModel)) * g_oUbo.g_vLightTravels);
            float facing = -dot(d, vec3(normal.x, normal.z, normal.y));
            vec3 lit = g_oUbo.g_vAmbientColour + (g_oUbo.g_vLightColor * max(facing, 0.0));
            vParkLight = round(clamp(lit, 0.0, 1.0) * 255.0) / 255.0;

            // Land outside the park (ParkGround.HalfLight): the terrain pass halves the corner's byte colour,
            // SHR 1 and AND 0x7f7f7f, while GraphicalOptions.LIGHTING is on (FUN_0056f670; docs/exe/park-engine.md,
            // "What the render-state 0x0800 tests are").
            if ((matFlags & FLAG_HALF_LIGHT) != 0)
                vParkLight = floor(round(vParkLight * 255.0) / 2.0) / 255.0;
        }
    }
}

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec3 vNormal;
        vec3 vPosition;
        vec3 vViewPosition;
        vec3 vWorldNormal;
    } vs_out;

    layout(location = 5) in flat int texIndex;
    layout(location = 6) in flat uint outMatFlags;
    layout(location = 7) in vec3 vParkLight;
    layout(location = 8) in float vFog;

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
        float g_flScrollU;

        vec3 g_vAmbientColour;
        float g_flParkLight;
        vec3 g_vLightTravels;
        float g_flFullLight;
        vec4 g_vLinearFog;
    } g_oUbo;

    // Material.TextureSlots: 31 is the most materials any shipped mesh names (Q203).
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
    layout( set = 1, binding = 16 ) uniform texture2D Color16;
    layout( set = 1, binding = 17 ) uniform texture2D Color17;
    layout( set = 1, binding = 18 ) uniform texture2D Color18;
    layout( set = 1, binding = 19 ) uniform texture2D Color19;
    layout( set = 1, binding = 20 ) uniform texture2D Color20;
    layout( set = 1, binding = 21 ) uniform texture2D Color21;
    layout( set = 1, binding = 22 ) uniform texture2D Color22;
    layout( set = 1, binding = 23 ) uniform texture2D Color23;
    layout( set = 1, binding = 24 ) uniform texture2D Color24;
    layout( set = 1, binding = 25 ) uniform texture2D Color25;
    layout( set = 1, binding = 26 ) uniform texture2D Color26;
    layout( set = 1, binding = 27 ) uniform texture2D Color27;
    layout( set = 1, binding = 28 ) uniform texture2D Color28;
    layout( set = 1, binding = 29 ) uniform texture2D Color29;
    layout( set = 1, binding = 30 ) uniform texture2D Color30;
    layout( set = 1, binding = 31 ) uniform texture2D Color31;
    layout( set = 1, binding = 32 ) uniform sampler s_Color;

    // The same filter clamped to the texture's edge, bound by Material.Set beside s_Color: a vertex
    // carrying FLAG_CLAMP samples with it.
    layout( set = 1, binding = 33 ) uniform sampler s_ColorClamp;

    // Set by LobbyModel where a material carries the file's bit of the same value and its texture an
    // alpha (LobbyModel.SeeThrough), and by UiMesh on every interface vertex.
    const uint FLAG_TRANSLUCENT = 2;

    // Not from the file: set by LobbyModel where that texture's alpha is cut out rather than
    // graded - see Texture.Alpha. The file only ever uses the flag word's low byte, so the high
    // bits are ours to carry this in.
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

    // Not from the file: set by LobbyModel where the original clamps the material's texture, its frame
    // carrying neither wrap bit (LobbyModel.Wraps). Nothing else sets it, so the paths, the ground and
    // the interface keep the sampler their caller bound.
    const uint FLAG_CLAMP = 0x20000;

    // Not from the file's flag word: set by LobbyModel where the material record's last word has 0x1 (LobbyModel.UnlitFlag).
    // The original gives such a material's vertices the channel mask, white, in place of their lit colours, so its
    // texture is drawn at its own brightness whatever the light (DrawList_AddMaterialFaces 0x00580530): the water.
    const uint FLAG_UNLIT = 0x80000;

    vec4 SampleColor(texture2D colour, vec2 uv)
    {
        if ((outMatFlags & FLAG_CLAMP) != 0)
            return texture(sampler2D(colour, s_ColorClamp), uv);

        return texture(sampler2D(colour, s_Color), uv);
    }

    void main()
    {
        vec2 finalTexCoords = vs_out.vTexCoords;

        vec3 N = normalize(g_oUbo.g_flWorldNormals > 0.5 ? vs_out.vWorldNormal : vs_out.vNormal);
        // g_vLightPos is a world-space point - Level.SunLight.Position - so what it is measured
        // against has to be world space too. Measured from the view-space position instead, the
        // sun would ride round with the orbit and light whichever side of an island faced the
        // viewer, wherever the light was actually standing.
        vec3 L = normalize(g_oUbo.g_vLightPos - vs_out.vPosition);
        
        vec3 vDiffuse = max(dot(N, L), 0.0) * g_oUbo.g_vLightColor;
        vec3 vAmbient = vec3(g_oUbo.g_flAmbient > 0.0 ? g_oUbo.g_flAmbient : 0.4);

        vec4 vTextureSample = vec4(1, 0, 1, 1);

        if ( texIndex == 0 ) vTextureSample = SampleColor(Color0, finalTexCoords);
        if ( texIndex == 1 ) vTextureSample = SampleColor(Color1, finalTexCoords);
        if ( texIndex == 2 ) vTextureSample = SampleColor(Color2, finalTexCoords);
        if ( texIndex == 3 ) vTextureSample = SampleColor(Color3, finalTexCoords);
        if ( texIndex == 4 ) vTextureSample = SampleColor(Color4, finalTexCoords);
        if ( texIndex == 5 ) vTextureSample = SampleColor(Color5, finalTexCoords);
        if ( texIndex == 6 ) vTextureSample = SampleColor(Color6, finalTexCoords);
        if ( texIndex == 7 ) vTextureSample = SampleColor(Color7, finalTexCoords);
        if ( texIndex == 8 ) vTextureSample = SampleColor(Color8, finalTexCoords);
        if ( texIndex == 9 ) vTextureSample = SampleColor(Color9, finalTexCoords);
        if ( texIndex == 10 ) vTextureSample = SampleColor(Color10, finalTexCoords);
        if ( texIndex == 11 ) vTextureSample = SampleColor(Color11, finalTexCoords);
        if ( texIndex == 12 ) vTextureSample = SampleColor(Color12, finalTexCoords);
        if ( texIndex == 13 ) vTextureSample = SampleColor(Color13, finalTexCoords);
        if ( texIndex == 14 ) vTextureSample = SampleColor(Color14, finalTexCoords);
        if ( texIndex == 15 ) vTextureSample = SampleColor(Color15, finalTexCoords);
        if ( texIndex == 16 ) vTextureSample = SampleColor(Color16, finalTexCoords);
        if ( texIndex == 17 ) vTextureSample = SampleColor(Color17, finalTexCoords);
        if ( texIndex == 18 ) vTextureSample = SampleColor(Color18, finalTexCoords);
        if ( texIndex == 19 ) vTextureSample = SampleColor(Color19, finalTexCoords);
        if ( texIndex == 20 ) vTextureSample = SampleColor(Color20, finalTexCoords);
        if ( texIndex == 21 ) vTextureSample = SampleColor(Color21, finalTexCoords);
        if ( texIndex == 22 ) vTextureSample = SampleColor(Color22, finalTexCoords);
        if ( texIndex == 23 ) vTextureSample = SampleColor(Color23, finalTexCoords);
        if ( texIndex == 24 ) vTextureSample = SampleColor(Color24, finalTexCoords);
        if ( texIndex == 25 ) vTextureSample = SampleColor(Color25, finalTexCoords);
        if ( texIndex == 26 ) vTextureSample = SampleColor(Color26, finalTexCoords);
        if ( texIndex == 27 ) vTextureSample = SampleColor(Color27, finalTexCoords);
        if ( texIndex == 28 ) vTextureSample = SampleColor(Color28, finalTexCoords);
        if ( texIndex == 29 ) vTextureSample = SampleColor(Color29, finalTexCoords);
        if ( texIndex == 30 ) vTextureSample = SampleColor(Color30, finalTexCoords);
        if ( texIndex == 31 ) vTextureSample = SampleColor(Color31, finalTexCoords);

        vec3 vShading = g_oUbo.g_flParkLight > 0.5 ? vParkLight : vDiffuse + vAmbient;
        if ((outMatFlags & FLAG_UNLIT) != 0)
            vShading = vec3(1.0);
        vec3 vOutColor = vTextureSample.xyz * vShading;

        // A see-through material keeps its texture's alpha; anything else is solid.
        bool bTranslucent = (outMatFlags & FLAG_TRANSLUCENT) != 0;

        // Which reference the test uses is the texture's, as the original's state word is: a .wct
        // with its header's byte 1 nought is cut at 240, one with it set is graded at 16
        // (docs/exe/textures.md, "The loader").
        float flAlphaReference = (outMatFlags & FLAG_CUTOUT) != 0
            ? CUTOUT_ALPHA_REFERENCE
            : GRADED_ALPHA_REFERENCE;

        if (bTranslucent && vTextureSample.a < flAlphaReference) discard;

        // g_flOpacity fades the whole model - see ModelEntity.Opacity. It multiplies the alpha
        // rather than gating the texture, so fading is a real blend rather than a dissolve: the
        // model keeps its shape all the way out instead of eroding.
        float flAlpha = bTranslucent ? vTextureSample.a : 1.0;
        fragColor = vec4(vOutColor, flAlpha * g_oUbo.g_flOpacity);

        // Fog by view-space depth, which is distance from the camera.
        float viewSpaceDepth = length(vs_out.vViewPosition);
        float fogFactor = exp(viewSpaceDepth * 0.01) * g_oUbo.g_flFogDensity;
        fogFactor = clamp( fogFactor, 0, 1 );

        // Mix with fog
        fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFogColour, fogFactor);

        // First person's: the colour times 1 - f and the fog colour times f added, the original's diffuse and
        // specular; the alpha is the texture's still.
        fragColor.xyz = mix(fragColor.xyz, g_oUbo.g_vFogColour, vFog);
    }
}