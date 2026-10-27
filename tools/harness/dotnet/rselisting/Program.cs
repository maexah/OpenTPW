using OpenTPW;

// Throwaway: lists a .RSE script's instructions with its own variable names resolved, so that what a
// ride script DOES with a given variable can be read rather than guessed at. Lives in the harness cache
// on purpose - it is analysis, not a feature.

if ( args.Length < 1 )
{
    Console.Error.WriteLine( "usage: rselisting <file.RSE> [variable-name-to-highlight]" );
    return 1;
}

var watch = args.Length > 1 ? args[1] : "VAR_LETMEON";

using var stream = File.OpenRead( args[0] );
var file = new RideScriptFile( stream );

Console.WriteLine( $"file      : {args[0]}" );
Console.WriteLine( $"valid     : {file.IsValid}   version 0x{file.Version:x8}   vars {file.VariableCount}" );
Console.WriteLine( $"instrs    : {file.Instructions.Count}" );
Console.WriteLine();

Console.WriteLine( "variables:" );
for ( var i = 0; i < file.VariableNames.Count; ++i )
    Console.WriteLine( $"  {i,3}  {file.VariableNames[i]}" );
Console.WriteLine();

var watched = -1;
for ( var i = 0; i < file.VariableNames.Count; ++i )
    if ( string.Equals( file.VariableNames[i], watch, StringComparison.Ordinal ) ) watched = i;

Console.WriteLine( $"watching  : {watch} = variable {watched}" );
Console.WriteLine();

string Show( RideOperand operand ) => operand.Kind switch
{
    RideOperandKind.Variable =>
        $"{(operand.Value >= 0 && operand.Value < file.VariableNames.Count ? file.VariableNames[operand.Value] : "var?" + operand.Value)}",
    RideOperandKind.String => $"\"{file.StringAt( operand.Value ) ?? "?"}\"",
    RideOperandKind.Location => $"->{operand.Value}",
    _ => operand.Value.ToString()
};

foreach ( var instruction in file.Instructions )
{
    var touches = instruction.Operands.Any(
        o => o.Kind == RideOperandKind.Variable && o.Value == watched );

    var operands = string.Join( ", ", instruction.Operands.Select( Show ) );

    Console.WriteLine( $"{(touches ? ">>" : "  ")} {instruction.Address,5}  {instruction.Opcode,-18} {operands}" );
}

return 0;
