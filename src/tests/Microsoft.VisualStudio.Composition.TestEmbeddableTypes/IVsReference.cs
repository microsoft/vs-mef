namespace Microsoft.VisualStudio.Shell.Interop
{
    using System.Runtime.InteropServices;

    [ComImport, Guid("f4906b35-f16e-4845-80e8-27cf7d2d3329")]
    public interface IVsReference
    {
    }

    [ComImport, Guid("43c41a42-12ef-42c2-9491-6eacae060621")]
    public interface IVsRetargetProjectAsync
    {
    }

    [ComImport, Guid("5af35a1d-3038-49c6-96ef-561da57a9423")]
    public interface IVsProjectReference : IVsReference
    {
    }

    [ComImport, Guid("b66a121e-a898-4c99-a786-0275d0aff865")]
    public interface IVsTask
    {
    }

    [ComImport, Guid("aa4012c0-e6e1-41cd-ba01-0bc65bb0fae3")]
    public interface IVsProjectTargetChange
    {
    }

    [ComImport, Guid("9f8d83bb-77dc-4828-a88c-b745630cf07d")]
    public interface IVsOutputWindowPane
    {
    }

    public struct AllColorableItemInfo
    {
        public int SomeField;
    }
}
