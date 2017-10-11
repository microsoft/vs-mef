namespace System.Runtime.InteropServices
{
    internal sealed class ImportedFromTypeLibAttribute : Attribute
    {
        public ImportedFromTypeLibAttribute(string tlbFile)
        {
            this.Value = tlbFile;
        }

        public string Value { get; }
    }
}