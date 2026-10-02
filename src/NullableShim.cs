namespace System.Runtime.CompilerServices
{
    // Needed to compile against libraries (NVorbis) that carry nullable-reference annotations.
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    internal sealed class NullableAttribute : System.Attribute
    {
        public NullableAttribute(byte b) { }
        public NullableAttribute(byte[] b) { }
    }

    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    internal sealed class NullableContextAttribute : System.Attribute
    {
        public NullableContextAttribute(byte b) { }
    }
}
