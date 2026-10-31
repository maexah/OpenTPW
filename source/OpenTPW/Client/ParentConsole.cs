using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// A Release build on Windows has no console of its own (the project's WinExe output, Q979), so a start from a terminal
/// would print nowhere. This joins the terminal that started the process, when there is one, so
/// <c>OpenTPW.exe --game ...</c> typed at a prompt prints its lines there. <c>cmd</c> does not wait for a windowed
/// program: its prompt comes back at once, the lines land after it, and a line typed there goes to <c>cmd</c> or to the
/// debug console, whichever reads first; the debug console wants a Debug build. A start from Explorer has no such
/// terminal: the call fails and nothing opens, the log and the failed start's box are all there is. A Debug build
/// already has its console, and the call fails there too, harmlessly.
/// </summary>
public static class ParentConsole
{
	/// <summary>The argument that means "the process that started this one" (<c>ATTACH_PARENT_PROCESS</c>).</summary>
	private const uint AttachParentProcess = 0xFFFFFFFF;

	[DllImport( "kernel32", SetLastError = true )]
	private static extern bool AttachConsole( uint processId );

	/// <summary>Answers whether this process now prints into its parent's terminal. Always false off Windows. Call before anything reads <see cref="Console.Out"/>.</summary>
	public static bool Attach()
	{
		if ( !OperatingSystem.IsWindows() )
			return false;

		try
		{
			return AttachConsole( AttachParentProcess );
		}
		catch ( Exception e ) when ( e is DllNotFoundException or EntryPointNotFoundException )
		{
			return false;
		}
	}
}
