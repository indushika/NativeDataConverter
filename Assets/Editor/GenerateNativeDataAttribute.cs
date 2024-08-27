using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
public sealed class GenerateNativeDataAttribute : Attribute
{
   
}
