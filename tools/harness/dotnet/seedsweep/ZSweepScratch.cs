using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

namespace OpenTPW.Tests;

// SCRATCH, never committed (Q1023): every test of the nine files once per seed of the park audio's script generator.
public sealed class CountingRandom( int seed, string name ) : Random
{
	public static long Draws;
	public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> ByName = new();
	private readonly Random _r = new( seed );
	private void Count() { Draws++; ByName.AddOrUpdate( name, 1, ( _, n ) => n + 1 ); }
	public override int Next() { Count(); return _r.Next(); }
	public override int Next( int max ) { Count(); return _r.Next( max ); }
	public override int Next( int min, int max ) { Count(); return _r.Next( min, max ); }
	public override long NextInt64() { Count(); return _r.NextInt64(); }
	public override long NextInt64( long max ) { Count(); return _r.NextInt64( max ); }
	public override long NextInt64( long min, long max ) { Count(); return _r.NextInt64( min, max ); }
	public override float NextSingle() { Count(); return _r.NextSingle(); }
	public override double NextDouble() { Count(); return _r.NextDouble(); }
	public override void NextBytes( byte[] buffer ) { Count(); _r.NextBytes( buffer ); }
	public override void NextBytes( Span<byte> buffer ) { Count(); _r.NextBytes( buffer ); }
	protected override double Sample() { Count(); return _r.NextDouble(); }
}

/// <summary>The context a test class with a <c>TestContext</c> property gets (the runner is not MSTest's).</summary>
public sealed class SweepContext : TestContext
{
	public override System.Collections.IDictionary Properties { get; } = new System.Collections.Hashtable();
	public override void WriteLine( string format, params object?[] args ) { }
	public override void Write( string format, params object?[] args ) { }
	public override void AddResultFile( string fileName ) { }
	public override void Write( string message ) { }
	public override void WriteLine( string message ) { }
}

[TestClass]
public class ZSweepScratch
{
	// Rows that fail under the sweep by construction, not by a seed: ParkWeatherFreshTests count on fresh parks' unseeded rolls
	// differing, and the sweep gives every generator of a name one stream per seed (Q1024).
	private static readonly string[] ByConstruction = [ "ParkWeatherFreshTests" ];

	/// <summary>
	/// The 31 ms clock starts each row at nought. Left to grow through a long sweep it passes where a float holds a tick count
	/// exactly (about 2^24 ms), and ParkTickTests.EveryWalkIsNoticedWithinOneTickOfComingDue fails by 32 ms from seed 194 on, not by a seed.
	/// </summary>
	private static void ResetGameClock()
	{
		typeof( GameClock ).GetProperty( nameof( GameClock.Ticks ) )!.SetValue( null, 0 );
		typeof( GameClock ).GetProperty( nameof( GameClock.Now ) )!.SetValue( null, 0f );
		GameClock.Rebase();
	}

	/// <summary>A test's rows: its DataRows, then its DynamicData source's, or one empty row.</summary>
	private static List<object?[]> RowsOf( MethodInfo method, Type type )
	{
		var rows = method.GetCustomAttributes<DataRowAttribute>().Select( r => r.Data ).ToList();
		foreach ( var dynamic in method.GetCustomAttributes<DynamicDataAttribute>() )
		{
			// MSTest 2.2.7 keeps the source's name, kind and declaring type in private fields; read them by their types.
			var fields = typeof( DynamicDataAttribute ).GetFields( BindingFlags.NonPublic | BindingFlags.Instance );
			var name = (string) fields.First( f => f.FieldType == typeof( string ) ).GetValue( dynamic )!;
			var owner = (Type?) fields.FirstOrDefault( f => f.FieldType == typeof( Type ) )?.GetValue( dynamic ) ?? type;
			var isMethod = fields.Any( f => f.FieldType.IsEnum && f.GetValue( dynamic )!.ToString() == "Method" );
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
			object? source = isMethod
				? owner.GetMethod( name, flags )!.Invoke( null, null )
				: owner.GetProperty( name, flags )?.GetValue( null ) ?? owner.GetField( name, flags )?.GetValue( null );
			rows.AddRange( (IEnumerable<object?[]>) source! );
		}

		if ( rows.Count == 0 )
			rows.Add( [] );

		return rows;
	}

	/// <summary>The arguments as the method takes them: a params array packed, an optional parameter left to its default.</summary>
	private static object?[] Bind( MethodInfo method, object?[] row )
	{
		var parameters = method.GetParameters();
		if ( parameters.Length > 0 && parameters[^1].GetCustomAttribute<ParamArrayAttribute>() != null )
		{
			var fixedCount = parameters.Length - 1;
			var args = new object?[parameters.Length];
			Array.Copy( row, args, Math.Min( fixedCount, row.Length ) );
			var rest = row.Skip( fixedCount ).ToArray();
			var element = parameters[^1].ParameterType.GetElementType()!;
			var array = Array.CreateInstance( element, rest.Length );
			Array.Copy( rest, array, rest.Length );
			args[^1] = array;
			return args;
		}

