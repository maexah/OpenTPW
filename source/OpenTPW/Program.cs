namespace OpenTPW;

/// <summary>
/// Program entry point
/// </summary>
public class Program
{
	public static void Main( string[] args )
	{
		// Before anything can reach the Vulkan bindings - see NativeLibraries.
		NativeLibraries.Register();

		Game.Run( args );
	}
}
