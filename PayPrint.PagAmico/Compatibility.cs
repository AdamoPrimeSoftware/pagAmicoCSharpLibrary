#if !NET5_0_OR_GREATER

// Le proprieta' "init" richiedono questo tipo, che .NET Framework e netstandard2.0 non hanno.
// Dichiararlo qui permette di compilare lo stesso sorgente anche per le applicazioni legacy,
// senza rinunciare alla sintassi moderna nella libreria.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
