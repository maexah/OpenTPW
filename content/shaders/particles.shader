vertex {
    //
    // On-screen particles: quads already placed in clip space, each vertex carrying its particle's
    // colour. There is no colour in the vertex format, so the colour rides in the material flags
    // word as 0xAARRGGBB - see ScreenParticles.
    //
    layout(location = 0) in vec3 position;
    layout(location = 1) in vec3 normal;
    layout(location = 2) in vec2 texCoords;
    layout(location = 3) in int texIndex;
    layout(location = 4) in uint matFlags;

    layout(location = 0) out VS_OUT {
        vec2 vTexCoords;
        vec4 vColour;
    } vs_out;

    layout(location = 5) out flat int outIgnoresAlpha;

    void main() {
        vs_out.vTexCoords = texCoords;
        vs_out.vColour = vec4(
            float((matFlags >> 16) & 0xFFu),
            float((matFlags >> 8) & 0xFFu),
            float(matFlags & 0xFFu),
            float((matFlags >> 24) & 0xFFu)) / 255.0;

        outIgnoresAlpha = texIndex;

        gl_Position = vec4(position.xy, 0.0, 1.0);
    }
}

fragment {
    layout(location = 0) in VS_OUT {
        vec2 vTexCoords;
        vec4 vColour;
    } vs_out;

    layout(location = 5) in flat int ignoresAlpha;

    layout(location = 0) out vec4 fragColor;

    layout(set = 0, binding = 0) uniform texture2D Color;
    layout(set = 0, binding = 1) uniform sampler s_Color;

    void main() {
        // The sprite times the particle's colour, alpha included, as the original's default texture
        // stage modulates them.
        fragColor = texture(sampler2D(Color, s_Color), vs_out.vTexCoords) * vs_out.vColour;

        // A sprite added without its alpha scaling it (one-one blending) - the additive material
        // scales by alpha, so alpha is made to count for nothing.
        if (ignoresAlpha != 0)
            fragColor.a = 1.0;
    }
}
