using System;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>Owns the existing frame-clock inputs for a replay, restoring all clock state afterwards.</summary>
internal sealed class SimulationClockScope : IDisposable
{
	private readonly (FieldInfo Field, object? Value)[] saved;

	public SimulationClockScope()
	{
		// Capture backing fields as well as the backlog and rebase flag: restoring public readings
		// alone would change the first tick of the next test. No constructors are bypassed.
		saved = new[] { typeof( Time ), typeof( GameClock ) }
			.SelectMany( type => type.GetFields( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
			.Where( field => !field.IsLiteral && !field.IsInitOnly )
			.Select( field => (field, field.GetValue( null )) ).ToArray();
		foreach ( var (field, _) in saved )
			field.SetValue( null, Activator.CreateInstance( field.FieldType ) );
		GameClock.Rebase();
		Frame( 0f );
	}

	public static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	public void Dispose()
	{
		foreach ( var (field, value) in saved )
			field.SetValue( null, value );
	}
}
