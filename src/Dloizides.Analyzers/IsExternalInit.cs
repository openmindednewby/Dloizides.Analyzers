using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[SuppressMessage("Major Code Smell", "S2094", Justification = "Compiler polyfill that enables positional records on netstandard2.0")]
[SuppressMessage("Design", "MA0182", Justification = "The compiler uses it for init accessors of positional records")]
internal static class IsExternalInit;
