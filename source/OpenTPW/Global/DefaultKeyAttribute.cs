[System.AttributeUsage( AttributeTargets.Field, Inherited = false, AllowMultiple = false )]
sealed class DefaultKeyAttribute : Attribute
{
	public NeoVeldrid.Key[] Keys;

	public DefaultKeyAttribute( params NeoVeldrid.Key[] keys )
	{
		Keys = keys;
	}
}
