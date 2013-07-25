namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    partial class CompositionTemplateFactory
    {
        private const string InstantiatedPartLocalVarName = "result";

        public CompositionConfiguration Configuration { get; set; }

        private IDisposable EmitMemberAssignment(Import import)
        {
            Requires.NotNull(import, "import");

            var importingField = import.ImportingMember as FieldInfo;
            var importingProperty = import.ImportingMember as PropertyInfo;
            Assumes.True(importingField != null || importingProperty != null);
            bool isPublic = importingField != null ? importingField.IsPublic : importingProperty.GetSetMethod(true).IsPublic;

            string tail;
            if (isPublic)
            {
                this.Write("{0}.{1} = ", InstantiatedPartLocalVarName, import.ImportingMember.Name);
                tail = ";";
            }
            else
            {
                if (importingField != null)
                {
                    this.Write(
                        "{0}.SetValue({1}, ",
                        this.GetFieldInfoExpression(importingField),
                        InstantiatedPartLocalVarName);
                    tail = ");";
                }
                else // property
                {
                    this.Write(
                        "{0}.Invoke({1}, new object[] {{ ",
                        this.GetMethodInfoExpression(importingProperty.GetSetMethod(true)),
                        InstantiatedPartLocalVarName);
                    tail = " });";
                }
            }

            return new DisposableWithAction(delegate
            {
                this.WriteLine(tail);
            });
        }

        private string GetFieldInfoExpression(FieldInfo fieldInfo)
        {
            Requires.NotNull(fieldInfo, "fieldInfo");

            if (fieldInfo.DeclaringType.IsGenericType)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.GetField({1}, BindingFlags.Instance | BindingFlags.NonPublic)",
                    this.GetClosedGenericTypeExpression(fieldInfo.DeclaringType),
                    Quote(fieldInfo.Name));
            }
            else
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.ManifestModule.ResolveField({1})",
                    this.GetAssemblyExpression(fieldInfo.DeclaringType.Assembly),
                    fieldInfo.MetadataToken);
            }
        }

        private string GetMethodInfoExpression(MethodInfo methodInfo)
        {
            Requires.NotNull(methodInfo, "methodInfo");

            if (methodInfo.DeclaringType.IsGenericType)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "MethodInfo.GetMethodFromHandle({0}.ManifestModule.ResolveMethod({1}).MethodHandle, {2})",
                    this.GetAssemblyExpression(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken,
                    this.GetClosedGenericTypeHandleExpression(methodInfo.DeclaringType));
            }
            else
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.ManifestModule.ResolveMethod({1})",
                    this.GetAssemblyExpression(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken);
            }
        }

        private string GetClosedGenericTypeExpression(Type type)
        {
            Requires.NotNull(type, "type");
            Requires.Argument(type.IsGenericTypeDefinition, "type", "GenericTypeDefinition required.");
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.ManifestModule.ResolveType({1}).MakeGenericType({2})",
                this.GetAssemblyExpression(type.Assembly),
                type.MetadataToken,
                string.Join(", ", type.GetGenericArguments().Select(t => "typeof(" + GetTypeName(t) + ")")));
        }

        private string GetClosedGenericTypeHandleExpression(Type type)
        {
            return GetClosedGenericTypeExpression(type) + ".TypeHandle";
        }

        private string GetAssemblyExpression(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            return string.Format(CultureInfo.InvariantCulture, "Assembly.Load({0})", Quote(assembly.FullName));
        }

        private void EmitImportSatisfyingAssignment(KeyValuePair<Import, IReadOnlyList<Export>> satisfyingExport)
        {
            Requires.Argument(satisfyingExport.Key.ImportingMember != null, "satisfyingExport", "No member to satisfy.");
            var import = satisfyingExport.Key;
            var importingMember = satisfyingExport.Key.ImportingMember;
            var exports = satisfyingExport.Value;

            var right = new StringWriter();
            EmitImportSatisfyingExpression(import, exports, right);
            string rightString = right.ToString();
            if (rightString.Length > 0)
            {
                using (this.EmitMemberAssignment(import))
                {
                    this.Write(rightString);
                }
            }
        }

        private void EmitImportSatisfyingExpression(Import import, IReadOnlyList<Export> exports, StringWriter writer)
        {
            if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                Type enumerableOfTType = typeof(IEnumerable<>).MakeGenericType(import.ImportDefinition.MemberWithoutManyWrapper);
                if (import.ImportDefinition.MemberType.IsArray || import.ImportDefinition.MemberType.IsEquivalentTo(enumerableOfTType))
                {
                    this.EmitSatisfyImportManyArrayOrEnumerable(import, exports);
                }
                else
                {
                    this.EmitSatisfyImportManyCollection(import, exports);
                }
            }
            else if (exports.Any())
            {
                this.EmitValueFactory(import, exports.Single(), writer);
            }
        }

        private void EmitSatisfyImportManyArrayOrEnumerable(Import import, IReadOnlyList<Export> exports)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");

            this.Write("{0}.{1} = new ", InstantiatedPartLocalVarName, import.ImportingMember.Name);
            if (import.ImportDefinition.MemberType.IsArray)
            {
                this.WriteLine("{0}[]", GetTypeName(import.ImportDefinition.MemberWithoutManyWrapper));
            }
            else
            {
                this.WriteLine("List<{0}>", GetTypeName(import.ImportDefinition.MemberWithoutManyWrapper));
            }

            this.WriteLine("{");
            using (Indent())
            {
                foreach (var export in exports)
                {
                    var valueWriter = new StringWriter();
                    EmitValueFactory(import, export, valueWriter);
                    this.WriteLine("{0},", valueWriter);
                }
            }

            this.WriteLine("};");
        }

        private void EmitSatisfyImportManyCollection(Import import, IReadOnlyList<Export> exports)
        {
            Requires.NotNull(import, "import");
            var importDefinition = import.ImportDefinition;
            Type elementType = PartDiscovery.GetElementTypeFromMany(importDefinition.MemberType);
            string elementTypeName = GetTypeName(elementType);
            Type listType = typeof(List<>).MakeGenericType(elementType);

            this.WriteLine("if ({0}.{1} == null)", InstantiatedPartLocalVarName, import.ImportingMember.Name);
            using (Indent(withBraces: true))
            {
                if (PartDiscovery.IsImportManyCollectionTypeCreateable(importDefinition))
                {
                    this.Write("{0}.{1} = new ", InstantiatedPartLocalVarName, import.ImportingMember.Name);
                    if (importDefinition.MemberType.IsAssignableFrom(listType))
                    {
                        this.Write("List<{0}>", elementTypeName);
                    }
                    else
                    {
                        this.Write(GetTypeName(importDefinition.MemberType));
                    }

                    this.WriteLine("();");
                }
                else
                {
                    this.WriteLine(
                        "throw new InvalidOperationException(\"The {0}.{1} collection must be instantiated by the importing constructor.\");",
                        import.PartDefinition.Type.Name,
                        import.ImportingMember.Name);
                }
            }

            this.WriteLine("else");
            using (Indent(withBraces: true))
            {
                this.WriteLine("{0}.{1}.Clear();", InstantiatedPartLocalVarName, import.ImportingMember.Name);
            }

            this.WriteLine(string.Empty);

            foreach (var export in exports)
            {
                var valueWriter = new StringWriter();
                EmitValueFactory(import, export, valueWriter);
                this.WriteLine("{0}.{1}.Add({2});", InstantiatedPartLocalVarName, import.ImportingMember.Name, valueWriter);
            }
        }

        private void EmitValueFactory(Import import, Export export, StringWriter writer)
        {
            using (this.ValueFactoryWrapper(import, export, writer))
            {
                if (export.PartDefinition == import.PartDefinition)
                {
                    // The part is importing itself. So just assign it directly.
                    writer.Write(InstantiatedPartLocalVarName);
                }
                else
                {
                    writer.Write("{0}(", GetPartFactoryMethodName(export.PartDefinition, import.ImportDefinition.Contract.Type.GetGenericArguments().Select(GetTypeName).ToArray()));
                    if (import.ImportDefinition.IsExportFactory)
                    {
                        writer.Write("new Dictionary<Type, object>()");
                    }
                    else
                    {
                        writer.Write("provisionalSharedObjects");
                    }

                    writer.Write("{0})",
                        import.ImportDefinition.RequiredCreationPolicy == MefV1.CreationPolicy.NonShared ? ", nonSharedInstanceRequired: true" : string.Empty);
                }
            }
        }

        private string GetExportMetadata(Export export)
        {
            var builder = new StringBuilder();
            builder.Append("new Dictionary<string, object> {");
            foreach (var metadatum in export.ExportDefinition.Metadata)
            {
                builder.AppendFormat(" {{ \"{0}\", \"{1}\" }}, ", metadatum.Key, (string)metadatum.Value);
            }
            builder.Append("}.ToImmutableDictionary()");
            return builder.ToString();
        }

        private IDisposable EmitConstructorInvocation(ComposablePartDefinition partDefinition)
        {
            var ctor = partDefinition.ImportingConstructorInfo;
            bool publicCtor = !partDefinition.Type.IsNotPublic && ctor.IsPublic;
            if (publicCtor)
            {
                this.Write("var {0} = new {1}(", InstantiatedPartLocalVarName, GetTypeName(partDefinition.Type));
            }
            else
            {
                this.WriteLine("var assembly = {0};", GetAssemblyExpression(partDefinition.Type.Assembly));
                if (partDefinition.Type.IsGenericTypeDefinition)
                {
                    this.WriteLine(
                        "var ctor = (ConstructorInfo)MethodInfo.GetMethodFromHandle(assembly.ManifestModule.ResolveMethod({0}).MethodHandle, {1});",
                        ctor.MetadataToken,
                        this.GetClosedGenericTypeHandleExpression(partDefinition.Type));
                }
                else
                {
                    this.WriteLine("var ctor = (ConstructorInfo)assembly.ManifestModule.ResolveMethod({0});", ctor.MetadataToken);
                }

                this.Write("var {0} = ({1})ctor.Invoke(new object[] {{", InstantiatedPartLocalVarName, GetTypeName(partDefinition.Type));
            }
            var indent = this.Indent();

            return new DisposableWithAction(delegate
            {
                indent.Dispose();
                if (publicCtor)
                {
                    this.WriteLine(");");
                }
                else
                {
                    this.WriteLine(" });");
                }
            });
        }

        private void EmitInstantiatePart(ComposablePart part)
        {
            using (this.EmitConstructorInvocation(part.Definition))
            {
                if (part.Definition.ImportingConstructor.Count > 0)
                {
                    this.WriteLine(string.Empty);
                    bool first = true;
                    foreach (var import in part.GetImportingConstructorImports())
                    {
                        if (!first)
                        {
                            this.WriteLine(",");
                        }

                        var expressionWriter = new StringWriter();
                        this.EmitImportSatisfyingExpression(import.Key, import.Value, expressionWriter);
                        this.Write(expressionWriter.ToString());
                        first = false;
                    }
                }
            }

            if (typeof(IDisposable).IsAssignableFrom(part.Definition.Type))
            {
                this.WriteLine("this.TrackDisposableValue({0});", InstantiatedPartLocalVarName);
            }

            this.WriteLine("provisionalSharedObjects.Add(typeof({0}), {1});", GetTypeName(part.Definition.Type), InstantiatedPartLocalVarName);

            foreach (var satisfyingExport in part.SatisfyingExports.Where(i => i.Key.ImportingMember != null))
            {
                this.EmitImportSatisfyingAssignment(satisfyingExport);
            }

            if (part.Definition.OnImportsSatisfied != null)
            {
                if (part.Definition.OnImportsSatisfied.DeclaringType.IsInterface)
                {
                    this.WriteLine("{0} onImportsSatisfiedInterface = {1};", part.Definition.OnImportsSatisfied.DeclaringType.FullName, InstantiatedPartLocalVarName);
                    this.WriteLine("onImportsSatisfiedInterface.{0}();", part.Definition.OnImportsSatisfied.Name);
                }
                else
                {
                    this.WriteLine("{0}.{1}();", InstantiatedPartLocalVarName, part.Definition.OnImportsSatisfied.Name);
                }
            }

            this.WriteLine("return {0};", InstantiatedPartLocalVarName);
        }

        private HashSet<Type> GetMetadataViewInterfaces()
        {
            var set = new HashSet<Type>();

            set.UnionWith(
                from part in this.Configuration.Parts
                from importAndExports in part.SatisfyingExports
                where importAndExports.Value.Count > 0
                let metadataType = importAndExports.Key.ImportDefinition.MetadataType
                where metadataType != null && metadataType.IsInterface && metadataType != typeof(IDictionary<string, object>)
                select metadataType);

            return set;
        }

        private IDisposable ValueFactoryWrapper(Import import, Export export, TextWriter writer)
        {
            var importDefinition = import.ImportDefinition;

            if (importDefinition.IsLazyConcreteType)
            {
                string fullTypeNameWithPerhapsLazy = GetTypeName(importDefinition.LazyType ?? importDefinition.CoercedValueType);
                if (importDefinition.MetadataType == null && importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type) && import.PartDefinition != export.PartDefinition)
                {
                    writer.Write("({0})", fullTypeNameWithPerhapsLazy);
                }
                else
                {
                    writer.Write("new {0}(() => ", fullTypeNameWithPerhapsLazy);
                }
            }
            else if (importDefinition.IsExportFactory)
            {
                writer.Write(
                    "new {0}(() => {{ var temp = ",
                    GetTypeName(importDefinition.ExportFactoryType));

                if (importDefinition.ExportFactorySharingBoundaries.Count > 0)
                {
                    writer.Write("new CompiledExportProvider(this, new [] { ");
                    writer.Write(string.Join(", ", importDefinition.ExportFactorySharingBoundaries.Select(Quote)));
                    writer.Write(" }).");
                }

                return new DisposableWithAction(delegate
                {
                    writer.Write(".Value; return Tuple.Create<{0}, Action>(temp, () => {{ ", GetTypeName(importDefinition.CoercedValueType));
                    if (typeof(IDisposable).IsAssignableFrom(export.PartDefinition.Type))
                    {
                        writer.Write("((IDisposable)temp).Dispose(); ");
                    }

                    writer.Write("}); }");
                    this.WriteExportMetadataReference(export, importDefinition, writer);
                    writer.Write(")");
                });
            }

            return new DisposableWithAction(() =>
            {
                string memberModifier = export.ExportingMember == null ? string.Empty : "." + export.ExportingMember.Name;
                string memberAccessor = ".Value" + memberModifier;
                if (importDefinition.IsLazy)
                {
                    if (importDefinition.MetadataType != null)
                    {
                        writer.Write("{0}", memberAccessor);
                        this.WriteExportMetadataReference(export, importDefinition, writer);
                        writer.Write(", true)");
                    }
                    else if (importDefinition.IsLazyConcreteType && !importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type))
                    {
                        writer.Write("{0}, true)", memberAccessor);
                    }
                    else if (import.PartDefinition == export.PartDefinition)
                    {
                        writer.Write(", true)");
                    }
                }
                else if (import.PartDefinition != export.PartDefinition)
                {
                    writer.Write(memberAccessor);
                }
            });
        }

        private void WriteExportMetadataReference(Export export, ImportDefinition importDefinition, TextWriter writer)
        {
            if (importDefinition.MetadataType != null)
            {
                writer.Write(", ");
                if (importDefinition.MetadataType != typeof(IDictionary<string, object>))
                {
                    writer.Write("new {0}(", GetClassNameForMetadataView(importDefinition.MetadataType));
                }

                writer.Write(GetExportMetadata(export));
                if (importDefinition.MetadataType != typeof(IDictionary<string, object>))
                {
                    writer.Write(")");
                }
            }
        }

        private IEnumerable<ComposablePartDefinition> RootPartDefinitions
        {
            get
            {
                return from part in this.Configuration.Parts
                       where part.RequiredSharingBoundaries.Count == 0
                       select part.Definition;
            }
        }

        private static string GetTypeName(Type type)
        {
            return GetTypeName(type, genericTypeDefinition: false);
        }

        private static string GetTypeName(Type type, bool genericTypeDefinition)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            string result = string.Empty;
            if (type.DeclaringType != null)
            {
                result = GetTypeName(type.DeclaringType, genericTypeDefinition) + ".";
            }

            if (genericTypeDefinition)
            {
                result += FilterTypeNameForGenericTypeDefinition(type, type.DeclaringType == null);
            }
            else
            {
                string[] typeArguments = type.GetGenericArguments().Select(GetTypeName).ToArray();
                result += ReplaceBackTickWithTypeArgs(type.DeclaringType == null ? type.FullName : type.Name, typeArguments);
            }

            return result;
        }

        private static string GetClassNameForMetadataView(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            if (metadataView.IsInterface)
            {
                return "ClassFor" + metadataView.Name;
            }

            return GetTypeName(metadataView);
        }

        private static string GetValueOrDefaultForMetadataView(PropertyInfo property, string sourceVarName)
        {
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0})({1}.ContainsKey(""{2}"") ? {1}[""{2}""] : ""{3}"")",
                    GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name,
                    defaultValueAttribute.Value);
            }
            else
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0}){1}[""{2}""]",
                    GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name);
            }
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, "type");

            string name = fullName ? type.FullName : type.Name;
            if (type.IsGenericType)
            {
                name = name.Substring(0, name.IndexOf('`'));
                name += "<";
                name += new String(',', type.GetGenericArguments().Length - 1);
                name += ">";
            }

            return name;
        }

        private static void Test<T>() { }

        private static string GetPartFactoryMethodNameNoTypeArgs(ComposablePartDefinition part)
        {
            string name = GetPartFactoryMethodName(part);
            int indexOfTypeArgs = name.IndexOf('<');
            if (indexOfTypeArgs >= 0)
            {
                return name.Substring(0, indexOfTypeArgs);
            }

            return name;
        }

        private static string GetPartFactoryMethodName(ComposablePartDefinition part, params string[] typeArguments)
        {
            if (typeArguments == null || typeArguments.Length == 0)
            {
                typeArguments = part.Type.GetGenericArguments().Select(t => t.Name).ToArray();
            }

            string name = "GetOrCreate" + ReplaceBackTickWithTypeArgs(part.Type.Name, typeArguments);
            return name;
        }

        private static string ReplaceBackTickWithTypeArgs(string originalName, params string[] typeArguments)
        {
            Requires.NotNullOrEmpty(originalName, "originalName");

            string name = originalName;
            int backTickIndex = originalName.IndexOf('`');
            if (backTickIndex >= 0)
            {
                name = originalName.Substring(0, name.IndexOf('`'));
                name += "<";
                int typeArgIndex = originalName.IndexOf('[', backTickIndex + 1);
                string typeArgumentsCountString = originalName.Substring(backTickIndex + 1);
                if (typeArgIndex >= 0)
                {
                    typeArgumentsCountString = typeArgumentsCountString.Substring(0, typeArgIndex - backTickIndex - 1);
                }

                int typeArgumentsCount = int.Parse(typeArgumentsCountString, CultureInfo.InvariantCulture);
                if (typeArguments == null || typeArguments.Length == 0)
                {
                    if (typeArgumentsCount == 1)
                    {
                        name += "T";
                    }
                    else
                    {
                        for (int i = 1; i <= typeArgumentsCount; i++)
                        {
                            name += "T" + i;
                            if (i < typeArgumentsCount)
                            {
                                name += ",";
                            }
                        }
                    }
                }
                else
                {
                    Requires.Argument(typeArguments.Length == typeArgumentsCount, "typeArguments", "Wrong length.");
                    name += string.Join(",", typeArguments);
                }

                name += ">";
            }

            return name;
        }

        private string GetPartOrMemberLazy(string partLocalVariableName, MemberInfo member, ExportDefinition exportDefinition)
        {
            Requires.NotNullOrEmpty(partLocalVariableName, "partLocalVariableName");
            Requires.NotNull(exportDefinition, "exportDefinition");

            if (member == null)
            {
                return partLocalVariableName;
            }

            string valueFactoryExpression;
            if (IsPublic(member))
            {
                string memberExpression = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Value.{1}",
                    partLocalVariableName,
                    member.Name);
                switch (member.MemberType)
                {
                    case MemberTypes.Method:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "new {0}({1})",
                            GetTypeName(exportDefinition.Contract.Type),
                            memberExpression);
                        break;
                    case MemberTypes.Field:
                    case MemberTypes.Property:
                        valueFactoryExpression = memberExpression;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
            else
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Method:
                        //MethodInfo mi;
                        //(Func<int>)mi.CreateDelegate(typeof(Func<int>), partLocalVariableName)
                        throw new NotImplementedException();
                    case MemberTypes.Field:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "({0}){1}.GetValue({2}.Value)",
                            GetTypeName(((FieldInfo)member).FieldType),
                            GetFieldInfoExpression((FieldInfo)member),
                            partLocalVariableName);
                        break;
                    case MemberTypes.Property:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "({0}){1}.Invoke({2}.Value, new object[0])",
                            GetTypeName(((PropertyInfo)member).PropertyType),
                            GetMethodInfoExpression(((PropertyInfo)member).GetGetMethod(true)),
                            partLocalVariableName);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "new LazyPart<{0}>(() => {1})",
                GetTypeName(exportDefinition.Contract.Type),
                valueFactoryExpression);
        }

        private static string Quote(string value)
        {
            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsPublic(MemberInfo memberInfo, bool setter = false)
        {
            Requires.NotNull(memberInfo, "memberInfo");
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Constructor:
                    return ((ConstructorInfo)memberInfo).IsPublic;
                case MemberTypes.Field:
                    return ((FieldInfo)memberInfo).IsPublic;
                case MemberTypes.Method:
                    return ((MethodInfo)memberInfo).IsPublic;
                case MemberTypes.Property:
                    var property = (PropertyInfo)memberInfo;
                    var method = setter ? property.GetSetMethod(true) : property.GetGetMethod(true);
                    return IsPublic(method);
                default:
                    throw new NotSupportedException();
            }
        }

        private IDisposable Indent(int count = 1, bool withBraces = false)
        {
            if (withBraces)
            {
                this.WriteLine("{");
            }

            this.PushIndent(new string(' ', count * 4));

            return new DisposableWithAction(delegate
            {
                this.PopIndent();
                if (withBraces)
                {
                    this.WriteLine("}");
                }
            });
        }
    }
}
