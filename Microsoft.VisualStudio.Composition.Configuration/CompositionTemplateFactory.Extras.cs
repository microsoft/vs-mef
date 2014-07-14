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
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Validation;

    partial class CompositionTemplateFactory
    {
        private const string InstantiatedPartLocalVarName = "result";

        private readonly HashSet<Assembly> relevantAssemblies = new HashSet<Assembly>();

        private readonly HashSet<Type> relevantEmbeddedTypes = new HashSet<Type>();

        /// <summary>
        /// Additional members of the generated ExportProvider-derived type to emit.
        /// </summary>
        private readonly List<MemberDeclarationSyntax> extraMembers = new List<MemberDeclarationSyntax>();

        /// <summary>
        /// The list of assemblies that need to be referenced by the generated code.
        /// </summary>
        private readonly List<Assembly> reflectionLoadedAssemblies = new List<Assembly>();

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
                    "{0}.ResolveField({1}/*{2}*/)",
                    this.GetManifestModuleSyntax(fieldInfo.DeclaringType.Assembly),
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
                    "((MethodInfo)MethodInfo.GetMethodFromHandle({0}.ResolveMethod({1}/*{3}*/).MethodHandle, {2}))",
                    this.GetManifestModuleSyntax(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken,
                    this.GetClosedGenericTypeHandleExpression(methodInfo.DeclaringType),
                    GetTypeName(methodInfo.DeclaringType, evenNonPublic: true) + "." + methodInfo.Name);
            }
            else
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "((MethodInfo){0}.ResolveMethod({1}/*{2}*/))",
                    this.GetManifestModuleSyntax(methodInfo.DeclaringType.Assembly),
                    methodInfo.MetadataToken,
                    GetTypeName(methodInfo.DeclaringType, evenNonPublic: true) + "." + methodInfo.Name);
            }
        }

        private string GetClosedGenericTypeExpression(Type type)
        {
            Requires.NotNull(type, "type");
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}.ResolveType({1}/*{3}*/).MakeGenericType({2})",
                this.GetManifestModuleSyntax(type.Assembly),
                type.GetGenericTypeDefinition().MetadataToken,
                string.Join(", ", type.GetGenericArguments().Select(t => t.IsGenericType && t.ContainsGenericParameters ? GetClosedGenericTypeExpression(t) : GetTypeExpression(t))),
                type.ContainsGenericParameters ? "incomplete" : this.GetTypeName(type, evenNonPublic: true));
        }

        private string GetClosedGenericTypeHandleExpression(Type type)
        {
            return GetClosedGenericTypeExpression(type) + ".TypeHandle";
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
                            if (nonSharedInstanceRequired) // code gen size optimization: take advantage of the optional parameter.
                            {
                                writer.Write(", nonSharedInstanceRequired: {0}", nonSharedInstanceRequired ? "true" : "false");
                            }

                            writer.Write(")");
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

        private ExpressionSyntax GetExportMetadata(ExportDefinitionBinding export)
        {
            return this.GetSyntaxToReconstructValue(export.ExportDefinition.Metadata);
        }

        private ExpressionSyntax GetSyntaxToReconstructValue<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> value)
        {
            if (value == null)
            {
                return GetSyntaxToReconstructValue((object)null);
            }

            ExpressionSyntax populatingExpression = SyntaxFactory.IdentifierName("EmptyMetadata");
            foreach (var pair in value)
            {
                populatingExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        populatingExpression,
                        SyntaxFactory.IdentifierName("Add")),
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList<ArgumentSyntax>(new ArgumentSyntax[] { 
                                SyntaxFactory.Argument(GetSyntaxToReconstructValue(pair.Key)),
                                SyntaxFactory.Argument(GetSyntaxToReconstructValue(pair.Value)),
                            })));
            }

            return populatingExpression;
        }

        private ExpressionSyntax GetSyntaxToReconstructValue(object value)
        {
            if (value == null)
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
            }

            Type valueType = value.GetType();
            if (value is string)
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal((string)value));
            }
            else if (typeof(char).IsEquivalentTo(valueType))
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal((char)value));
            }
            else if (typeof(bool).IsEquivalentTo(valueType))
            {
                return (bool)value
                    ? SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)
                    : SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
            }
            else if (valueType.IsEquivalentTo(typeof(double)) && (double)value == double.MaxValue)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)),
                    SyntaxFactory.IdentifierName("MaxValue"));
            }
            else if (valueType.IsEquivalentTo(typeof(double)) && (double)value == double.MinValue)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)),
                    SyntaxFactory.IdentifierName("MinValue"));
            }
            else if (valueType.IsEquivalentTo(typeof(float)) && (float)value == float.MaxValue)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)),
                    SyntaxFactory.IdentifierName("MaxValue"));
            }
            else if (valueType.IsEquivalentTo(typeof(float)) && (float)value == float.MinValue)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)),
                    SyntaxFactory.IdentifierName("MinValue"));
            }
            else if (valueType.IsPrimitive)
            {
                return SyntaxFactory.CastExpression(
                    SyntaxFactory.ParseTypeName(GetTypeName(valueType)),
                    SyntaxFactory.ParenthesizedExpression(SyntaxFactory.ParseExpression(value.ToString())));
            }
            else if (valueType.IsEquivalentTo(typeof(Guid)))
            {
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Guid"),
                        SyntaxFactory.IdentifierName("Parse")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(((Guid)value).ToString()))))));
            }
            else if (valueType.IsEnum)
            {
                ExpressionSyntax underlyingTypeValue = GetSyntaxToReconstructValue(Convert.ChangeType(value, Enum.GetUnderlyingType(valueType)));
                if (IsPublic(valueType, true))
                {
                    return SyntaxFactory.CastExpression(GetTypeNameSyntax(valueType), underlyingTypeValue);
                }
                else
                {
                    return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Enum"),
                            SyntaxFactory.IdentifierName("ToObject")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(GetTypeExpressionSyntax(valueType)),
                            SyntaxFactory.Argument(underlyingTypeValue))));
                }
            }
            else if (typeof(Type).IsAssignableFrom(valueType))
            {
                // assumeNonPublic=true because typeof() would result in the JIT compiler
                // loading the assembly containing the type itself even before this
                // part is activated.
                return SyntaxFactory.CastExpression(
                    GetTypeNameSyntax(valueType), // Cast as TypeInfo to avoid some compilation errors.
                    SyntaxFactory.ParseExpression(GetTypeExpression((Type)value, assumeNonPublic: true)));
            }
            else if (valueType.IsArray)
            {
                var array = (Array)value;
                return SyntaxFactory.ArrayCreationExpression(
                    SyntaxFactory.ArrayType(GetTypeNameSyntax(valueType.GetElementType()))
                        .WithRankSpecifiers(
                            SyntaxFactory.SingletonList(
                            SyntaxFactory.ArrayRankSpecifier(
                                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                    SyntaxFactory.OmittedArraySizeExpression())))),
                    SyntaxFactory.InitializerExpression(
                        SyntaxKind.ArrayInitializerExpression,
                        SyntaxFactory.SeparatedList<ExpressionSyntax>(
                            array.Cast<object>().Select(GetSyntaxToReconstructValue))))
                    .WithNewKeywordTrivia();
            }

            throw new NotSupportedException();
        }

        private TypeSyntax GetTypeNameSyntax(Type type, bool genericTypeDefinition = false, bool evenNonPublic = false)
        {
            Requires.NotNull(type, "type");

            if (type.IsEquivalentTo(typeof(string)))
            {
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword));
            }
            else if (type.IsEquivalentTo(typeof(object)))
            {
                return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));
            }

            return SyntaxFactory.ParseTypeName(this.GetTypeName(type, genericTypeDefinition, evenNonPublic));
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
                var manifestModuleSyntax = this.GetManifestModuleSyntax(ctor.DeclaringType.Assembly);
                var typeName = GetTypeNameSyntax(ctor.DeclaringType, evenNonPublic: true).ToString() + "." + ctor.Name;
                string ctorExpression;
                if (ctor.DeclaringType.IsGenericType)
                {
                    ctorExpression = string.Format(
                        CultureInfo.InvariantCulture,
                        "(ConstructorInfo)MethodInfo.GetMethodFromHandle({2}.ResolveMethod({0}/*{3}*/).MethodHandle, {1})",
                        ctor.MetadataToken,
                        this.GetClosedGenericTypeHandleExpression(ctor.DeclaringType),
                        manifestModuleSyntax,
                        typeName);
                }
                else
                {
                    ctorExpression = string.Format(
                        CultureInfo.InvariantCulture,
                        "(ConstructorInfo){0}.ResolveMethod({1}/*{2}*/)",
                        manifestModuleSyntax,
                        ctor.MetadataToken,
                        typeName);
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
                bool newSharingScope = importDefinition.ExportFactorySharingBoundaries.Count > 0;

                if (newSharingScope)
                {
                    writer.Write("() => { var scope = ");
                    writer.Write("new CompiledExportProvider(this, new [] { ");
                    writer.Write(string.Join(", ", importDefinition.ExportFactorySharingBoundaries.Select(Quote)));
                    writer.Write(" }); var part = scope.");
                }
                else
                {
                    writer.Write("() => { var part = ");
                }

                return new DisposableWithAction(delegate
                {
                    writer.Write(".Value; return ");
                    using (this.EmitExportFactoryTupleConstruction(import.ImportingSiteElementType, "part", writer))
                    {
                        writer.Write("() => { ");
                        if (newSharingScope || typeof(IDisposable).IsAssignableFrom(export.PartDefinition.Type))
                        {
                            writer.Write("((IDisposable){0}).Dispose(); ", newSharingScope ? "scope" : "part");
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
                            this.Write("new Export(importDefinition.ContractName, {0}, ",
                                GetExportMetadata(export));
                            if (export.ExportingMember == null && !export.PartDefinition.Type.IsGenericType)
                            {
                                this.Write(
                                    "GetValueFactoryFunc({0}, provisionalSharedObjects)",
                                    GetPartFactoryMethodName(export.PartDefinition));
                            }
                            else
                            {
                                this.Write(
                                    "() => ({0}).Value",
                                    this.GetValueFactoryExpression(synthesizedImport, export));
                            }

                            this.WriteLine("),");
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

        private ExpressionSyntax GetTypeExpressionSyntax(Type type, bool genericTypeDefinition = false, bool assumeNonPublic = false)
        {
            Requires.NotNull(type, "type");

            if (!assumeNonPublic && IsPublic(type, true) && !type.IsEmbeddedType()) // embedded types need to be matched to their receiving assembly
            {
                return SyntaxFactory.TypeOfExpression(this.GetTypeNameSyntax(type, genericTypeDefinition));
            }
            else
            {
                var targetType = (genericTypeDefinition && type.IsGenericType) ? type.GetGenericTypeDefinition() : type;
                var typeExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("Type"),
                        SyntaxFactory.IdentifierName("GetType")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(targetType.AssemblyQualifiedName))))));

                if (!genericTypeDefinition && targetType.IsGenericTypeDefinition)
                {
                    // Concatenate on the generic type arguments if the caller didn't explicitly want the generic type definition.
                    // Note that the type itself may be a generic type definition, in which case the concatenated types might be
                    // T1, T2. That's fine. In fact that's what we want because it causes the types from the caller's caller to
                    // propagate.
                    var constructedTypeExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            typeExpression,
                            SyntaxFactory.IdentifierName("MakeGenericType")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                            SyntaxKind.CommaToken,
                            targetType.GetGenericArguments().Select(arg => SyntaxFactory.Argument(this.GetTypeExpressionSyntax(arg, false))).ToArray())));
                    return constructedTypeExpression;
                }

                return typeExpression;
            }
        }

        /// <summary>
        /// Gets a C# expression that evaluates to a System.Type instance for the specified type.
        /// </summary>
        private string GetTypeExpression(Type type, bool genericTypeDefinition = false, bool assumeNonPublic = false)
        {
            return this.GetTypeExpressionSyntax(type, genericTypeDefinition, assumeNonPublic)
                .NormalizeWhitespace().ToString();
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
                    GetSyntaxToReconstructValue(defaultValueAttribute.Value),
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
            Requires.NotNull(part, "part");
            string name = "GetOrCreate" + part.Id;
            return name;
        }

        private static string GetPartFactoryMethodName(ComposablePartDefinition part, params string[] typeArguments)
        {
            if (typeArguments == null || typeArguments.Length == 0)
            {
                typeArguments = part.Type.GetGenericArguments().Select(t => t.Name).ToArray();
            }

            string name = GetPartFactoryMethodNameNoTypeArgs(part);

            if (typeArguments.Length > 0)
            {
                name += "<";
                name += string.Join(",", typeArguments);
                name += ">";
            }

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
                    "((ILazy<{4}>)((ConstructorInfo)MethodInfo.GetMethodFromHandle({0}.ResolveMethod({1}/*{3}*/).MethodHandle, {2})).Invoke(new object[] {{ (Func<object>)(() => ",
                    this.GetManifestModuleSyntax(lazyTypeDefinition.Assembly),
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

            if (IsPublic(exportFactoryImport.ImportingSiteType, true))
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
                    "((ConstructorInfo)MethodInfo.GetMethodFromHandle({0}.ResolveMethod({1}/*{3}*/).MethodHandle, {2})).Invoke(new object[] {{ ",
                    this.GetManifestModuleSyntax(exportFactoryImport.ExportFactoryType.Assembly),
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
            Type tupleType = typeof(Tuple<,>).MakeGenericType(firstArgType, typeof(Action));
            if (IsPublic(tupleType, true))
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

        /// <summary>
        /// Gets the expression syntax for the manifest module of the given assembly.
        /// </summary>
        /// <param name="assembly">The assembly for which the manifest module is required by the generated code.</param>
        /// <returns>The expression syntax.</returns>
        private ExpressionSyntax GetManifestModuleSyntax(Assembly assembly)
        {
            // Ensure that this assembly has been assigned an index, which we'll use in the generated code.
            int index = this.reflectionLoadedAssemblies.IndexOf(assembly);
            if (index < 0)
            {
                this.reflectionLoadedAssemblies.Add(assembly);
                index = this.reflectionLoadedAssemblies.Count - 1;
            }

            // CODE: this.GetAssemblyManifest(index)
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName("GetAssemblyManifest")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(index))))));
        }

        private void EmitAdditionalMembers()
        {
            this.extraMembers.Add(this.CreateGetAssemblyNameMethod());

            foreach (var member in this.extraMembers)
            {
                this.WriteLine(string.Empty);
                this.WriteLine(member.NormalizeWhitespace().ToString());
            }
        }

        /// <summary>
        /// Creates the syntax for the ExportProvider.GetAssemblyName method override.
        /// </summary>
        private MemberDeclarationSyntax CreateGetAssemblyNameMethod()
        {
            var assemblyIdParameter = SyntaxFactory.IdentifierName("assemblyId");
            var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                "GetAssemblyName")
                .AddModifiers(
                    SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                    SyntaxFactory.Parameter(
                        SyntaxFactory.List<AttributeListSyntax>(),
                        SyntaxFactory.TokenList(),
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
                        assemblyIdParameter.Identifier,
                        null))));

            var switchStatement = SyntaxFactory.SwitchStatement(assemblyIdParameter);

            for (int i = 0; i < this.reflectionLoadedAssemblies.Count; i++)
            {
                Assembly assembly = this.reflectionLoadedAssemblies[i];
                var label = SyntaxFactory.SingletonList<SwitchLabelSyntax>(
                    SyntaxFactory.SwitchLabel(
                        SyntaxKind.CaseSwitchLabel,
                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i))));
                var statement = SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(assembly.FullName)));
                var section = SyntaxFactory.SwitchSection(label, SyntaxFactory.SingletonList<StatementSyntax>(statement));
                switchStatement = switchStatement.AddSections(section);
            }

            switchStatement = switchStatement.AddSections(SyntaxFactory.SwitchSection(
                SyntaxFactory.SingletonList<SwitchLabelSyntax>(
                    SyntaxFactory.SwitchLabel(SyntaxKind.DefaultSwitchLabel)),
                SyntaxFactory.SingletonList<StatementSyntax>(
                    SyntaxFactory.ThrowStatement(
                        SyntaxFactory.ObjectCreationExpression(this.GetTypeNameSyntax(typeof(ArgumentOutOfRangeException)))
                            .WithArgumentList(SyntaxFactory.ArgumentList())))));

            method = method.WithBody(SyntaxFactory.Block(switchStatement));
            return method;
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

        private IDisposable Indent(int count = 1, bool withBraces = false, bool noLineFeedAfterClosingBrace = false, TextWriter writer = null)
        {
            if (count == 0)
            {
                return null;
            }

            if (count > 1)
            {
                // Push all but the last level *before* printing the curly braces.
                this.PushIndent(new string(' ', (count - 1) * 4));
            }

            writer = writer ?? new SelfTextWriter(this);
            if (withBraces)
            {
                writer.WriteLine("{");
            }

            this.PushIndent(new string(' ', 4));

            return new DisposableWithAction(delegate
            {
                this.PopIndent();
                if (withBraces)
                {
                    writer.Write("}");
                    if (!noLineFeedAfterClosingBrace)
                    {
                        writer.WriteLine();
                    }
                }

                if (count > 1)
                {
                    // Pop the extra outer-indent.
                    this.PopIndent();
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

            public override void WriteLine()
            {
                this.factory.WriteLine(string.Empty);
            }

            public override void WriteLine(string value)
            {
                this.factory.WriteLine(value);
            }

            public override void WriteLine(string format, params object[] arg)
            {
                this.factory.WriteLine(format, arg);
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
