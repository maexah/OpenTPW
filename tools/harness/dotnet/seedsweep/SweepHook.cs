// SCRATCH (Q1023), never committed.
namespace OpenTPW;

public static class SweepHook
{
	public static System.Func<string, System.Random>? Factory;
	public static System.Random Make( string name ) => Factory?.Invoke( name ) ?? new System.Random();
}
