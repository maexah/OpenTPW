namespace OpenTPW;

/// <summary>
/// How a park is lit: the theme's ambient colour and the direction its one sun travels, both set once
/// as the park loads and never changed by anything time-driven. The sun's colour stays on
/// <see cref="Level.SunLight"/>. Handed to <c>content/shaders/test.shader</c> by <see cref="ModelEntity"/>;
/// see <c>docs/exe/park-engine.md</c>, "The lighting model".
/// </summary>
/// <param name="Ambient">ThemeEngine.AmbientLightLevel's R, G, B / 255.</param>
/// <param name="Travels">LightNormal, normalised and swapped into this engine's Z-up axes.</param>
public sealed record ParkLight( Vector3 Ambient, Vector3 Travels );
