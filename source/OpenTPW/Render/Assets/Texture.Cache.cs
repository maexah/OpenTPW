namespace OpenTPW;

partial class Texture
{
	private static bool TryGetCachedTexture( string path, out Texture? texture )
	{
		texture = null;

		if ( string.IsNullOrEmpty( path ) )
			return false;

		// No ToList(): FirstOrDefault stops at the match, where materialising the whole sequence
		// first allocated a list of every texture in the game on every lookup and then searched it.
		var existingTexture = Asset.All.OfType<Texture>().FirstOrDefault( t => t.Path == path );

		if ( existingTexture != null )
		{
			texture = existingTexture;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Takes over an already-loaded texture's GPU handles, if one of this path is loaded.
	///
	/// <para>
	/// <b>This is asked BEFORE a file is read, not after it has been decoded.</b> It used to be
	/// consulted only from <see cref="CreateTexture"/>, which runs at the END of the path
	/// constructor - so a .wct was decompressed and put through the whole wavelet decode, and the
	/// result was then thrown away in favour of the copy already in memory. The cache saved the GPU
	/// upload and none of the work.
	/// </para>
	///
	/// <para>
	/// <b>Measured on the jungle's terrain:</b> <c>base.MD2</c> names its 57 textures once per mesh
	/// that uses them - <b>1,047 loads of 57 distinct</b> across its 272 meshes - so 990 of those
	/// decodes were repeats of one already done. That phase cost 16,383 ms of an 18,930 ms park
	/// load; 99.5% of loading the terrain was this.
	/// </para>
	///
	/// <para>
	/// <b>A hit carries across everything the loading road assigns</b>, because a texture served from
	/// here never runs <c>CreateTexture</c> at all and would otherwise keep the field initialisers -
	/// the default sampler instead of the one its flags asked for, and no size and no path. <b>That is
	/// not hypothetical: it is what the lobby's sea was drawn with.</b> <c>Water.Spawn</c> asks for
	/// <c>TextureFlags.Wrap</c>, and on the way back from a park the request was served from here and
	/// drawn with the default <c>Mirror</c> instead, which flips every other tile and turns the sea's
	/// wave ripples into a diamond lattice. The paragraph this replaces claimed the skipped
	/// assignments made no difference to the result; they made that one.
	/// </para>
	///
	/// <para>
	/// A texture taken over here is not registered, exactly as a hit inside <c>CreateTexture</c> is
	/// not, which is what keeps the loading bar's learned step count the same - and it is also why
	/// assigning <see cref="Asset.Path"/> here cannot put a second entry in the cache.
	/// </para>
	///
	/// <para>
	/// The handles are safe to share because <see cref="Delete"/> removes a texture from
	/// <see cref="Asset.All"/>, which is the very list searched here - so a deleted texture cannot be
	/// handed out afterwards. That self-invalidation is why this stays a scan over the asset list
	/// rather than becoming a dictionary that would need invalidating by hand.
	/// </para>
	/// </summary>
	private bool TryAdoptCached( string path, TextureFlags flags )
	{
		if ( !TryGetCachedTexture( path, out var cached ) )
			return false;

		// Served only to a request that asked for the same thing. The flags decide the sampler AND
		// the pixels - PinkChromaKey rewrites them in place - so a texture loaded under one set is
		// not the answer to a request carrying another, and handing it over would give the second
		// caller the first's wrapping or an un-keyed copy. Nothing the game ships asks for one path
		// under two sets of flags, so this refuses nothing today; what it stops is the next caller
		// that does from quietly getting somebody else's.
		if ( cached!.Requested != flags )
			return false;

		Adopted = true;

		NativeTexture = cached.NativeTexture;
		NativeTextureView = cached.NativeTextureView;

		// Everything the loading road assigns. HasGradedAlpha is a property of the pixels these
		// handles hold, so a shared texture has to answer the same as the one that loaded it - left
		// out, every one of them read as a cut-out. The other four are the same argument: the
		// sampler its flags asked for, the size, and the path it came from.
		HasGradedAlpha = cached.HasGradedAlpha;
		SamplerType = cached.SamplerType;
		Width = cached.Width;
		Height = cached.Height;
		Path = cached.Path;

		return true;
	}
}
