using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace OpenTPW.Tests;

/// <summary>
/// Which compiled methods name a type or a member, read from their IL: the check behind a "Dead by CODE: nothing calls
/// it" label (<c>AGENTS.md</c> rule 3). A call, a field read, <c>new</c>, <c>typeof</c> and a delegate's <c>ldftn</c> all
/// count. A name reached only by string (a settings key, reflection) does not: grep for that.
/// </summary>
internal static class ILReferences
{
	private const BindingFlags Every = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

	private static readonly Dictionary<short, OpCode> Codes = typeof( OpCodes ).GetFields( BindingFlags.Public | BindingFlags.Static )
		.Select( field => (OpCode)field.GetValue( null )! )
		.ToDictionary( code => code.Value );

	/// <summary>The methods, outside <paramref name="target"/> and the types nested in it, that name it or any of its members; "Type.Method" each.</summary>
	public static string[] UsersOf( Type target, params Assembly[] assemblies )
		=> Users( assemblies, ( type, _ ) => Inside( type, target ), member => member is Type type ? Same( type, target ) : member.DeclaringType is { } owner && Same( owner, target ) );

	/// <summary>The methods, other than <paramref name="target"/> itself, that name it; "Type.Method" each.</summary>
	public static string[] CallersOf( MethodBase target, params Assembly[] assemblies )
		=> Users( assemblies, ( _, method ) => Is( method, target ), member => member is MethodBase method && Is( method, target ) );

	private static bool Is( MethodBase a, MethodBase b )
		=> a.Module == b.Module && a.MetadataToken == b.MetadataToken;

	private static bool Same( Type a, Type b )
		=> (a.IsGenericType && !a.IsGenericTypeDefinition ? a.GetGenericTypeDefinition() : a) == b;

	private static bool Inside( Type? type, Type outer )
	{
		for ( ; type != null; type = type.DeclaringType )
		{
			if ( type == outer )
				return true;
		}

		return false;
	}

	private static string[] Users( Assembly[] assemblies, Func<Type, MethodBase, bool> skip, Func<MemberInfo, bool> names )
	{
		var users = new List<string>();

		foreach ( var type in assemblies.SelectMany( Types ) )
		{
			var methods = type.GetMethods( Every ).Cast<MethodBase>().Concat( type.GetConstructors( Every ) );

			foreach ( var method in methods )
			{
				if ( !skip( type, method ) && Tokens( method ).Any( member => names( member ) ) )
					users.Add( $"{type.FullName}.{method.Name}" );
			}
		}

		return users.Distinct().ToArray();
	}

	private static IEnumerable<Type> Types( Assembly assembly )
	{
		try
		{
			return assembly.GetTypes();
		}
		catch ( ReflectionTypeLoadException partial )
		{
			return partial.Types.OfType<Type>();
		}
	}

	/// <summary>Every member and type a method's body names by token.</summary>
	private static IEnumerable<MemberInfo> Tokens( MethodBase method )
	{
		var il = method.GetMethodBody()?.GetILAsByteArray();

		if ( il == null )
			yield break;

		var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
		var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

		for ( var at = 0; at < il.Length; )
		{
			short value = il[at++];

			if ( value == 0xFE )
				value = unchecked( (short)(0xFE00 | il[at++]) );

			var code = Codes[value];

			switch ( code.OperandType )
			{
				case OperandType.InlineMethod:
				case OperandType.InlineField:
				case OperandType.InlineType:
				case OperandType.InlineTok:
					if ( Resolve( method.Module, BitConverter.ToInt32( il, at ), typeArguments, methodArguments ) is { } member )
						yield return member;
					at += 4;
					break;
				case OperandType.InlineSwitch:
					at += 4 + 4 * BitConverter.ToInt32( il, at );
					break;
				default:
					at += Size( code.OperandType );
					break;
			}
		}
	}

	private static MemberInfo? Resolve( Module module, int token, Type[]? typeArguments, Type[]? methodArguments )
	{
		try
		{
			return module.ResolveMember( token, typeArguments, methodArguments );
		}
		catch ( Exception e ) when ( e is ArgumentException or BadImageFormatException or TypeLoadException or MissingMemberException )
		{
			return null;
		}
	}

	private static int Size( OperandType operand ) => operand switch
	{
		OperandType.InlineNone => 0,
		OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
		OperandType.InlineVar => 2,
		OperandType.InlineI8 or OperandType.InlineR => 8,
		_ => 4,
	};
}