		return parameters.Select( ( p, i ) => i < row.Length ? row[i] : p.DefaultValue ).ToArray();
	}

	[TestMethod]
	public void Sweep()
	{
		var lo = int.Parse( Environment.GetEnvironmentVariable( "SWEEP_LO" ) ?? "-1" );
		if ( lo < 0 )
			return;
		var hi = int.Parse( Environment.GetEnvironmentVariable( "SWEEP_HI" )! );
		var log = Environment.GetEnvironmentVariable( "SWEEP_LOG" )!;
		var only = Environment.GetEnvironmentVariable( "SWEEP_CLASSES" )?.Split( ',' );
		var names = new[] { "ParkCarSoundsTests", "ParkEvictionTests", "ParkGuestSoundHeightTests", "ParkPlacedObjectsTests", "ParkScreamChainTests",
			"ParkStaffSoundTests", "ParkVoiceRangeTests", "SoundBankKeptTests", "VoicePlacementTests", "ScriptSoundTests" };
		var all = Environment.GetEnvironmentVariable( "SWEEP_ALL" ) == "1";
		var types = typeof( ZSweepScratch ).Assembly.GetTypes().Where( t => !ByConstruction.Contains( t.Name ) && ( all ? t.GetCustomAttribute<TestClassAttribute>() != null && t != typeof( ZSweepScratch ) : names.Contains( t.Name ) ) && ( only is null || only.Contains( t.Name ) ) ).OrderBy( t => t.Name ).ToArray();
		var onlyFile = Environment.GetEnvironmentVariable( "SWEEP_ONLY" );
		var onlyTests = onlyFile is null ? null : File.ReadAllLines( onlyFile ).ToHashSet();
		var clock = Stopwatch.StartNew();

		for ( var seed = lo; seed <= hi; seed++ )
		{
			var s = seed; var ran = 0;
			var raw = Environment.GetEnvironmentVariable( "SWEEP_RAW" ) == "1";
			SweepHook.Factory = name => new CountingRandom( raw ? s : s * 7919 + name.Aggregate( 17, ( h, c ) => h * 31 + c ), name );

			foreach ( var type in types )
			{
				foreach ( var method in type.GetMethods( BindingFlags.Public | BindingFlags.Instance ).Where( m => m.GetCustomAttribute<TestMethodAttribute>() != null ) )
				{
					if ( onlyTests is not null && !onlyTests.Contains( type.Name + "." + method.Name ) )
						continue;
					var rows = RowsOf( method, type );
					var rowNumber = 0;
					foreach ( var row in rows )
					{
						rowNumber++; ran++;
						Time.Now = 0f; Time.Delta = 0f; ResetGameClock(); var instance = Activator.CreateInstance( type )!;
						type.GetProperty( "TestContext" )?.SetValue( instance, new SweepContext() );
						var init = type.GetMethods().Where( m => m.GetCustomAttribute<TestInitializeAttribute>() != null );
						var cleanup = type.GetMethods().Where( m => m.GetCustomAttribute<TestCleanupAttribute>() != null );
						string? failure = null; CountingRandom.Draws = 0; CountingRandom.ByName.Clear(); var t0 = clock.ElapsedMilliseconds;
						try
						{
							foreach ( var m in init ) m.Invoke( instance, null );
							// An async test is awaited: left running, it fails unseen and publishes into the next row (UpdateChecker's offer).
							if ( method.Invoke( instance, Bind( method, row ) ) is System.Threading.Tasks.Task task )
								task.GetAwaiter().GetResult();
						}
						catch ( Exception e )
						{
							var inner = e is TargetInvocationException { InnerException: { } i } ? i : e;
							if ( inner.GetType().Name.Contains( "Inconclusive" ) ) { failure = "SKIP"; }
							else failure = inner.GetType().Name + ": " + inner.Message.Split( '\n' )[0] + ( inner is AssertFailedException ? "" : " @ " + ( inner.StackTrace ?? "" ).Split( '\n' )[0].Trim() );
						}
						finally
						{
							try { foreach ( var m in cleanup ) m.Invoke( instance, null ); } catch ( Exception e ) { failure ??= "CLEANUP " + e.Message.Split( '\n' )[0]; }
						}

						if ( Environment.GetEnvironmentVariable( "SWEEP_PROFILE" ) == "1" )
							File.AppendAllText( log + ".profile", $"{type.Name}.{method.Name}#{rowNumber} draws {CountingRandom.Draws} [{string.Join( ",", CountingRandom.ByName.OrderBy( p => p.Key ).Select( p => p.Key + "=" + p.Value ) )}] ms {clock.ElapsedMilliseconds - t0} {failure ?? "ok"}\n" );
						if ( failure is not null && failure != "SKIP" )
							File.AppendAllText( log, $"seed {seed} FAIL {type.Name}.{method.Name}#{rowNumber}: {failure}\n" );
					}
				}
			}

			File.AppendAllText( log + ".progress", $"seed {seed} ran {ran} done {clock.Elapsed.TotalSeconds:F0}s\n" );
		}

		File.AppendAllText( log, $"DONE {lo}..{hi}\n" );
	}
}
