using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A script started since the load, as a park file holds one (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a thing
/// bought and a thing sold"): its running state, the thing it belongs to, and what its loader took from its
/// <c>.RSE</c> file - the body, the string blob, the time slice - with the offset of its name in the blob (-1
/// until it names itself), its speed word and the folder it was loaded from.
/// </summary>
public sealed record MadeScript( WrittenScript Script, int Thing, byte[] Body, byte[] Strings, int NameAt,
	int TimeSlice, int Speed, string Directory );

public sealed partial class ParkScriptStates
{
	/// <summary>What the loader starts a script's speed word at (<c>FUN_005587f0</c>).</summary>
	public const int LoaderSpeed = 50;

	/// <summary>The struct's size where the file names none: 61 dwords.</summary>
	private const int MadeStructSize = 244;

	/// <summary>
	/// A whole record for a script the file does not hold (FileFormats <c>saves.md</c>, "The ride script module"):
	/// the struct as the loader <c>FUN_005587f0</c> fills it with the running state laid over, then its blocks, and
	/// its object list, the effects it has started, newest first. The addresses the game replaces as it loads are
	/// nought, an effect's two list links among them.
	///
	/// <para>
	/// <b>A walk slot's last dword is nought</b>, since a walk here does not keep it.
	/// </para>
	/// </summary>
	public static byte[] MadeRecord( MadeScript made, int modelHandle, int structSize = MadeStructSize )
	{
		var script = made.Script;
		var record = new byte[Math.Max( structSize, MadeStructSize )];

		void Dword( int index, int value ) => PutInt32( record, index * 4, value );

		Dword( HandleDword, script.Handle );
		Dword( PositionDword, script.Position );
		Dword( CallIndexDword, script.CallIndex );
		Dword( HeapIndexDword, script.HeapIndex );
		Dword( ResultDword, script.Result );
		Dword( 0x13, script.Heads?.Length ?? 0 );
		Dword( LengthDword, made.Body.Length / 4 );
		Dword( 0x15, script.Stack.Length );
		Dword( 0x16, script.Limbo?.Length ?? 0 );
		Dword( InLimboDword, script.InLimbo );
		Dword( 0x19, script.Bounce?.Length ?? 0 );
		PutInt16( record, BouncingAt, script.Bouncing );
		PutInt16( record, BouncingAt + 2, script.BounceBase );
		Dword( BounceNodeDword, script.BounceNode );
		Dword( 0x1d, made.NameAt );
		Dword( 0x1f, script.Walk?.Length ?? 0 );
		PutInt16( record, 0x88, 1000 );
		PutInt16( record, 0x8a, 1000 );
		Dword( 0x23, script.Variables.Length );
		Dword( 0x24, made.Strings.Length );
		Dword( 0x25, made.TimeSlice );
		Dword( 0x26, -1 );
		Dword( WaitDeadlineDword, (int)script.WaitDeadline );
		Dword( AnimationDeadlineDword, (int)script.AnimationDeadline );
		Dword( LoopingKeyDword, script.LoopingKey );
		PutInt16( record, ThingAt, made.Thing );
		Dword( AnimationMarkDword, script.AnimationMark );
		Dword( 0x30, made.Speed );
		Dword( TimerDeadlineDword, (int)script.TimerDeadline );
		Dword( ModelHandleDword, modelHandle );
		Dword( 0x36, -1 );
		PutInt16( record, 0xe4, 1000 );
		PutInt16( record, 0xe6, 0xffff );

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		writer.Write( record );
		writer.Write( made.Body.Length );
		writer.Write( made.Body );
		writer.Write( script.Stack.Length * 4 );

		foreach ( var value in script.Stack )
			writer.Write( value );

		writer.Write( script.Variables.Length * 4 );

		foreach ( var value in script.Variables )
			writer.Write( value );

		writer.Write( made.Strings.Length );
		writer.Write( made.Strings );

		var limbo = script.Limbo ?? [];

		writer.Write( limbo.Length * LimboSlotSize );

		foreach ( var slot in limbo )
		{
			writer.Write( slot.Handle );
			writer.Write( slot.Handle != 0 ? slot.Due : 0u );
		}

		var bounce = script.Bounce ?? [];

		writer.Write( bounce.Length * BounceSlotSize );

		foreach ( var slot in bounce )
		{
			writer.Write( slot.Handle );
			writer.Write( slot.Node );
			writer.Write( slot.Due );
			writer.Write( slot.Start );
		}

		var walk = script.Walk ?? [];

		writer.Write( walk.Length );

		foreach ( var slot in walk )
		{
			var bytes = new byte[SubRecordSize];

			// A free slot whole too: one let go keeps all but its state and its handle, and one never used is nought.
			PutInt16( bytes, 0x00, slot.WalkNode );
			PutInt16( bytes, 0x02, slot.HeadNode );
			PutInt16( bytes, 0x04, slot.OffFrom );
			PutInt16( bytes, 0x06, slot.OffTo );
			PutInt32( bytes, 0x08, (int)slot.Start );
			PutInt32( bytes, 0x0c, (int)slot.Due );
			PutInt32( bytes, 0x10, slot.Handle );
			PutInt16( bytes, 0x14, slot.Facing );
			PutInt16( bytes, 0x16, slot.Action );
			PutInt16( bytes, 0x18, slot.State );
			PutInt16( bytes, 0x1a, slot.Flags );

			writer.Write( bytes );
		}

		var heads = script.Heads ?? [];

		writer.Write( heads.Length * 4 );

		foreach ( var head in heads )
			writer.Write( head );

		var directory = Encoding.Latin1.GetBytes( made.Directory + "\0" );

		writer.Write( directory.Length );
		writer.Write( directory );
		writer.Write( Encoding.ASCII.GetBytes( ObjectGuard ) );

		WriteEffects( writer, script.Effects ?? [] );
		writer.Flush();

		return stream.ToArray();
	}

	/// <summary>An object list as it follows the guard: the count, a record's size, and each record with its two links nought.</summary>
	private static void WriteEffects( BinaryWriter writer, SavedEffect[] effects )
	{
		writer.Write( effects.Length );
		writer.Write( EffectRecordSize );

		foreach ( var effect in effects )
		{
			writer.Write( 0 );
			writer.Write( 0 );
			writer.Write( effect.Type );
			writer.Write( effect.Handle );
			writer.Write( effect.Node );
			writer.Write( effect.Index );
			writer.Write( effect.Tag );
		}
	}

	/// <summary>
	/// The running scripts of <paramref name="scripts"/> whose object list holds another count of records than the
	/// file's, each with its list: what <see cref="Put"/> leaves and <see cref="Splice"/> writes.
	/// </summary>
	public Dictionary<int, SavedEffect[]> Relisted( IEnumerable<WrittenScript> scripts )
	{
		var relisted = new Dictionary<int, SavedEffect[]>();

		foreach ( var script in scripts )
		{
			if ( script.Effects is { } effects && _byHandle.TryGetValue( script.Handle, out var saved ) && saved.Effects is { } held
				&& effects.Length != held.Length && _places[script.Handle].Effects >= 0 )
				relisted[script.Handle] = effects;
		}

		return relisted;
	}

	/// <summary>The size of one record of a script's object list, the effects it has started.</summary>
	private const int EffectRecordSize = 28;

	/// <summary>The script records of the file that belong to <paramref name="thing"/>: its own and any it started.</summary>
	public IEnumerable<int> HandlesOf( int thing ) =>
		_order.Where( handle => _byHandle.TryGetValue( handle, out var saved ) && saved.Thing == thing );

	/// <summary>
	/// <paramref name="body"/> with <paramref name="made"/> ahead of the file's records, the newest first as the
	/// game writes its list, the records of <paramref name="gone"/> taken out, and the count the records are walked
	/// by kept. A kept script of <paramref name="relisted"/> takes that object list for the file's, its record's
	/// other bytes as they stand.
	/// </summary>
	/// <exception cref="InvalidOperationException">The module was not read whole.</exception>
	public byte[] Splice( byte[] body, IReadOnlyList<byte[]> made, IReadOnlyCollection<int> gone,
		IReadOnlyDictionary<int, SavedEffect[]>? relisted = null )
	{
		if ( Problem != null || !ClosedOnGuard || _countAt < 0 || _recordsAt < 0 )
			throw new InvalidOperationException( $"the park file's scripts were not read whole: {Problem}" );

		using var stream = new MemoryStream( body.Length + made.Sum( record => record.Length ) );
		using var writer = new BinaryWriter( stream );

		stream.Write( body, 0, _recordsAt );

		foreach ( var record in made )
			stream.Write( record );

		var end = _recordsAt;
		var kept = 0;

		foreach ( var handle in _order )
		{
			var (start, stop) = _extents[handle];

			if ( start != end )
				throw new InvalidOperationException( $"script {handle}'s record begins at {start}, not where the one before ended" );

			end = stop;

			if ( gone.Contains( handle ) )
				continue;

			if ( relisted != null && relisted.TryGetValue( handle, out var effects ) && _places[handle].Effects >= 0 )
			{
				// Up to the guard's end; the count and the size are the two dwords before the first record.
				stream.Write( body, start, _places[handle].Effects - 8 - start );
				WriteEffects( writer, effects );
				writer.Flush();
			}
			else
				stream.Write( body, start, stop - start );

			++kept;
		}

		stream.Write( body, end, body.Length - end );

		var result = stream.ToArray();

		BinaryPrimitives.WriteInt32LittleEndian( result.AsSpan( _countAt, 4 ), kept + made.Count );

		return result;
	}

	/// <summary>The struct's size in this file.</summary>
	public int StructSize => _structSize;
}
