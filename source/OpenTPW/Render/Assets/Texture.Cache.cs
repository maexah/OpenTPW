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
	/// <b>The set of cache hits is unchanged, so nothing about the result differs.</b> A hit already
	/// returned before the sampler, size and path were assigned, and before
	/// <see cref="PreprocessTextureData"/> and <see cref="IsGraded"/> - both of which only touch the
	/// local pixel array - so the only difference is the work that is no longer done. A texture
	/// taken over here is not registered, exactly as a hit inside <c>CreateTexture</c> was not, which
	/// is what keeps the loading bar's learned step count the same.
	/// </para>
	///
	/// <para>
	/// The handles are safe to share because <see cref="Delete"/> removes a texture from
	/// <see cref="Asset.All"/>, which is the very list searched here - so a deleted texture cannot be
	/// handed out afterwards. That self-invalidation is why this stays a scan over the asset list
	/// rather than becoming a dictionary that would need invalidating by hand.
	/// </para>
	/// </summary>
	private bool TryAdoptCached( string path )
	{
		if ( !TryGetCachedTexture( path, out var cached ) )
			return false;

		NativeTexture = cached!.NativeTexture;
		NativeTextureView = cached!.NativeTextureView;

		// Carried across with the GPU handles: it is a property of the pixels those handles hold, so
		// a texture served from the cache has to answer the same as the one that loaded it. Left
		// out, every shared texture would read as a cut-out.
		HasGradedAlpha = cached!.HasGradedAlpha;

		return true;
	}
}
