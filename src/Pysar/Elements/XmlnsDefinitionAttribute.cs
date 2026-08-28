namespace Pysar.Elements
{
    /// <summary>Maps a XAML XML namespace URI to a CLR namespace in this assembly.</summary>
    /// <remarks>
    ///     This is the one Pysar itself reads, in <c>XamlTypeResolver</c>. The assembly also
    ///     carries <c>System.Windows.Markup.XmlnsDefinitionAttribute</c>, which only the IDE XAML
    ///     editors read. Both are needed; see the remarks on that type for why.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace) : Attribute
    {
        public string XmlNamespace { get; } = xmlNamespace;
        public string ClrNamespace { get; } = clrNamespace;
    }
}
