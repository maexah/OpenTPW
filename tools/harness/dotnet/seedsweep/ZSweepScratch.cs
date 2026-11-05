using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

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

[TestClass]
public class ZSweepScratch
{
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
		var types = typeof( ZSweepScratch ).Assembly.GetTypes().Where( t => ( all ? t.GetCustomAttribute<TestClassAttribute>() != null && t != typeof( ZSweepScratch ) : names.Contains( t.Name ) ) && ( only is null || only.Contains( t.Name ) ) ).OrderBy( t => t.Name ).ToArray();
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
					var rows = method.GetCustomAttributes<DataRowAttribute>().Select( r => r.Data ).ToList();
					if ( rows.Count == 0 )
						rows.Add( [] );

					var rowNumber = 0;
					foreach ( var row in rows )
					{
						rowNumber++; ran++;
						Time.Now = 0f; Time.Delta = 0f; var instance = Activator.CreateInstance( type )!;
						var init = type.GetMethods().Where( m => m.GetCustomAttribute<TestInitializeAttribute>() != null );
						var cleanup = type.GetMethods().Where( m => m.GetCustomAttribute<TestCleanupAttribute>() != null );
						string? failure = null; CountingRandom.Draws = 0; CountingRandom.ByName.Clear(); var t0 = clock.ElapsedMilliseconds;
						try
						{
							foreach ( var m in init ) m.Invoke( instance, null );
							method.Invoke( instance, row );
						}
						catch ( Exception e )
						{
							var inner = e is TargetInvocationException { InnerException: { } i } ? i : e;
							if ( inner.GetType().Name.Contains( "Inconclusive" ) ) { failure = "SKIP"; }
							else failure = inner.GetType().Name + ": " + inner.Message.Split( '\n' )[0];
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
