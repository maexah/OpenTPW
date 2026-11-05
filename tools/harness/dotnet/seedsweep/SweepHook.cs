// SCRATCH (Q1023, Q1024), never committed.
namespace OpenTPW;

public static class SweepHook
{
	private static System.Func<string, System.Random>? _factory;
	private static System.Random? _shared;

	/// <summary>Makes the generator of a name; set once per seed, which also starts <see cref="Shared"/> afresh.</summary>
	public static System.Func<string, System.Random>? Factory { get => _factory; set { _factory = value; _shared = null; } }

	public static System.Random Make( string name ) => Factory?.Invoke( name ) ?? new System.Random();

	/// <summary>Stands for <c>Random.Shared</c> (the product's three static draws): one stream a seed, named "Shared".</summary>
	public static System.Random Shared => Factory is null ? System.Random.Shared : _shared ??= Make( "Shared" );
}
