namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    partial class CompositionTemplateFactory
    {
        private const string InstantiatedPartLocalVarName = "result";

        private readonly HashSet<Assembly> relevantAssemblies = new HashSet<Assembly>();

        private readonly HashSet<Type> relevantEmbeddedTypes = new HashSet<Type>();

        /// <summary>
        /// A collection of symbols defined at the level of the generated class.
        /// </summary>
        /// <remarks>
        /// This is useful to ensure that any generated symbol is unique.
        /// </remarks>
        private readonly HashSet<string> classSymbols = new HashSet<string>();

        /// <summary>
        /// A set of local variable names that have already been used in the currently generating part factory method.
        /// </summary>
        private readonly HashSet<string> localSymbols = new HashSet<string>();

        /// <summary>
        /// A lookup table of arbitrary objects to the symbols that have been reserved for them.
        /// </summary>
        private readonly Dictionary<object, string> reservedSymbols = new Dictionary<object, string>();

        public CompositionConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets the relevant assemblies that must be referenced when compiling the generated code.
        /// </summary>
        public ISet<Assembly> RelevantAssemblies
        {
            get { return this.relevantAssemblies; }
        }

        /// <summary>
        /// Gets the relevant embedded types that must be discoverable when compiling the generated code.
        /// </summary>
        public ISet<Type> RelevantEmbeddedTypes
        {
            get { return this.relevantEmbeddedTypes; }
        }

        private IDisposable EmitMemberAssignment(ImportDefinitionBinding import)
        {
            Requires.NotNull(import, "import");

            var importingField = import.ImportingMember as FieldInfo;
            var importingProperty = import.ImportingMember as PropertyInfo;
            Assumes.True(importingField != null || importingProperty != null);

            string tail;
            if (IsPublic(import.ImportingMember, import.ComposablePartType, setter: true))
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
                    "{0}.ManifestModule.ResolveField({1}/*{2}*/)",
                    this.GetAssemblyExpression(fieldInfo.DeclaringType.Assembly),
                    fieldInfo.MetadataToken,
                    GetTypeName(fieldInfo.DeclaringType, evenNonPublic: true) + "." + fieldInfo.Name);
            }
        }

        private string GetMethodInfoExpression(MethodInfo methodInfo)
        {
            Requires.NotNull(methodInfo, "methodInfo");

            if (methodInfo.DeclaringType.IsGenericType)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "((MethodInfo)MethodInfo.GetMethodFromHandle({0}.ManifestModule.ResolveMethod({1}/*{3}*/).MethodHandle, {2}))",
                    this.GetAssemblyExpression(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken,
                    this.GetClosedGenericTypeHandleExpression(methodInfo.DeclaringType),
                    GetTypeName(methodInfo.DeclaringType, evenNonPublic: true) + "." + methodInfo.Name);
            }
            else
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "((MethodInfo){0}.ManifestModule.ResolveMethod({1}/*{2}*/))",
                    this.GetAssemblyExpression(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken,
                    GetTypeName(methodInfo.DeclaringType, evenNonPublic: true) + "." + methodInfo.Name);
            }
        }

        private string GetClosedGenericTypeExpression(Type type)
        {
            Requires.NotNull(type, "type");
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.ManifestModule.ResolveType({1}/*{3}*/).MakeGenericType({2})",
                this.GetAssemblyExpression(type.Assembly),
                type.GetGenericTypeDefinition().MetadataToken,
                string.Join(", ", type.GetGenericArguments().Select(t => t.IsGenericType && t.ContainsGenericParameters ? GetClosedGenericTypeExpression(t) : GetTypeExpression(t))),
                type.ContainsGenericParameters ? "incomplete" : this.GetTypeName(type, evenNonPublic: true));
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

        private void EmitImportSatisfyingAssignment(KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExport)
        {
            Requires.Argument(satisfyingExport.Key.ImportingMember != null, "satisfyingExport", "No member to satisfy.");
            var import = satisfyingExport.Key;
            var importingMember = satisfyingExport.Key.ImportingMember;
            var exports = satisfyingExport.Value;

            string expression = GetImportSatisfyingExpression(import, exports);
            using (this.EmitMemberAssignment(import))
            {
                this.Write(expression);
            }
        }

        private string GetImportSatisfyingExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports)
        {
            if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                Type enumerableOfTType = typeof(IEnumerable<>).MakeGenericType(import.ImportingSiteTypeWithoutCollection);
                if (import.ImportingSiteType.IsArray || import.ImportingSiteType.IsEquivalentTo(enumerableOfTType))
                {
                    return this.GetSatisfyImportManyArrayExpression(import, exports);
                }
                else
                {
                    return this.GetSatisfyImportManyCollectionExpression(import, exports);
                }
            }
            else if (exports.Any())
            {
                return this.GetValueFactoryExpression(import, exports.Single());
            }
            else
            {
                if (IsPublic(import.ImportingSiteType))
                {
                    return string.Format(CultureInfo.InvariantCulture, "default({0})", GetTypeName(import.ImportingSiteType));
                }
                else if (import.ImportingSiteType.IsValueType)
                {
                    // It's a non-public struct. We have to construct its default value by hand.
                    return string.Format(CultureInfo.InvariantCulture, "Activator.CreateInstance({0})", GetTypeExpression(import.ImportingSiteType));
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "({0})null", GetTypeName(import.ImportingSiteType));
                }
            }
        }

        private string GetSatisfyImportManyArrayExpression(ImportDefinitionBinding import, IEnumerable<ExportDefinitionBinding> exports)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");

            string localVarName = ReserveLocalVarName(import.ImportingMember != null ? import.ImportingMember.Name : "tmp");
            this.Write("var {0} = ", localVarName);
            if (IsPublic(import.ImportingSiteType, true))
            {
                this.WriteLine("new {0}[]", GetTypeName(import.ImportingSiteTypeWithoutCollection));
                this.WriteLine("{");
                using (Indent())
                {
                    foreach (var export in exports)
                    {
                        this.WriteLine("{0},", this.GetValueFactoryExpression(import, export));
                    }
                }

                this.WriteLine("};");
            }
            else
            {
                // This will require a multi-statement construction of the array.
                this.WriteLine("Array.CreateInstance({0}, {1});", this.GetTypeExpression(import.ImportingSiteTypeWithoutCollection), exports.Count());
                int arrayIndex = 0;
                foreach (var export in exports)
                {
                    this.WriteLine(
                        "{0}.SetValue({1}, {2});",
                        localVarName,
                        this.GetValueFactoryExpression(import, export),
                        arrayIndex++);
                }
            }

            return localVarName;
        }

        private string EmitOpenGenericExportCollection(ImportDefinition importDefinition, IEnumerable<ExportDefinitionBinding> exports)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(exports, "exports");

            const string localVarName = "temp";
            this.WriteLine("Array {0} = Array.CreateInstance(typeof(ILazy<>).MakeGenericType(compositionContract.Type), {1});", localVarName, exports.Count().ToString(CultureInfo.InvariantCulture));

            int index = 0;
            foreach (var export in exports)
            {
                this.WriteLine("{0}.SetValue({1}, {2});", localVarName, GetPartOrMemberLazy(export), index++);
            }

            return localVarName;
        }

        private string GetSatisfyImportManyCollectionExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports)
        {
            Requires.NotNull(import, "import");
            var importDefinition = import.ImportDefinition;
            Type elementType = import.ImportingSiteTypeWithoutCollection;
            Type listType = typeof(List<>).MakeGenericType(elementType);
            bool stronglyTypedCollection = IsPublic(elementType, true);
            Type icollectionType = typeof(ICollection<>).MakeGenericType(elementType);
            string importManyLocalVarTypeName = stronglyTypedCollection ? GetTypeName(icollectionType) : "object";
            string tempVarName = ReserveLocalVarName(import.ImportingMember.Name);

            // Casting the collection to ICollection<T> instead of the concrete type guarantees
            // that we'll be able to call Add(T) and Clear() on it even if the type is NonPublic
            // or its methods are explicit interface implementations.
            if (import.ImportingMember is FieldInfo)
            {
                this.WriteLine("var {0} = ({3}){1}.GetValue({2});", tempVarName, GetFieldInfoExpression((FieldInfo)import.ImportingMember), InstantiatedPartLocalVarName, importManyLocalVarTypeName);
            }
            else
            {
                this.WriteLine("var {0} = ({3}){1}.Invoke({2}, new object[0]);", tempVarName, GetMethodInfoExpression(((PropertyInfo)import.ImportingMember).GetGetMethod(true)), InstantiatedPartLocalVarName, importManyLocalVarTypeName);
            }

            this.WriteLine("if ({0} == null)", tempVarName);
            using (Indent(withBraces: true))
            {
                if (PartDiscovery.IsImportManyCollectionTypeCreateable(import))
                {
                    if (import.ImportingSiteType.IsAssignableFrom(listType))
                    {
                        if (stronglyTypedCollection)
                        {
                            string elementTypeName = GetTypeName(elementType);
                            this.WriteLine("{0} = new List<{1}>();", tempVarName, elementTypeName);
                        }
                        else
                        {
                            this.Write("{0} = ", tempVarName);
                            EmitConstructorInvocationExpression(typeof(List<>).MakeGenericType(elementType).GetConstructor(new Type[0]), alwaysUseReflection: true, skipCast: true).Dispose();
                            this.WriteLine(";");
                        }
                    }
                    else
                    {
                        this.Write("{0} = ({1})(", tempVarName, importManyLocalVarTypeName);
                        using (this.EmitConstructorInvocationExpression(import.ImportingSiteType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null)))
                        {
                            // no arguments to constructor
                        }

                        this.WriteLine(");");
                    }

                    if (import.ImportingMember is FieldInfo)
                    {
                        this.WriteLine("{0}.SetValue({1}, {2});", GetFieldInfoExpression((FieldInfo)import.ImportingMember), InstantiatedPartLocalVarName, tempVarName);
                    }
                    else
                    {
                        this.WriteLine("{0}.Invoke({1}, new object[] {{ {2} }});", GetMethodInfoExpression(((PropertyInfo)import.ImportingMember).GetSetMethod(true)), InstantiatedPartLocalVarName, tempVarName);
                    }
                }
                else
                {
                    this.WriteLine(
                        "throw new InvalidOperationException(\"The {0}.{1} collection must be instantiated by the importing constructor.\");",
                        import.ComposablePartType.Name,
                        import.ImportingMember.Name);
                }
            }

            this.WriteLine("else");
            using (Indent(withBraces: true))
            {
                if (stronglyTypedCollection)
                {
                    this.WriteLine("{0}.Clear();", tempVarName);
                }
                else
                {
                    this.WriteLine(
                        "{0}.Invoke({1}, new object[0]);",
                        GetMethodInfoExpression(icollectionType.GetMethod("Clear")),
                        tempVarName);
                }
            }

            this.WriteLine(string.Empty);

            foreach (var export in exports)
            {
                if (stronglyTypedCollection)
                {
                    this.WriteLine("{0}.Add({1});", tempVarName, this.GetValueFactoryExpression(import, export));
                }
                else
                {
                    this.WriteLine(
                        "{0}.Invoke({1}, new object[] {{ {2} }});",
                        GetMethodInfoExpression(icollectionType.GetMethod("Add")),
                        tempVarName,
                        this.GetValueFactoryExpression(import, export));
                }
            }

            return string.Format(CultureInfo.InvariantCulture, "({0}){1}", GetTypeName(import.ImportingSiteType), tempVarName);
        }

        private string GetValueFactoryExpression(ImportDefinitionBinding import, ExportDefinitionBinding export)
        {
            var writer = new StringWriter();

            using (this.ValueFactoryWrapper(import, export, writer))
            {
                if (export.PartDefinition.Type.IsEquivalentTo(import.ComposablePartType) && !import.IsExportFactory)
                {
                    // The part is importing itself. So just assign it directly.
                    writer.Write(InstantiatedPartLocalVarName);
                }
                else if (export.IsStaticExport)
                {
                    if (IsPublic(export.ExportingMember, export.PartDefinition.Type))
                    {
                        writer.Write(GetTypeName(export.PartDefinition.Type));
                    }
                    else
                    {
                        // What we write here will be emitted as the argument to a reflection GetValue method call.
                        writer.Write("null");
                    }
                }
                else
                {
                    string provisionalSharedObjectsExpression = import.IsExportFactory
                        ? "new Dictionary<Type, object>()"
                        : "provisionalSharedObjects";
                    bool nonSharedInstanceRequired = PartCreationPolicyConstraint.IsNonSharedInstanceRequired(import.ImportDefinition);
                    if (import.ComposablePartType == null && export.PartDefinition.Type.IsGenericType)
                    {
                        // We're constructing an open generic export using generic type args that are only known at runtime.
                        const string TypeArgsPlaceholder = "***PLACEHOLDER***";
                        string expressionTemplate = GetGenericPartFactoryMethodInvokeExpression(
                            export.PartDefinition,
                            TypeArgsPlaceholder,
                            provisionalSharedObjectsExpression,
                            nonSharedInstanceRequired);
                        string expression = expressionTemplate.Replace(TypeArgsPlaceholder, "(Type[])importDefinition.Metadata[\"" + CompositionConstants.GenericParametersMetadataName + "\"]");
                        writer.Write("((ILazy<object>)({0}))", expression);
                    }
                    else
                    {
                        var genericTypeArgs = export.PartDefinition.Type.GetTypeInfo().IsGenericType
                            ? (IReadOnlyList<Type>)import.ImportDefinition.Metadata.GetValueOrDefault(CompositionConstants.GenericParametersMetadataName, ImmutableList<Type>.Empty)
                            : Enumerable.Empty<Type>();

                        if (genericTypeArgs.All(arg => IsPublic(arg, true)))
                        {
                            writer.Write("{0}(", GetPartFactoryMethodName(export.PartDefinition, genericTypeArgs.Select(GetTypeName).ToArray()));
                            writer.Write(provisionalSharedObjectsExpression);
                            writer.Write(", nonSharedInstanceRequired: {0})", nonSharedInstanceRequired ? "true" : "false");
                        }
                        else
                        {
                            string expression = GetGenericPartFactoryMethodInvokeExpression(
                                export.PartDefinition,
                                string.Join(", ", genericTypeArgs.Select(t => GetTypeExpression(t))),
                                provisionalSharedObjectsExpression,
                                nonSharedInstanceRequired);
                            writer.Write("((ILazy<object>)({0}))", expression);
                        }
                    }
                }
            }

            return writer.ToString();
        }

        private string GetExportMetadata(ExportDefinitionBinding export)
        {
            var builder = new StringBuilder();
            builder.Append("new Dictionary<string, object> {");
            foreach (var metadatum in export.ExportDefinition.Metadata)
            {
                builder.AppendFormat(" {{ \"{0}\", {1} }}, ", metadatum.Key, GetExportMetadataValueExpression(metadatum.Value));
            }
            builder.Append("}.ToImmutableDictionary()");
            return builder.ToString();
        }

        private string GetExportMetadataValueExpression(object value)
        {
            if (value == null)
            {
                return "null";
            }

            Type valueType = value.GetType();
            if (value is string)
            {
                return "\"" + value + "\"";
            }
            else if (typeof(char).IsEquivalentTo(valueType))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}'",
                    (char)value == '\'' ? "\\'" : value);
            }
            else if (typeof(bool).IsEquivalentTo(valueType))
            {
                return (bool)value ? "true" : "false";
            }
            else if (valueType.IsEquivalentTo(typeof(double)) && (double)value == double.MaxValue)
            {
                return "double.MaxValue";
            }
            else if (valueType.IsEquivalentTo(typeof(double)) && (double)value == double.MinValue)
            {
                return "double.MinValue";
            }
            else if (valueType.IsEquivalentTo(typeof(float)) && (float)value == float.MaxValue)
            {
                return "float.MaxValue";
            }
            else if (valueType.IsEquivalentTo(typeof(float)) && (float)value == float.MinValue)
            {
                return "float.MinValue";
            }
            else if (valueType.IsPrimitive)
            {
                return string.Format(CultureInfo.InvariantCulture, "({0})({1})", GetTypeName(valueType), value);
            }
            else if (valueType.IsEquivalentTo(typeof(Guid)))
            {
                return string.Format(CultureInfo.InvariantCulture, "Guid.Parse(\"{0}\")", value);
            }
            else if (valueType.IsEnum)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({0}){1}",
                    GetTypeName(valueType),
                    Convert.ChangeType(value, Enum.GetUnderlyingType(valueType)));
            }
            else if (typeof(Type).IsAssignableFrom(valueType))
            {
                // assumeNonPublic=true because typeof() would result in the JIT compiler
                // loading the assembly containing the type itself even before this
                // part is activated.
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "({1}){0}",
                    GetTypeExpression((Type)value, assumeNonPublic: true),
                    GetTypeName(valueType)); // Cast as TypeInfo to avoid some compilation errors.
            }
            else if (valueType.IsArray)
            {
                var builder = new StringBuilder();
                builder.AppendFormat("new {0}[] {{ ", GetTypeName(valueType.GetElementType()));
                bool firstValue = true;
                foreach (object element in (Array)value)
                {
                    if (!firstValue)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(GetExportMetadataValueExpression(element));
                    firstValue = false;
                }

                builder.Append("}");
                return builder.ToString();
            }

            throw new NotSupportedException();
        }

        private IDisposable EmitConstructorInvocationExpression(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            return this.EmitConstructorInvocationExpression(partDefinition.ImportingConstructorInfo);
        }

        private IDisposable EmitConstructorInvocationExpression(ConstructorInfo ctor, bool alwaysUseReflection = false, bool skipCast = false, TextWriter writer = null)
        {
            Requires.NotNull(ctor, "ctor");

            writer = writer ?? new SelfTextWriter(this);
            bool publicCtor = !alwaysUseReflection && IsPublic(ctor, ctor.DeclaringType);
            if (publicCtor)
            {
                writer.Write("new {0}(", GetTypeName(ctor.DeclaringType));
            }
            else
            {
                string assemblyExpression = GetAssemblyExpression(ctor.DeclaringType.Assembly);
                string ctorExpression;
                if (ctor.DeclaringType.IsGenericType)
                {
                    ctorExpression = string.Format(
                        CultureInfo.InvariantCulture,
                        "(ConstructorInfo)MethodInfo.GetMethodFromHandle({2}.ManifestModule.ResolveMethod({0}/*{3}*/).MethodHandle, {1})",
                        ctor.MetadataToken,
                        this.GetClosedGenericTypeHandleExpression(ctor.DeclaringType),
                        GetAssemblyExpression(ctor.DeclaringType.Assembly),
                        GetTypeName(ctor.DeclaringType, evenNonPublic: true) + "." + ctor.Name);
                }
                else
                {
                    ctorExpression = string.Format(
                        CultureInfo.InvariantCulture,
                        "(ConstructorInfo){0}.ManifestModule.ResolveMethod({1}/*{2}*/)",
                        GetAssemblyExpression(ctor.DeclaringType.Assembly),
                        ctor.MetadataToken,
                        GetTypeName(ctor.DeclaringType, evenNonPublic: true) + "." + ctor.Name);
                }

                writer.Write("({0})({1}).Invoke(new object[] {{", skipCast ? "object" : GetTypeName(ctor.DeclaringType), ctorExpression);
            }
            var indent = this.Indent(writer: writer);

            return new DisposableWithAction(delegate
            {
                indent.Dispose();
                if (publicCtor)
                {
                    writer.Write(")");
                }
                else
                {
                    writer.Write(" })");
                }
            });
        }

        private void EmitInstantiatePart(ComposedPart part)
        {
            localSymbols.Clear();

            if (!part.Definition.IsInstantiable)
            {
                this.WriteLine("return CannotInstantiatePartWithNoImportingConstructor();");
                return;
            }

            this.WriteLine("{0} {1};", GetTypeName(part.Definition.Type), InstantiatedPartLocalVarName);
            using (Indent(withBraces: true))
            {
                int importingConstructorArgIndex = 0;
                var importingConstructorArgNames = new string[part.Definition.ImportingConstructor.Count];
                foreach (var pair in part.GetImportingConstructorImports())
                {
                    this.WriteLine(
                        "var {0} = {1};",
                        importingConstructorArgNames[importingConstructorArgIndex++] = ReserveLocalVarName("arg"),
                        this.GetImportSatisfyingExpression(pair.Key, pair.Value));
                }

                this.Write("{0} = ", InstantiatedPartLocalVarName);
                using (this.EmitConstructorInvocationExpression(part.Definition))
                {
                    this.Write(string.Join(", ", importingConstructorArgNames));
                }

                this.WriteLine(";");
            }

            if (typeof(IDisposable).IsAssignableFrom(part.Definition.Type))
            {
                this.WriteLine("this.TrackDisposableValue((IDisposable){0});", InstantiatedPartLocalVarName);
            }

            if (part.Definition.IsShared)
            {
                this.WriteLine("provisionalSharedObjects.Add(partType, {0});", InstantiatedPartLocalVarName);
            }

            foreach (var satisfyingExport in part.SatisfyingExports.Where(i => i.Key.ImportingMember != null))
            {
                this.EmitImportSatisfyingAssignment(satisfyingExport);
            }

            if (part.Definition.OnImportsSatisfied != null)
            {
                if (part.Definition.OnImportsSatisfied.DeclaringType.IsInterface)
                {
                    this.WriteLine("var onImportsSatisfiedInterface = ({0}){1};", part.Definition.OnImportsSatisfied.DeclaringType.FullName, InstantiatedPartLocalVarName);
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
                let metadataType = importAndExports.Key.MetadataType
                where metadataType != null && metadataType.IsInterface && metadataType != typeof(IDictionary<string, object>)
                select metadataType);

            return set;
        }

        private IDisposable ValueFactoryWrapper(ImportDefinitionBinding import, ExportDefinitionBinding export, TextWriter writer)
        {
            var importDefinition = import.ImportDefinition;

            LazyConstructionResult closeLazy = null;
            bool closeParenthesis = false;
            if (import.IsLazyConcreteType || (export.ExportingMember != null && import.IsLazy))
            {
                if (IsPublic(import.ImportingSiteTypeWithoutCollection, true) && export.ExportedValueType.IsEquivalentTo(import.ImportingSiteElementType))
                {
                    string lazyTypeName = GetTypeName(LazyPart.FromLazy(import.ImportingSiteTypeWithoutCollection));
                    if (import.MetadataType == null && export.ExportedValueType.IsEquivalentTo(export.PartDefinition.Type) && import.ComposablePartType != export.PartDefinition.Type)
                    {
                        writer.Write("({0})", lazyTypeName);
                    }
                    else
                    {
                        writer.Write("new {0}(() => ", lazyTypeName);
                        closeParenthesis = true;
                    }
                }
                else
                {
                    closeLazy = this.EmitLazyConstruction(import.ImportingSiteElementType, import.MetadataType, writer);
                }
            }
            else if (import.IsExportFactory)
            {
                var exportFactoryEmitClose = this.EmitExportFactoryConstruction(import, writer);
                writer.Write("() => { var temp = ");

                if (importDefinition.ExportFactorySharingBoundaries.Count > 0)
                {
                    writer.Write("new CompiledExportProvider(this, new [] { ");
                    writer.Write(string.Join(", ", importDefinition.ExportFactorySharingBoundaries.Select(Quote)));
                    writer.Write(" }).");
                }

                return new DisposableWithAction(delegate
                {
                    writer.Write(".Value; return ");
                    using (this.EmitExportFactoryTupleConstruction(import.ImportingSiteElementType, "temp", writer))
                    {
                        writer.Write("() => { ");
                        if (typeof(IDisposable).IsAssignableFrom(export.PartDefinition.Type))
                        {
                            writer.Write("((IDisposable)temp).Dispose(); ");
                        }

                        writer.Write("}");
                    }

                    writer.Write("; }");
                    this.WriteExportMetadataReference(export, import, writer);
                    exportFactoryEmitClose.Dispose();
                });
            }
            else if (!IsPublic(export.PartDefinition.Type) && IsPublic(import.ImportingSiteTypeWithoutCollection, true))
            {
                writer.Write("({0})", GetTypeName(import.ImportingSiteTypeWithoutCollection));
            }

            if (export.ExportingMember != null)
            {
                if (IsPublic(export.ExportingMember, export.PartDefinition.Type))
                {
                    switch (export.ExportingMember.MemberType)
                    {
                        case MemberTypes.Method:
                            closeParenthesis = true;
                            var methodInfo = (MethodInfo)export.ExportingMember;
                            writer.Write("new {0}(", GetTypeName(typeof(Delegate).IsAssignableFrom(import.ImportingSiteElementType) ? import.ImportingSiteElementType : export.ExportedValueType));
                            break;
                    }
                }
                else
                {
                    closeParenthesis = true;

                    switch (export.ExportingMember.MemberType)
                    {
                        case MemberTypes.Field:
                            writer.Write(
                                "({0}){1}.GetValue(",
                                GetTypeName(import.ImportingSiteElementType),
                                GetFieldInfoExpression((FieldInfo)export.ExportingMember));
                            break;
                        case MemberTypes.Method:
                            writer.Write(
                                "({0}){1}.CreateDelegate({2}, ",
                                GetTypeName(import.ImportingSiteElementType),
                                GetMethodInfoExpression((MethodInfo)export.ExportingMember),
                                GetTypeExpression(typeof(Delegate).IsAssignableFrom(import.ImportingSiteElementType) ? import.ImportingSiteElementType : export.ExportedValueType));
                            break;
                        case MemberTypes.Property:
                            writer.Write(
                                "({0}){1}.Invoke(",
                                GetTypeName(import.ImportingSiteElementType),
                                GetMethodInfoExpression(((PropertyInfo)export.ExportingMember).GetGetMethod(true)));
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }

            return new DisposableWithAction(() =>
            {
                string memberModifier = string.Empty;
                if (export.ExportingMember != null)
                {
                    if (IsPublic(export.ExportingMember, export.PartDefinition.Type))
                    {
                        memberModifier = "." + export.ExportingMember.Name;
                        switch (export.ExportingMember.MemberType)
                        {
                            case MemberTypes.Method:
                                memberModifier += ")";
                                break;
                        }
                    }
                    else
                    {
                        switch (export.ExportingMember.MemberType)
                        {
                            case MemberTypes.Field:
                                memberModifier = ")";
                                break;
                            case MemberTypes.Property:
                                memberModifier = ", new object[0])";
                                break;
                            case MemberTypes.Method:
                                memberModifier = ")";
                                break;
                        }
                    }
                }

                string memberAccessor = memberModifier;
                if ((!export.PartDefinition.Type.IsEquivalentTo(import.ComposablePartType) || import.IsExportFactory) && !export.IsStaticExport)
                {
                    memberAccessor = ".Value" + memberAccessor;
                }

                if (import.IsLazy)
                {
                    if (import.MetadataType != null)
                    {
                        writer.Write(memberAccessor);
                        if (closeLazy != null)
                        {
                            closeLazy.OnBeforeWriteMetadata();
                        }

                        this.WriteExportMetadataReference(export, import, writer);
                    }
                    else if (import.IsLazyConcreteType && !export.ExportedValueType.IsEquivalentTo(export.PartDefinition.Type))
                    {
                        writer.Write(memberAccessor);
                    }
                    else if (closeLazy != null || (export.ExportingMember != null && import.IsLazy))
                    {
                        writer.Write(memberAccessor);
                    }

                    if (closeLazy != null)
                    {
                        closeLazy.Dispose();
                    }
                    else if (closeParenthesis)
                    {
                        writer.Write(")");
                    }
                }
                else if (import.ComposablePartType != export.PartDefinition.Type)
                {
                    writer.Write(memberAccessor);
                }
            });
        }

        private void EmitGetExportsReturnExpression(IGrouping<string, ExportDefinitionBinding> exports)
        {
            using (Indent(4))
            {
                ////if (contract.Type.IsGenericTypeDefinition)
                ////{
                ////    string localVarName = this.EmitOpenGenericExportCollection(WrapContractAsImportDefinition(contract), exports);
                ////    this.WriteLine("return (IEnumerable<object>){0};", localVarName);
                ////}
                ////else
                {
                    var synthesizedImport = new ImportDefinitionBinding(
                        new ImportDefinition(exports.Key, ImportCardinality.ZeroOrMore, ImmutableDictionary<string, object>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty),
                        typeof(object));

                    this.WriteLine("return new Export[]");
                    this.WriteLine("{");
                    using (Indent())
                    {
                        foreach (var export in exports)
                        {
                            this.WriteLine(
                                "new Export(importDefinition.ContractName, {1}, () => ({0}).Value),",
                                this.GetValueFactoryExpression(synthesizedImport, export),
                                GetExportMetadata(export));
                        }
                    }

                    this.Write("}");
                    this.WriteLine(";");
                }
            }
        }

        private void WriteExportMetadataReference(ExportDefinitionBinding export, ImportDefinitionBinding import, TextWriter writer)
        {
            if (import.MetadataType != null)
            {
                writer.Write(", ");

                if (import.MetadataType == typeof(IDictionary<string, object>))
                {
                    writer.Write(GetExportMetadata(export));
                }
                else if (import.MetadataType.IsInterface)
                {
                    writer.Write("new {0}(", GetClassNameForMetadataView(import.MetadataType));
                    writer.Write(GetExportMetadata(export));
                    writer.Write(")");
                }
                else
                {
                    using (EmitConstructorInvocationExpression(import.MetadataType.GetConstructor(new Type[] { typeof(IDictionary<string, object>) }), writer: writer))
                    {
                        writer.Write(GetExportMetadata(export));
                    }
                }
            }
        }

        private IEnumerable<IGrouping<string, ExportDefinitionBinding>> ExportsByContract
        {
            get
            {
                return
                    from part in this.Configuration.Parts
                    from exportingMemberAndDefinition in part.Definition.ExportDefinitions
                    let export = new ExportDefinitionBinding(exportingMemberAndDefinition.Value, part.Definition, exportingMemberAndDefinition.Key)
                    where part.Definition.IsInstantiable || part.Definition.Equals(ExportProvider.ExportProviderPartDefinition) // normally they must be instantiable, but we have one special case.
                    group export by export.ExportDefinition.ContractName into exportsByContract
                    select exportsByContract;
            }
        }

        private string GetTypeName(Type type)
        {
            return this.GetTypeName(type, false, false);
        }

        private string GetTypeName(Type type, bool genericTypeDefinition = false, bool evenNonPublic = false)
        {
            return ReflectionHelpers.GetTypeName(type, genericTypeDefinition, evenNonPublic, this.relevantAssemblies, this.relevantEmbeddedTypes);
        }

        /// <summary>
        /// Gets a C# expression that evaluates to a System.Type instance for the specified type.
        /// </summary>
        private string GetTypeExpression(Type type, bool genericTypeDefinition = false, bool assumeNonPublic = false)
        {
            Requires.NotNull(type, "type");

            if (!assumeNonPublic && IsPublic(type, true) && !type.IsEmbeddedType()) // embedded types need to be matched to their receiving assembly
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "typeof({0})",
                    this.GetTypeName(type, genericTypeDefinition));
            }
            else
            {
                var targetType = (genericTypeDefinition && type.IsGenericType) ? type.GetGenericTypeDefinition() : type;
                var expression = new StringBuilder();
                expression.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "Type.GetType({0})",
                    Quote(targetType.AssemblyQualifiedName));
                if (!genericTypeDefinition && targetType.IsGenericTypeDefinition)
                {
                    // Concatenate on the generic type arguments if the caller didn't explicitly want the generic type definition.
                    // Note that the type itself may be a generic type definition, in which case the concatenated types might be
                    // T1, T2. That's fine. In fact that's what we want because it causes the types from the caller's caller to
                    // propagate.
                    expression.Append(".MakeGenericType(");
                    foreach (Type typeArg in targetType.GetGenericArguments())
                    {
                        expression.Append(this.GetTypeExpression(typeArg, false));
                        expression.Append(", ");
                    }

                    expression.Length -= 2;
                    expression.Append(")");
                }

                return expression.ToString();
            }
        }

        private static bool IsPublic(Type type, bool checkGenericTypeArgs = false)
        {
            return ReflectionHelpers.IsPublic(type, checkGenericTypeArgs);
        }

        private string GetClassNameForMetadataView(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            return ReserveClassSymbolName(
                metadataView.IsInterface ? "ClassFor" + metadataView.Name : this.GetTypeName(metadataView),
                metadataView);
        }

        private string GetValueOrDefaultForMetadataView(PropertyInfo property, string sourceVarName)
        {
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0})({1}.ContainsKey(""{2}"") ? {1}[""{2}""]{4} : {3})",
                    this.GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name,
                    GetExportMetadataValueExpression(defaultValueAttribute.Value),
                    property.PropertyType.IsValueType ? string.Empty : (" as " + this.GetTypeName(property.PropertyType)));
            }
            else
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0}){1}[""{2}""]",
                    this.GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name);
            }
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

            string name = "GetOrCreate" + ReflectionHelpers.ReplaceBackTickWithTypeArgs(part.Id, typeArguments);
            return name;
        }

        private static string GetPartFactoryMethodInvokeExpression(
            ComposablePartDefinition part,
            string typeArgsParamsArrayExpression,
            string provisionalSharedObjectsExpression,
            bool nonSharedInstanceRequired)
        {
            if (part.Type.IsGenericType)
            {
                return GetGenericPartFactoryMethodInvokeExpression(
                    part,
                    typeArgsParamsArrayExpression,
                    provisionalSharedObjectsExpression,
                    false);
            }
            else
            {
                return "this." + GetPartFactoryMethodName(part) + "(" + provisionalSharedObjectsExpression + ", " + (nonSharedInstanceRequired ? "true" : "false") + ")";
            }
        }

        private static string GetGenericPartFactoryMethodInfoExpression(ComposablePartDefinition part, string typeArgsParamsArrayExpression)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "this.GetMethodWithArity(\"{0}\", {1})"
                + ".MakeGenericMethod({2})",
                GetPartFactoryMethodNameNoTypeArgs(part),
                part.Type.GetGenericArguments().Length,
                typeArgsParamsArrayExpression);
        }

        private static string GetGenericPartFactoryMethodInvokeExpression(
            ComposablePartDefinition part,
            string typeArgsParamsArrayExpression,
            string provisionalSharedObjectsExpression,
            bool nonSharedInstanceRequired)
        {
            return GetGenericPartFactoryMethodInfoExpression(part, typeArgsParamsArrayExpression) +
                ".Invoke(this, new object[] { " + provisionalSharedObjectsExpression + ", /* nonSharedInstanceRequired: */ " + (nonSharedInstanceRequired ? "true" : "false") + " })";
        }

        private string GetPartOrMemberLazy(ExportDefinitionBinding export)
        {
            Requires.NotNull(export, "export");

            MemberInfo member = export.ExportingMember;
            ExportDefinition exportDefinition = export.ExportDefinition;

            string partExpression = GetPartFactoryMethodInvokeExpression(
                export.PartDefinition,
                "compositionContract.Type.GetGenericArguments()",
                "provisionalSharedObjects",
                false);

            if (member == null)
            {
                return partExpression;
            }

            string valueFactoryExpression;
            if (IsPublic(member, export.PartDefinition.Type))
            {
                string memberExpression = string.Format(
                    CultureInfo.InvariantCulture,
                    "({0}).Value.{1}",
                    partExpression,
                    member.Name);
                switch (member.MemberType)
                {
                    case MemberTypes.Method:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "new {0}({1})",
                            GetTypeName(export.ExportedValueType),
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
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "({0}){1}.CreateDelegate({3}, ({2}).Value)",
                            GetTypeName(export.ExportedValueType),
                            GetMethodInfoExpression((MethodInfo)member),
                            partExpression,
                            GetTypeExpression(export.ExportedValueType));
                        break;
                    case MemberTypes.Field:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "({0}){1}.GetValue(({2}).Value)",
                            GetTypeName(((FieldInfo)member).FieldType),
                            GetFieldInfoExpression((FieldInfo)member),
                            partExpression);
                        break;
                    case MemberTypes.Property:
                        valueFactoryExpression = string.Format(
                            CultureInfo.InvariantCulture,
                            "({0}){1}.Invoke(({2}).Value, new object[0])",
                            GetTypeName(((PropertyInfo)member).PropertyType),
                            GetMethodInfoExpression(((PropertyInfo)member).GetGetMethod(true)),
                            partExpression);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "new LazyPart<{0}>(() => {1})",
                GetTypeName(export.ExportedValueType),
                valueFactoryExpression);
        }

        private static string Quote(string value)
        {
            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool IsPublic(MemberInfo memberInfo, Type reflectedType, bool setter = false)
        {
            Requires.NotNull(memberInfo, "memberInfo");
            Requires.NotNull(reflectedType, "reflectedType");
            Requires.Argument(memberInfo.ReflectedType.IsAssignableFrom(reflectedType), "reflectedType", "Type must be the one that defines memberInfo or a derived type.");

            if (!IsPublic(reflectedType, true))
            {
                return false;
            }

            var obsoleteAttribute = memberInfo.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute != null && obsoleteAttribute.IsError)
            {
                // It would generate a compile error if we referenced this member directly, so consider it non-public.
                return false;
            }

            switch (memberInfo.MemberType)
            {
                case MemberTypes.Constructor:
                    return ((ConstructorInfo)memberInfo).IsPublic;
                case MemberTypes.Field:
                    var fieldInfo = (FieldInfo)memberInfo;
                    return fieldInfo.IsPublic && IsPublic(fieldInfo.FieldType, true); // we have to check the type in case it contains embedded generic type arguments
                case MemberTypes.Method:
                    return ((MethodInfo)memberInfo).IsPublic;
                case MemberTypes.Property:
                    var property = (PropertyInfo)memberInfo;
                    var method = setter ? property.GetSetMethod(true) : property.GetGetMethod(true);
                    return IsPublic(method, reflectedType) && IsPublic(property.PropertyType, true); // we have to check the type in case it contains embedded generic type arguments
                default:
                    throw new NotSupportedException();
            }
        }

        private LazyConstructionResult EmitLazyConstruction(Type valueType, Type metadataType, TextWriter writer = null)
        {
            writer = writer ?? new SelfTextWriter(this);
            Type lazyTypeDefinition = metadataType != null ? typeof(LazyPart<,>) : typeof(LazyPart<>);
            Type[] lazyTypeArgs = metadataType != null ? new[] { valueType, metadataType } : new[] { valueType };
            Type lazyType = lazyTypeDefinition.MakeGenericType(lazyTypeArgs);
            if (IsPublic(lazyType, true))
            {
                writer.Write("new LazyPart<{0}", GetTypeName(valueType));
                if (metadataType != null)
                {
                    writer.Write(", {0}", GetTypeName(metadataType));
                }

                writer.Write(">(() => ");
                return new LazyConstructionResult(delegate
                {
                    writer.Write(")");
                });
            }
            else
            {
                var ctor = lazyTypeDefinition.GetConstructors().Single(c => c.GetParameters()[0].ParameterType.Equals(typeof(Func<object>)));
                writer.WriteLine(
                    "((ILazy<{4}>)((ConstructorInfo)MethodInfo.GetMethodFromHandle({0}.ManifestModule.ResolveMethod({1}/*{3}*/).MethodHandle, {2})).Invoke(new object[] {{ (Func<object>)(() => ",
                    GetAssemblyExpression(lazyTypeDefinition.Assembly),
                    ctor.MetadataToken,
                    this.GetClosedGenericTypeHandleExpression(lazyType),
                    GetTypeName(ctor.DeclaringType, evenNonPublic: true) + "." + ctor.Name,
                    GetTypeName(valueType) + (metadataType != null ? (", " + GetTypeName(metadataType)) : ""));
                var indent = Indent();
                return new LazyConstructionResult(
                    () =>
                    {
                        writer.Write(")");
                    },
                    () =>
                    {
                        indent.Dispose();
                        writer.Write(" }))");
                    });
            }
        }

        private IDisposable EmitExportFactoryConstruction(ImportDefinitionBinding exportFactoryImport, TextWriter writer = null)
        {
            writer = writer ?? new SelfTextWriter(this);

            if (IsPublic(exportFactoryImport.ImportingSiteElementType))
            {
                writer.Write("new {0}(", GetTypeName(exportFactoryImport.ExportFactoryType));
                return new DisposableWithAction(delegate
                {
                    writer.Write(")");
                });
            }
            else
            {
                var ctor = exportFactoryImport.ExportFactoryType.GetConstructors().Single();
                writer.WriteLine(
                    "((ConstructorInfo)MethodInfo.GetMethodFromHandle({0}.ManifestModule.ResolveMethod({1}/*{3}*/).MethodHandle, {2})).Invoke(new object[] {{ ",
                    GetAssemblyExpression(exportFactoryImport.ExportFactoryType.Assembly),
                    ctor.MetadataToken,
                    this.GetClosedGenericTypeHandleExpression(exportFactoryImport.ExportFactoryType),
                    ReflectionHelpers.GetTypeName(ctor.DeclaringType, false, true, null, null) + "." + ctor.Name);
                using (Indent())
                {
                    writer.WriteLine(
                        "{0}.CreateFuncOfType(",
                        typeof(ReflectionHelpers).FullName);
                    using (Indent())
                    {
                        writer.WriteLine(
                            "{0},",
                            this.GetExportFactoryTupleTypeExpression(exportFactoryImport.ImportingSiteElementType));
                    }
                }

                var indent = Indent(3);
                return new DisposableWithAction(delegate
                {
                    indent.Dispose();
                    writer.WriteLine();
                    writer.Write(") })");
                });
            }
        }

        private IDisposable EmitExportFactoryTupleConstruction(Type firstArgType, string valueExpression, TextWriter writer)
        {
            if (IsPublic(firstArgType))
            {
                writer.Write(
                    "Tuple.Create<{0}, Action>(({0})({1}), ",
                    this.GetTypeName(firstArgType),
                    valueExpression);

                return new DisposableWithAction(delegate
                {
                    writer.Write(")");
                });
            }
            else
            {
                string create = GetMethodInfoExpression(
                    new Func<object, object, Tuple<object, object>>(Tuple.Create<object, object>)
                    .GetMethodInfo().GetGenericMethodDefinition());
                writer.Write(
                    "{0}.MakeGenericMethod({2}, typeof(Action)).Invoke(null, new object[] {{ {1}, (Action)(",
                    create,
                    valueExpression,
                    GetTypeExpression(firstArgType));

                return new DisposableWithAction(delegate
                {
                    writer.Write(") })");
                });
            }
        }

        private string GetExportFactoryTupleTypeExpression(Type constructedType)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "typeof(Tuple<,>).MakeGenericType({0}, typeof(System.Action))",
                this.GetTypeExpression(constructedType));
        }

        private string ReserveLocalVarName(string desiredName)
        {
            if (this.localSymbols.Add(desiredName))
            {
                return desiredName;
            }

            int i = 0;
            string candidateName;
            do
            {
                i++;
                candidateName = desiredName + "_" + i.ToString(CultureInfo.InvariantCulture);
            } while (!this.localSymbols.Add(candidateName));

            return candidateName;
        }

        private string ReserveClassSymbolName(string shortName, object namedValue)
        {
            string result;
            if (this.reservedSymbols.TryGetValue(namedValue, out result))
            {
                return result;
            }

            if (this.classSymbols.Add(shortName))
            {
                result = shortName;
            }
            else
            {
                int i = 0;
                string candidateName;
                do
                {
                    i++;
                    candidateName = shortName + "_" + i.ToString(CultureInfo.InvariantCulture);
                } while (!this.classSymbols.Add(candidateName));

                result = candidateName;
            }

            this.reservedSymbols.Add(namedValue, result);
            return result;
        }

        private IDisposable Indent(int count = 1, bool withBraces = false, TextWriter writer = null)
        {
            writer = writer ?? new SelfTextWriter(this);
            if (withBraces)
            {
                writer.WriteLine("{");
            }

            this.PushIndent(new string(' ', count * 4));

            return new DisposableWithAction(delegate
            {
                this.PopIndent();
                if (withBraces)
                {
                    writer.WriteLine("}");
                }
            });
        }

        private class SelfTextWriter : TextWriter
        {
            private CompositionTemplateFactory factory;

            internal SelfTextWriter(CompositionTemplateFactory factory)
            {
                this.factory = factory;
            }

            public override Encoding Encoding
            {
                get { return Encoding.Default; }
            }

            public override void Write(char value)
            {
                this.factory.Write(value.ToString());
            }

            public override void Write(string value)
            {
                this.factory.Write(value);
            }
        }

        private class LazyConstructionResult : IDisposable
        {
            private Action beforeWriteMetadata;
            private Action disposal;

            internal LazyConstructionResult(Action beforeWriteMetadata, Action disposal)
            {
                this.beforeWriteMetadata = beforeWriteMetadata;
                this.disposal = disposal;
            }

            internal LazyConstructionResult(Action disposal)
            {
                this.disposal = disposal;
            }

            internal void OnBeforeWriteMetadata()
            {
                if (this.beforeWriteMetadata != null)
                {
                    this.beforeWriteMetadata();
                    this.beforeWriteMetadata = null;
                }
            }

            public void Dispose()
            {
                this.OnBeforeWriteMetadata(); // in case the caller didn't bother with metadata.
                if (this.disposal != null)
                {
                    this.disposal();
                }
            }
        }
    }
}
