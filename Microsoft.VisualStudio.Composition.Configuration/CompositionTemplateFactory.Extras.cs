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

        private static readonly TypeSyntax dictionaryOfTypeObject = SyntaxFactory.GenericName("Dictionary")
                    .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(CodeGen.JoinSyntaxNodes<TypeSyntax>(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.IdentifierName("Type"),
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))));

        private static readonly ObjectCreationExpressionSyntax newDictionaryOfTypeObjectExpression =
            SyntaxFactory.ObjectCreationExpression(dictionaryOfTypeObject, SyntaxFactory.ArgumentList(), null);

        private static readonly LiteralExpressionSyntax NullSyntax = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

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

        private StatementSyntax CreateMemberAssignment(ImportDefinitionBinding import, ExpressionSyntax value, IdentifierNameSyntax partInstanceVar)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(value, "value");

            StatementSyntax statement;
            if (IsPublic(import.ImportingMember, import.ComposablePartType, setter: true))
            {
                // result.Property = value;
                statement = SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, partInstanceVar, SyntaxFactory.IdentifierName(import.ImportingMember.Name)),
                    value));
            }
            else
            {
                ExpressionSyntax expression;
                var importingField = import.ImportingMember as FieldInfo;
                var importingProperty = import.ImportingMember as PropertyInfo;
                Assumes.True(importingField != null || importingProperty != null);

                if (importingField != null)
                {
                    // fieldInfo.SetValue(result, value);
                    expression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, this.GetFieldInfoExpressionSyntax(importingField), SyntaxFactory.IdentifierName("SetValue")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(partInstanceVar),
                            SyntaxFactory.Argument(value))));
                }
                else // property
                {
                    // propertyInfo.SetValue(result, value);
                    expression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, this.GetMethodInfoExpression(importingProperty.GetSetMethod(true)), SyntaxFactory.IdentifierName("Invoke")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(partInstanceVar),
                            GetObjectArrayArgument(value))));
                }

                return SyntaxFactory.ExpressionStatement(expression);
            }

            return statement;
        }

        private ExpressionSyntax CreateMemberRetrieval(MemberInfo member, ExpressionSyntax declaringTypeInstance)
        {
            Requires.NotNull(member, "member");
            Requires.NotNull(declaringTypeInstance, "declaringTypeInstance");

            var type = member as Type;
            if (type != null)
            {
                // The member is the type itself.
                return declaringTypeInstance;
            }

            bool isStatic = member.IsStatic();
            if (IsPublic(member, member.DeclaringType))
            {
                // Cast to make sure we succeed even if the member is an explicit interface implementation.
                var typeOrInstance = isStatic
                    ? (ExpressionSyntax)this.GetTypeNameSyntax(member.DeclaringType)
                    : SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                        this.GetTypeNameSyntax(member.DeclaringType),
                        declaringTypeInstance));
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    typeOrInstance,
                    SyntaxFactory.IdentifierName(member.Name));
            }

            var field = member as FieldInfo;
            if (field != null)
            {
                // fieldInfo.GetValue(instance)
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        this.GetFieldInfoExpressionSyntax(field),
                        SyntaxFactory.IdentifierName("GetValue")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                        isStatic ? NullSyntax : declaringTypeInstance))));
            }

            var property = member as PropertyInfo;
            if (property != null)
            {
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        GetMethodInfoExpression(property.GetGetMethod(true)),
                        SyntaxFactory.IdentifierName("Invoke")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(isStatic ? NullSyntax : declaringTypeInstance),
                        GetObjectArrayArgument())));
            }

            var method = member as MethodInfo;
            if (method != null)
            {
                return this.GetMethodInfoExpression(method);
            }

            throw new NotSupportedException();
        }

        private ExpressionSyntax GetFieldInfoExpressionSyntax(FieldInfo fieldInfo)
        {
            Requires.NotNull(fieldInfo, "fieldInfo");

            if (fieldInfo.DeclaringType.IsGenericType)
            {
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        this.GetClosedGenericTypeExpression(fieldInfo.DeclaringType),
                        SyntaxFactory.IdentifierName("GetField")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(fieldInfo.Name))),
                        SyntaxFactory.Argument(SyntaxFactory.BinaryExpression(
                            SyntaxKind.BitwiseOrExpression,
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("BindingFlags"), SyntaxFactory.IdentifierName("Instance")),
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("BindingFlags"), SyntaxFactory.IdentifierName("NonPublic")))))));
            }
            else
            {
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        this.GetManifestModuleSyntax(fieldInfo.DeclaringType.Assembly),
                        SyntaxFactory.IdentifierName("ResolveField")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(fieldInfo.MetadataToken))
                            .WithTrailingTrivia(SyntaxFactory.Comment("/*" + GetTypeName(fieldInfo.DeclaringType, evenNonPublic: true) + "." + fieldInfo.Name + "*/"))))));
            }
        }

        /// <summary>
        /// Gets syntax that will reconstruct the MemberInfo for a given member.
        /// </summary>
        /// <param name="member">A field or method. If a property, either the getter or the setter will be retrieved.</param>
        /// <param name="favorPropertySetter"><c>true</c> to create syntax to reconstruct the property setter method; <c>false</c> to reconstruct the getter method.</param>
        /// <returns>The reconstruction syntax.</returns>
        private ExpressionSyntax GetMemberInfoSyntax(MemberInfo member, bool favorPropertySetter = false)
        {
            Requires.NotNull(member, "member");

            var property = member as PropertyInfo;
            if (property != null)
            {
                member = favorPropertySetter ? property.GetSetMethod(true) : property.GetGetMethod(true);
            }

            IdentifierNameSyntax infoClass, memberHandle, getMemberFromHandle, resolveMember;
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    infoClass = SyntaxFactory.IdentifierName("FieldInfo");
                    memberHandle = SyntaxFactory.IdentifierName("FieldHandle");
                    getMemberFromHandle = SyntaxFactory.IdentifierName("GetFieldFromHandle");
                    resolveMember = SyntaxFactory.IdentifierName("ResolveField");
                    break;
                case MemberTypes.Method:
                    infoClass = SyntaxFactory.IdentifierName("MethodInfo");
                    memberHandle = SyntaxFactory.IdentifierName("MethodHandle");
                    getMemberFromHandle = SyntaxFactory.IdentifierName("GetMethodFromHandle");
                    resolveMember = SyntaxFactory.IdentifierName("ResolveMethod");
                    break;
                default:
                    throw new NotSupportedException();
            }

            // manifest.ResolveMember(metadataToken/*description*/)
            var resolveMemberInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    this.GetManifestModuleSyntax(member.DeclaringType.Assembly),
                    resolveMember),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(member.MetadataToken))
                        .WithTrailingTrivia(SyntaxFactory.Comment("/*" + GetTypeName(member.DeclaringType, evenNonPublic: true) + "." + member.Name + "*/"))))));

            ExpressionSyntax memberInfoSyntax;
            if (member.DeclaringType.IsGenericType)
            {
                // MethodInfo.GetMethodFromHandle({0}.ResolveMethod({1}/*{3}*/).MethodHandle, {2})
                memberInfoSyntax = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, infoClass, getMemberFromHandle),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, resolveMemberInvocation, memberHandle)),
                        SyntaxFactory.Argument(this.GetClosedGenericTypeHandleExpression(member.DeclaringType)))));
            }
            else
            {
                memberInfoSyntax = resolveMemberInvocation;
            }

            var castExpression = SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.CastExpression(infoClass, memberInfoSyntax));
            return castExpression;
        }

        private ExpressionSyntax GetMethodInfoExpression(MethodInfo methodInfo)
        {
            return this.GetMemberInfoSyntax(methodInfo);
        }

        private ExpressionSyntax GetClosedGenericTypeExpression(Type type)
        {
            Requires.NotNull(type, "type");

            // {0}.ResolveType({1}/*{3}*/).MakeGenericType({2})
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            this.GetManifestModuleSyntax(type.Assembly),
                            SyntaxFactory.IdentifierName("ResolveType")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(type.GetGenericTypeDefinition().MetadataToken)))
                                .WithTrailingTrivia(SyntaxFactory.Comment("/*" + (type.ContainsGenericParameters ? "incomplete" : this.GetTypeName(type, evenNonPublic: true)) + "*/"))))),
                    SyntaxFactory.IdentifierName("MakeGenericType")),
                SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                    SyntaxKind.CommaToken,
                    type.GetGenericArguments().Select(t => SyntaxFactory.Argument(t.IsGenericType && t.ContainsGenericParameters ? GetClosedGenericTypeExpression(t) : GetTypeExpressionSyntax(t))).ToArray())));
        }

        private ExpressionSyntax GetClosedGenericTypeHandleExpression(Type type)
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GetClosedGenericTypeExpression(type),
                SyntaxFactory.IdentifierName("TypeHandle"));
        }

        private StatementSyntax[] GetImportSatisfyingAssignmentSyntax(KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExport, ExpressionSyntax provisionalSharedObjects)
        {
            Requires.Argument(satisfyingExport.Key.ImportingMember != null, "satisfyingExport", "No member to satisfy.");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            var import = satisfyingExport.Key;
            var importingMember = satisfyingExport.Key.ImportingMember;
            var exports = satisfyingExport.Value;
            var partInstanceVar = SyntaxFactory.IdentifierName(InstantiatedPartLocalVarName);

            IReadOnlyList<StatementSyntax> prereqs;
            var expression = GetImportSatisfyingExpression(import, exports, provisionalSharedObjects, out prereqs);
            var statements = new List<StatementSyntax>(prereqs);
            statements.Add(CreateMemberAssignment(import, expression, partInstanceVar));
            return statements.ToArray();
        }

        private ExpressionSyntax GetImportSatisfyingExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, ExpressionSyntax provisionalSharedObjects, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                Type enumerableOfTType = typeof(IEnumerable<>).MakeGenericType(import.ImportingSiteTypeWithoutCollection);
                if (import.ImportingSiteType.IsArray || import.ImportingSiteType.IsEquivalentTo(enumerableOfTType))
                {
                    return this.GetSatisfyImportManyArrayExpression(import, exports, provisionalSharedObjects, out prerequisiteStatements);
                }
                else
                {
                    return this.GetSatisfyImportManyCollectionExpression(import, exports, provisionalSharedObjects, out prerequisiteStatements);
                }
            }
            else if (exports.Any())
            {
                prerequisiteStatements = ImmutableList<StatementSyntax>.Empty;
                return this.GetImportAssignableValueForExport(import, exports.Single(), provisionalSharedObjects);
            }
            else
            {
                prerequisiteStatements = ImmutableList<StatementSyntax>.Empty;
                if (IsPublic(import.ImportingSiteType))
                {
                    return SyntaxFactory.DefaultExpression(this.GetTypeNameSyntax(import.ImportingSiteType));
                }
                else if (import.ImportingSiteType.IsValueType)
                {
                    // It's a non-public struct. We have to construct its default value by hand.
                    return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Activator"),
                            SyntaxFactory.IdentifierName("CreateInstance")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(GetTypeExpressionSyntax(import.ImportingSiteType)))));
                }
                else
                {
                    return SyntaxFactory.CastExpression(
                        this.GetTypeNameSyntax(import.ImportingSiteType),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullKeyword));
                }
            }
        }

        private ExpressionSyntax GetSatisfyImportManyArrayExpression(ImportDefinitionBinding import, IEnumerable<ExportDefinitionBinding> exports, ExpressionSyntax provisionalSharedObjects, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            var prereqs = new List<StatementSyntax>();
            prerequisiteStatements = prereqs;

            if (IsPublic(import.ImportingSiteType, true))
            {
                var arrayType = SyntaxFactory.ArrayType(
                    this.GetTypeNameSyntax(import.ImportingSiteTypeWithoutCollection),
                    SyntaxFactory.SingletonList<ArrayRankSpecifierSyntax>(
                        SyntaxFactory.ArrayRankSpecifier(
                            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.OmittedArraySizeExpression()))));
                return SyntaxFactory.ArrayCreationExpression(
                    arrayType,
                    SyntaxFactory.InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    CodeGen.JoinSyntaxNodes<ExpressionSyntax>(
                        SyntaxKind.CommaToken,
                        exports.Select(export => this.GetImportAssignableValueForExport(import, export, provisionalSharedObjects)).ToArray())));
            }
            else
            {
                // This will require a multi-statement construction of the array.
                // var localVarName = Array.CreateInstance(typeof(...), exports.Count);
                var localVar = SyntaxFactory.IdentifierName(ReserveLocalVarName(import.ImportingMember != null ? import.ImportingMember.Name : "tmp"));
                prereqs.Add(SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                        .AddVariables(SyntaxFactory.VariableDeclarator(localVar.Identifier)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("Array"),
                                        SyntaxFactory.IdentifierName("CreateInstance")),
                                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                                        SyntaxKind.CommaToken,
                                        SyntaxFactory.Argument(this.GetTypeExpressionSyntax(import.ImportingSiteTypeWithoutCollection)),
                                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(exports.Count())))))))))));
                int arrayIndex = 0;
                foreach (var export in exports)
                {
                    prereqs.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            localVar,
                            SyntaxFactory.IdentifierName("SetValue")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(this.GetImportAssignableValueForExport(import, export, provisionalSharedObjects)),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(arrayIndex++))))))));
                }

                return localVar;
            }
        }

        private ExpressionSyntax GetSatisfyImportManyCollectionExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, ExpressionSyntax provisionalSharedObjects, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            var importDefinition = import.ImportDefinition;
            Type elementType = import.ImportingSiteTypeWithoutCollection;
            Type listType = typeof(List<>).MakeGenericType(elementType);
            bool stronglyTypedCollection = IsPublic(elementType, true);
            Type icollectionType = typeof(ICollection<>).MakeGenericType(elementType);
            var importManyLocalVarType = stronglyTypedCollection ? GetTypeNameSyntax(icollectionType) : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));
            var tempVar = SyntaxFactory.IdentifierName(ReserveLocalVarName(import.ImportingMember.Name));
            var instantiatedPartLocalVar = SyntaxFactory.IdentifierName(InstantiatedPartLocalVarName);

            var prereqs = new List<StatementSyntax>();
            prerequisiteStatements = prereqs;

            // Casting the collection to ICollection<T> instead of the concrete type guarantees
            // that we'll be able to call Add(T) and Clear() on it even if the type is NonPublic
            // or its methods are explicit interface implementations.
            ExpressionSyntax tempVarAssignedValue;
            if (import.ImportingMember is FieldInfo)
            {
                // fieldInfo.GetValue(result);
                tempVarAssignedValue = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        this.GetFieldInfoExpressionSyntax((FieldInfo)import.ImportingMember),
                        SyntaxFactory.IdentifierName("GetValue")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(instantiatedPartLocalVar))));
            }
            else
            {
                // methodInfo.Invoke(result, new object[0])
                tempVarAssignedValue = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        GetMethodInfoExpression(((PropertyInfo)import.ImportingMember).GetGetMethod(true)),
                        SyntaxFactory.IdentifierName("Invoke")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(instantiatedPartLocalVar),
                        GetObjectArrayArgument())));
            }

            // var tempVar = (ICollection<T>)...
            prereqs.Add(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var"),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(tempVar.Identifier)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.CastExpression(importManyLocalVarType, tempVarAssignedValue)))))));

            var initBlock = SyntaxFactory.Block();
            var clearBlock = SyntaxFactory.Block();

            // if (tempVar == null)
            {
                if (PartDiscovery.IsImportManyCollectionTypeCreateable(import))
                {
                    ConstructorInfo collectionCtor;
                    if (import.ImportingSiteType.IsAssignableFrom(listType))
                    {
                        collectionCtor = typeof(List<>).MakeGenericType(elementType).GetConstructor(new Type[0]);
                    }
                    else
                    {
                        collectionCtor = import.ImportingSiteType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
                    }

                    initBlock = initBlock.AddStatements(
                        // tempVar = new List<T>();
                        SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            tempVar,
                            SyntaxFactory.CastExpression(
                                importManyLocalVarType,
                                this.ObjectCreationExpression(collectionCtor, new ExpressionSyntax[0])))),

                        // result.Member = tempVar
                        this.CreateMemberAssignment(
                            import,
                            SyntaxFactory.CastExpression(this.GetTypeNameSyntax(import.ImportingSiteType), tempVar),
                            instantiatedPartLocalVar));
                }
                else
                {
                    initBlock = initBlock.AddStatements(
                        SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName("InvalidOperationException"),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(string.Format(
                                        CultureInfo.InvariantCulture,
                                        "throw new InvalidOperationException(\"The {0}.{1} collection must be instantiated by the importing constructor.\");",
                                        import.ComposablePartType.Name,
                                        import.ImportingMember.Name)))))),
                                null)));
                }
            }

            // else tempVar != null
            {
                InvocationExpressionSyntax clearInvocation;
                if (stronglyTypedCollection)
                {
                    // tempVar.Clear();
                    clearInvocation = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, tempVar, SyntaxFactory.IdentifierName("Clear")),
                            SyntaxFactory.ArgumentList());
                }
                else
                {
                    // clearMethodInfo.Invoke(tempVar, new object[0])
                    clearInvocation = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            GetMethodInfoExpression(icollectionType.GetMethod("Clear")),
                            SyntaxFactory.IdentifierName("Invoke")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(tempVar),
                            GetObjectArrayArgument())));
                }

                clearBlock = clearBlock.AddStatements(SyntaxFactory.ExpressionStatement(clearInvocation));
            }

            prereqs.Add(SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, tempVar, SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                initBlock,
                SyntaxFactory.ElseClause(clearBlock)));

            foreach (var export in exports)
            {
                InvocationExpressionSyntax addExpression;
                var exportValue = this.GetImportAssignableValueForExport(import, export, provisionalSharedObjects);
                if (stronglyTypedCollection)
                {
                    // tempVar.Add(export);
                    addExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, tempVar, SyntaxFactory.IdentifierName("Add")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(exportValue))));
                }
                else
                {
                    // addMethodInfo.Invoke(tempVar, new object[] { export })
                    addExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetMethodInfoExpression(icollectionType.GetMethod("Add")), SyntaxFactory.IdentifierName("Invoke")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(tempVar),
                            GetObjectArrayArgument(exportValue))));
                }

                prereqs.Add(SyntaxFactory.ExpressionStatement(addExpression));
            }

            // (TCollection)tempVar
            var castExpression = SyntaxFactory.CastExpression(
                this.GetTypeNameSyntax(import.ImportingSiteType),
                tempVar);

            return castExpression;
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

        private ExpressionSyntax ObjectCreationExpression(ComposablePartDefinition partDefinition, ExpressionSyntax[] arguments, bool alwaysUseReflection = false)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            return this.ObjectCreationExpression(partDefinition.ImportingConstructorInfo, arguments, alwaysUseReflection);
        }

        private ExpressionSyntax ObjectCreationExpression(ConstructorInfo ctor, ExpressionSyntax[] arguments, bool alwaysUseReflection = false)
        {
            Requires.NotNull(ctor, "ctor");
            Requires.NotNull(arguments, "arguments");

            bool publicCtor = !alwaysUseReflection && IsPublic(ctor, ctor.DeclaringType);
            if (publicCtor)
            {
                return SyntaxFactory.ObjectCreationExpression(
                    this.GetTypeNameSyntax(ctor.DeclaringType),
                    SyntaxFactory.ArgumentList(
                        CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                            SyntaxKind.CommaToken,
                            arguments.Select(SyntaxFactory.Argument).ToArray())),
                    null);
            }
            else
            {
                var manifestModuleSyntax = this.GetManifestModuleSyntax(ctor.DeclaringType.Assembly);
                var typeName = GetTypeNameSyntax(ctor.DeclaringType, evenNonPublic: true).ToString() + "." + ctor.Name;

                // manifestModule.ResolveMethod(ctorMetadataToken/*typeName.ctor*/)
                var resolveMethodSyntax = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        manifestModuleSyntax,
                        SyntaxFactory.IdentifierName("ResolveMethod")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ctor.MetadataToken))
                        .WithTrailingTrivia(SyntaxFactory.Comment("/*" + typeName + "*/"))))));

                ExpressionSyntax ctorSyntax;
                if (ctor.DeclaringType.IsGenericType)
                {
                    // "(ConstructorInfo)MethodInfo.GetMethodFromHandle({2}.ResolveMethod({0}/*{3}*/).MethodHandle, {1})"
                    ctorSyntax = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("MethodInfo"),
                            SyntaxFactory.IdentifierName("GetMethodFromHandle")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    resolveMethodSyntax,
                                    SyntaxFactory.IdentifierName("MethodHandle"))),
                            SyntaxFactory.Argument(this.GetClosedGenericTypeHandleExpression(ctor.DeclaringType)))));
                }
                else
                {
                    // (ConstructorInfo){0}.ResolveMethod({1}/*{2}*/) 
                    ctorSyntax = resolveMethodSyntax;
                }

                ctorSyntax = SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                    SyntaxFactory.IdentifierName("ConstructorInfo"),
                    ctorSyntax));

                // (Type)ctor.Invoke(new object[] { ... })
                var invokeArg = GetObjectArrayArgument(arguments);
                var invokeExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ctorSyntax,
                        SyntaxFactory.IdentifierName("Invoke")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(invokeArg)));
                if (IsPublic(ctor.DeclaringType))
                {
                    var castExpression = SyntaxFactory.CastExpression(
                        this.GetTypeNameSyntax(ctor.DeclaringType),
                        invokeExpression);
                    return castExpression;
                }
                else
                {
                    return invokeExpression;
                }
            }
        }

        /// <summary>
        /// Creates a <c>new object[] { arg1, arg2 }</c> style syntax for a list of arguments.
        /// </summary>
        /// <param name="arguments">The list of arguments to format as an object array.</param>
        /// <returns>The object[] creation syntax.</returns>
        private static ArgumentSyntax GetObjectArrayArgument(params ExpressionSyntax[] arguments)
        {
            if (arguments.Length == 0)
            {
                return SyntaxFactory.Argument(SyntaxFactory.IdentifierName("EmptyObjectArray"));
            }
            else
            {
                return SyntaxFactory.Argument(SyntaxFactory.ArrayCreationExpression(
                    SyntaxFactory.ArrayType(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
                        SyntaxFactory.SingletonList<ArrayRankSpecifierSyntax>(
                            SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.OmittedArraySizeExpression())))),
                    SyntaxFactory.InitializerExpression(
                        SyntaxKind.ArrayInitializerExpression,
                        CodeGen.JoinSyntaxNodes(SyntaxKind.CommaToken, arguments))));
            }
        }

        private MethodDeclarationSyntax CreateInstantiatePartMethod(ComposedPart part)
        {
            var provisionalSharedObjectsIdentifier = SyntaxFactory.IdentifierName("provisionalSharedObjects");
            var partInstanceIdentifier = SyntaxFactory.IdentifierName("result");

            var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
                GetPartFactoryMethodName(part.Definition))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddParameterListParameters(SyntaxFactory.Parameter(provisionalSharedObjectsIdentifier.Identifier).WithType(dictionaryOfTypeObject));

            var statements = new List<StatementSyntax>();
            if (part.Definition.IsInstantiable)
            {
                // TPart result;
                statements.Add(SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        this.GetTypeNameSyntax(part.Definition.Type),
                        SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                            SyntaxFactory.VariableDeclarator(partInstanceIdentifier.Identifier)))));

                var localSymbols = new HashSet<string>();
                var block = SyntaxFactory.Block();
                int importingConstructorArgIndex = 0;
                var importingConstructorArgNames = new string[part.Definition.ImportingConstructor.Count];
                foreach (var pair in part.GetImportingConstructorImports())
                {
                    IReadOnlyList<StatementSyntax> prereqStatements;
                    var importSatisfyingExpression = this.GetImportSatisfyingExpression(pair.Key, pair.Value, provisionalSharedObjectsIdentifier, out prereqStatements);
                    if (prereqStatements.Count > 0)
                    {
                        block = block.AddStatements(prereqStatements.ToArray());
                    }

                    block = block.AddStatements(SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName("var"),
                            SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                SyntaxFactory.VariableDeclarator(
                                    importingConstructorArgNames[importingConstructorArgIndex++] = ReserveLocalVarName("arg", localSymbols))
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        importSatisfyingExpression))))));
                }

                block = block.AddStatements(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        partInstanceIdentifier,
                        this.ObjectCreationExpression(part.Definition, importingConstructorArgNames.Select(SyntaxFactory.IdentifierName).ToArray()))));

                statements.Add(block);


                if (typeof(IDisposable).IsAssignableFrom(part.Definition.Type))
                {
                    // this.TrackDisposableValue((IDisposable)result);
                    statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName("TrackDisposableValue"),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                            SyntaxFactory.CastExpression(
                                SyntaxFactory.IdentifierName("IDisposable"),
                                partInstanceIdentifier)))))));
                }

                if (part.Definition.IsShared)
                {
                    // provisionalSharedObjects.Add(partType, result);
                    var partTypeSyntax = this.GetTypeExpressionSyntax(part.Definition.Type);
                    statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            provisionalSharedObjectsIdentifier,
                            SyntaxFactory.IdentifierName("Add")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(partTypeSyntax),
                            SyntaxFactory.Argument(partInstanceIdentifier))))));
                }

                foreach (var satisfyingExport in part.SatisfyingExports.Where(i => i.Key.ImportingMember != null))
                {
                    statements.AddRange(this.GetImportSatisfyingAssignmentSyntax(satisfyingExport, provisionalSharedObjectsIdentifier));
                }

                if (part.Definition.OnImportsSatisfied != null)
                {
                    ExpressionSyntax receiver;
                    if (part.Definition.OnImportsSatisfied.DeclaringType.IsInterface)
                    {
                        var iface = SyntaxFactory.IdentifierName("onImportsSatisfiedInterface");
                        statements.Add(SyntaxFactory.LocalDeclarationStatement(
                            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                                .AddVariables(SyntaxFactory.VariableDeclarator(iface.Identifier).WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.CastExpression(
                                            this.GetTypeNameSyntax(part.Definition.OnImportsSatisfied.DeclaringType),
                                            partInstanceIdentifier))))));
                        receiver = iface;
                    }
                    else
                    {
                        receiver = partInstanceIdentifier;
                    }

                    statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            receiver,
                            SyntaxFactory.IdentifierName(part.Definition.OnImportsSatisfied.Name)),
                        SyntaxFactory.ArgumentList())));
                }

                // return result;
                statements.Add(SyntaxFactory.ReturnStatement(partInstanceIdentifier));
            }
            else
            {
                statements.Add(SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName("CannotInstantiatePartWithNoImportingConstructor"),
                        SyntaxFactory.ArgumentList())));
            }

            method = method.AddBodyStatements(statements.ToArray());

            return method;
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

        private ExpressionSyntax ExportFactoryCreationSyntax(ImportDefinitionBinding import, ExportDefinitionBinding export)
        {
            Requires.NotNull(import, "import");
            Requires.Argument(import.IsExportFactory, "import", "IsExportFactory is expected to be true.");
            Requires.NotNull(export, "export");

            // ExportFactory<T>.ctor(Func<Tuple<T, Action>>)
            // ExportFactory<T, TMetadata>.ctor(Func<Tuple<T, Action>>, TMetadata)
            var exportFactoryCtorArguments = new List<ExpressionSyntax>();

            // Prepare the export factory delegate.
            var statements = new List<StatementSyntax>();
            bool newSharingScope = import.ImportDefinition.ExportFactorySharingBoundaries.Count > 0;
            ExpressionSyntax scope;

            if (newSharingScope)
            {
                // var scope = new CompiledExportProvider(this, new [] { "sharing", "boundaries" });
                var scopeLocalVar = SyntaxFactory.IdentifierName("scope");
                statements.Add(
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName("var"),
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(scopeLocalVar.Identifier)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ObjectCreationExpression(
                                            SyntaxFactory.IdentifierName("CompiledExportProvider"),
                                            SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                                                SyntaxKind.CommaToken,
                                                SyntaxFactory.Argument(SyntaxFactory.ThisExpression()),
                                                SyntaxFactory.Argument(SyntaxFactory.ImplicitArrayCreationExpression(
                                                    SyntaxFactory.InitializerExpression(
                                                        SyntaxKind.ArrayInitializerExpression,
                                                        CodeGen.JoinSyntaxNodes<ExpressionSyntax>(SyntaxKind.CommaToken, import.ImportDefinition.ExportFactorySharingBoundaries.Select(sb => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(sb))).ToArray())))))),
                                            null)))))));
                scope = scopeLocalVar;
            }
            else
            {
                // var scope = this;
                scope = SyntaxFactory.ThisExpression();
            }

            // ILazy<T> part = GetOrCreateShareableValue(typeof(Part), ...);
            Type[] typeArgs = import.ImportingSiteElementType.GetGenericArguments();
            var partLocalVar = SyntaxFactory.IdentifierName("part");
            statements.Add(
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(partLocalVar.Identifier)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    GetPartInstanceLazy(export.PartDefinition, newDictionaryOfTypeObjectExpression, true, typeArgs, scope)))))));

            // var value = part.Value.SomeMember;
            var exportedValueLocalVar = SyntaxFactory.IdentifierName("value");
            statements.Add(
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var"),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(exportedValueLocalVar.Identifier)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    GetExportedValueFromPart(partLocalVar, import, export, ValueFactoryType.ActualValue)))))));

            ExpressionSyntax disposeReceiver = null;
            if (newSharingScope)
            {
                disposeReceiver = scope;
            }
            else if (typeof(IDisposable).IsAssignableFrom(export.PartDefinition.Type))
            {
                disposeReceiver = SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                    SyntaxFactory.IdentifierName("IDisposable"),
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, partLocalVar, SyntaxFactory.IdentifierName("Value"))));
            }

            ExpressionSyntax disposeAction = disposeReceiver != null
                ? (ExpressionSyntax)SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("Action"),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                        SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                disposeReceiver,
                                SyntaxFactory.IdentifierName("Dispose")),
                            SyntaxFactory.ArgumentList()))))),
                    null)
                : SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

            var tupleType = typeof(Tuple<,>).MakeGenericType(import.ImportingSiteElementType, typeof(Action));
            var tupleValue = !IsPublic(export.PartDefinition.Type, true) && IsPublic(tupleType, true)
                ? (ExpressionSyntax)SyntaxFactory.CastExpression(this.GetTypeNameSyntax(import.ImportingSiteElementType), exportedValueLocalVar)
                : exportedValueLocalVar;

            var tupleExpression = this.ObjectCreationExpression(
                tupleType.GetConstructors().Single(),
                new ExpressionSyntax[] { tupleValue, disposeAction });
            statements.Add(SyntaxFactory.ReturnStatement(tupleExpression));

            var tupleFactoryLambda = SyntaxFactory.ParenthesizedLambdaExpression(SyntaxFactory.Block(statements));
            if (IsPublic(import.ExportFactoryType, true))
            {
                exportFactoryCtorArguments.Add(tupleFactoryLambda);
            }
            else
            {
                // Since we'll be using reflection to pass in the tuple factory, we have to
                // explicitly give the lambda a delegate shape or the C# compiler won't know what to do with it.
                var tupleFactoryDelegate = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("ReflectionHelpers"),
                        SyntaxFactory.IdentifierName("CreateFuncOfType")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(this.GetTypeExpressionSyntax(tupleType)),
                        SyntaxFactory.Argument(tupleFactoryLambda))));
                exportFactoryCtorArguments.Add(tupleFactoryDelegate);
            }

            // Add the metadata argument if applicable.
            if (import.ExportFactoryType.GenericTypeArguments.Length > 1)
            {
                exportFactoryCtorArguments.Add(GetExportMetadata(export, import));
            }

            return this.ObjectCreationExpression(
                import.ExportFactoryType.GetConstructors().Single(),
                exportFactoryCtorArguments.ToArray());
        }

        private enum ValueFactoryType
        {
            ActualValue,
            LazyOfT,
            FuncOfObject,
        }

        private ExpressionSyntax ConvertValue(ExpressionSyntax value, TypeSyntax valueType, ValueFactoryType current, ValueFactoryType target)
        {
            switch (current)
            {
                case ValueFactoryType.ActualValue:
                    switch (target)
                    {
                        case ValueFactoryType.ActualValue:
                            return value;
                        case ValueFactoryType.LazyOfT:
                            // new Lazy<T>(() => value)
                            throw new NotImplementedException();
                        case ValueFactoryType.FuncOfObject:
                            // new Func<object>(() => value)
                            var lambda = SyntaxFactory.ParenthesizedLambdaExpression(value);
                            return SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.GenericName("Func").AddTypeArgumentListArguments(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword))),
                                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))),
                                null);
                        default:
                            throw new ArgumentOutOfRangeException("target");
                    }
                case ValueFactoryType.LazyOfT:
                    switch (target)
                    {
                        case ValueFactoryType.ActualValue:
                            // value.Value;
                            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, value, SyntaxFactory.IdentifierName("Value"));
                        case ValueFactoryType.LazyOfT:
                            return value;
                        case ValueFactoryType.FuncOfObject:
                            // value.ValueFactory;
                            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, value, SyntaxFactory.IdentifierName("ValueFactory"));
                        default:
                            throw new ArgumentOutOfRangeException("target");
                    }
                case ValueFactoryType.FuncOfObject:
                    switch (target)
                    {
                        case ValueFactoryType.ActualValue:
                            return SyntaxFactory.InvocationExpression(value, SyntaxFactory.ArgumentList());
                        case ValueFactoryType.LazyOfT:
                            // new Lazy<T>(value)
                            throw new NotImplementedException();
                        case ValueFactoryType.FuncOfObject:
                            return value;
                        default:
                            throw new ArgumentOutOfRangeException("target");
                    }
                default:
                    throw new ArgumentOutOfRangeException("current");
            }
        }

        private ExpressionSyntax GetExportedValueFromPart(ExpressionSyntax lazyPart, ImportDefinitionBinding import, ExportDefinitionBinding export, ValueFactoryType lazyType)
        {
            Requires.NotNull(lazyPart, "lazyPart");
            Requires.NotNull(import, "import");
            Requires.NotNull(export, "export");

            if (export.ExportingMember == null)
            {
                return this.ConvertValue(lazyPart, this.GetTypeNameSyntax(export.PartDefinition.Type), ValueFactoryType.LazyOfT, lazyType);
            }

            // To retrieve a member, we must have the actual instance that hosts it.
            var partInstance = this.ConvertValue(lazyPart, this.GetTypeNameSyntax(export.PartDefinition.Type), ValueFactoryType.LazyOfT, ValueFactoryType.ActualValue);

            var memberValue = CreateMemberRetrieval(export.ExportingMember, partInstance);
            switch (export.ExportingMember.MemberType)
            {
                case MemberTypes.Method:
                    var delegateType = typeof(Delegate).IsAssignableFrom(import.ImportingSiteElementType) ? import.ImportingSiteElementType : export.ExportedValueType;
                    if (IsPublic(delegateType, true) && IsPublic(export.ExportingMember, export.PartDefinition.Type))
                    {
                        memberValue = this.ObjectCreationExpression(
                            delegateType.GetConstructors().Single(),
                            new ExpressionSyntax[] { memberValue });
                    }
                    else
                    {
                        // memberValue.CreateDelegate(delegateType, partInstance)
                        memberValue = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                memberValue,
                                SyntaxFactory.IdentifierName("CreateDelegate")),
                            SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                                SyntaxKind.CommaToken,
                                SyntaxFactory.Argument(this.GetTypeExpressionSyntax(delegateType)),
                                SyntaxFactory.Argument(partInstance))));
                    }

                    break;
            }

            return this.ConvertValue(memberValue, this.GetTypeNameSyntax(import.ImportingSiteElementType), ValueFactoryType.ActualValue, lazyType);
        }

        private ExpressionSyntax GetImportAssignableValueForExport(ImportDefinitionBinding import, ExportDefinitionBinding export, ExpressionSyntax provisionalSharedObjects, ExpressionSyntax scope = null)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(export, "export");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            if (import.IsExportFactory)
            {
                return this.ExportFactoryCreationSyntax(import, export);
            }

            bool isNonSharedInstanceRequired = PartCreationPolicyConstraint.IsNonSharedInstanceRequired(import.ImportDefinition);
            Type[] typeArgs = import.ImportingSiteElementType.GetGenericArguments();
            var exportedValueSyntax = GetExportedValueFromPart(
                    GetPartInstanceLazy(export.PartDefinition, provisionalSharedObjects, isNonSharedInstanceRequired, typeArgs, scope),
                    import,
                    export,
                    import.IsLazy ? ValueFactoryType.FuncOfObject : ValueFactoryType.ActualValue);

            if (import.IsLazy)
            {
                var lazyExportedValueSyntax = CreateLazyConstruction(
                    import.ImportingSiteElementType,
                    exportedValueSyntax,
                    import.MetadataType,
                    GetExportMetadata(export, import));
                return lazyExportedValueSyntax;
            }
            else
            {
                return SyntaxFactory.CastExpression(
                    this.GetTypeNameSyntax(import.ImportingSiteElementType),
                    exportedValueSyntax);
            }
        }

        private MethodDeclarationSyntax CreateGetExportsCoreHelperMethod(IGrouping<string, ExportDefinitionBinding> exports, IdentifierNameSyntax importDefinition)
        {
            Requires.NotNull(exports, "exports");

            var exportExpressions = exports.Select(e => this.ExportCreationSyntax(e, importDefinition)).ToArray();

            var exportArrayType = SyntaxFactory.ArrayType(
                    SyntaxFactory.IdentifierName("Export"),
                    SyntaxFactory.SingletonList<ArrayRankSpecifierSyntax>(
                        SyntaxFactory.ArrayRankSpecifier(
                            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                SyntaxFactory.OmittedArraySizeExpression()))));

            var exportArrayExpression = SyntaxFactory.ArrayCreationExpression(exportArrayType)
                .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, CodeGen.JoinSyntaxNodes<ExpressionSyntax>(SyntaxKind.CommaToken, exportExpressions)));

            var method = SyntaxFactory.MethodDeclaration(
                exportArrayType,
                ReserveClassSymbolName("GetExportsCore_" + Utilities.MakeIdentifierNameSafe(exports.Key), null))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(importDefinition.Identifier)
                    .WithType(SyntaxFactory.IdentifierName("ImportDefinition")))
                .AddBodyStatements(SyntaxFactory.ReturnStatement(exportArrayExpression));
            return method;
        }

        private void EmitGetExportsReturnExpression(IGrouping<string, ExportDefinitionBinding> exports, string importDefinitionVarName)
        {
            using (Indent(4))
            {
                var method = CreateGetExportsCoreHelperMethod(exports, SyntaxFactory.IdentifierName(importDefinitionVarName));
                this.extraMembers.Add(method);
                var returnStatement = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName(method.Identifier),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("importDefinition"))))));
                this.WriteLine(returnStatement.NormalizeWhitespace().ToString());
            }
        }

        private ExpressionSyntax GetExportMetadata(ExportDefinitionBinding export, ImportDefinitionBinding import)
        {
            Requires.NotNull(export, "export");

            var metadataDictionary = this.GetExportMetadata(export);
            if (import == null || import.MetadataType == null || import.MetadataType == typeof(IDictionary<string, object>))
            {
                return metadataDictionary;
            }
            else if (import.MetadataType.IsInterface)
            {
                return SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName(GetClassNameForMetadataView(import.MetadataType)),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(metadataDictionary))),
                    null);
            }
            else
            {
                return this.ObjectCreationExpression(
                    import.MetadataType.GetConstructor(new Type[] { typeof(IDictionary<string, object>) }),
                    new ExpressionSyntax[] { metadataDictionary });
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
            string name = "Create" + part.Id;
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

        /// <summary>
        /// Creates an expression that creates a <see cref="LazyPart{T, TMetadata}"/> instance.
        /// </summary>
        /// <param name="valueType">The type for T.</param>
        /// <param name="valueFactory">The value factory, including the lambda when applicable.</param>
        /// <param name="metadataType">The type for TMetadata.</param>
        /// <param name="metadata">The metadata.</param>
        /// <returns>The object creation expression.</returns>
        private ExpressionSyntax CreateLazyConstruction(Type valueType, ExpressionSyntax valueFactory, Type metadataType, ExpressionSyntax metadata)
        {
            Requires.NotNull(valueType, "valueType");
            Requires.NotNull(valueFactory, "valueFactory");

            Type lazyTypeDefinition = metadataType != null ? typeof(LazyPart<,>) : typeof(LazyPart<>);
            Type[] lazyTypeArgs = metadataType != null ? new[] { valueType, metadataType } : new[] { valueType };
            Type lazyType = lazyTypeDefinition.MakeGenericType(lazyTypeArgs);
            ExpressionSyntax[] lazyArgs = metadataType == null ? new[] { valueFactory } : new[] { valueFactory, metadata };

            var ctor = lazyType.GetConstructors().First(c => c.GetParameters()[0].ParameterType.Equals(typeof(Func<object>)));
            var lazyConstruction = this.ObjectCreationExpression(ctor, lazyArgs);
            return lazyConstruction;
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

        private ExpressionSyntax ExportCreationSyntax(ExportDefinitionBinding export, IdentifierNameSyntax importDefinition)
        {
            Requires.NotNull(export, "export");
            Requires.NotNull(importDefinition, "importDefinition");

            bool isOpenGenericExport = export.ExportedValueType.ContainsGenericParameters;
            var partDefinition = export.PartDefinition;
            ExpressionSyntax partTypeExpression = this.GetTypeExpressionSyntax(partDefinition.Type, isOpenGenericExport);

            ExpressionSyntax partFactoryMethod;
            if (isOpenGenericExport)
            {
                partFactoryMethod = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(GetPartFactoryMethodNameNoTypeArgs(partDefinition)));
            }
            else
            {
                partFactoryMethod = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(GetPartFactoryMethodName(partDefinition)));
            }

            ExpressionSyntax sharingBoundary = partDefinition.IsShared
                ? SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(this.Configuration.GetEffectiveSharingBoundary(partDefinition)))
                : SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

            bool nonSharedInstanceRequired = !partDefinition.IsShared;

            ExpressionSyntax exportingMemberExpression = export.ExportingMember != null
                ? this.GetMemberInfoSyntax(export.ExportingMember)
                : SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

            var createExport = SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName("CreateExport"),
                SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                    SyntaxKind.CommaToken,
                    SyntaxFactory.Argument(importDefinition),
                    SyntaxFactory.Argument(GetExportMetadata(export)),
                    SyntaxFactory.Argument(partTypeExpression),
                    SyntaxFactory.Argument(partFactoryMethod),
                    SyntaxFactory.Argument(sharingBoundary),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(nonSharedInstanceRequired ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)),
                    SyntaxFactory.Argument(exportingMemberExpression))));

            return createExport;
        }

        /// <summary>
        /// Creates an expression that evaluates to an <see cref="ILazy{T}"/>.
        /// </summary>
        private ExpressionSyntax GetPartInstanceLazy(ComposablePartDefinition partDefinition, ExpressionSyntax provisionalSharedObjects, bool nonSharedInstanceRequired, IReadOnlyList<Type> typeArgs, ExpressionSyntax scope = null)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            Requires.NotNull(provisionalSharedObjects, "provisionalSharedObjects");

            // typeArgs may be supplied to us for an import of "Func<object>" even though the exporter is just a method.
            if (typeArgs == null || !partDefinition.Type.IsGenericTypeDefinition)
            {
                typeArgs = ImmutableList<Type>.Empty;
            }

            scope = scope ?? SyntaxFactory.ThisExpression();

            // Force the query to be for an isolated instance if the instance is never shared.
            nonSharedInstanceRequired |= !partDefinition.IsShared;

            if (partDefinition.Equals(ExportProvider.ExportProviderPartDefinition))
            {
                // Special case for our synthesized part that acts as a placeholder for *this* export provider.
                // this.NonDisposableWrapper
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    scope,
                    SyntaxFactory.IdentifierName("NonDisposableWrapper"));
            }

            Type partType = typeArgs.Count == 0 ? partDefinition.Type : partDefinition.Type.MakeGenericType(typeArgs.ToArray());
            ExpressionSyntax partTypeExpression = this.GetTypeExpressionSyntax(partType);
            SimpleNameSyntax partFactoryMethodName = SyntaxFactory.IdentifierName(GetPartFactoryMethodNameNoTypeArgs(partDefinition));
            ExpressionSyntax partFactoryMethod;
            bool publicInvocation = typeArgs.All(t => IsPublic(t, true));
            if (publicInvocation)
            {
                var typeArgsSyntax = typeArgs.Select(t => this.GetTypeNameSyntax(t)).ToArray();
                if (typeArgs.Count > 0)
                {
                    partFactoryMethodName = SyntaxFactory.GenericName(
                        partFactoryMethodName.Identifier,
                        SyntaxFactory.TypeArgumentList(CodeGen.JoinSyntaxNodes(SyntaxKind.CommaToken, typeArgsSyntax)));
                }

                // scope.CreateSomePart<T1, T2>
                partFactoryMethod = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        scope,
                        partFactoryMethodName);
            }
            else
            {
                var typeArgsExpressionSyntax = typeArgs.Select(t => this.GetTypeExpressionSyntax(t)).ToArray();

                // GetMethodWithArity("CreateSomePart", 2).MakeGenericMethod(typeof(T1), typeof(T2)).CreateDelegate(typeof(Func<Dictionary<Type, object>, object>), scope)
                var getMethodWithArity = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("GetMethodWithArity"),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(partFactoryMethodName.Identifier.ToString()))),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(typeArgs.Count))))));
                var makeGenericMethod = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        getMethodWithArity,
                        SyntaxFactory.IdentifierName("MakeGenericMethod")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(SyntaxKind.CommaToken, typeArgsExpressionSyntax.Select(SyntaxFactory.Argument).ToArray())));
                var funcOfDictionaryObject = SyntaxFactory.ParseTypeName("Func<Dictionary<Type, object>, object>");
                var createDelegate = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        makeGenericMethod,
                        SyntaxFactory.IdentifierName("CreateDelegate")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.TypeOfExpression(funcOfDictionaryObject)),
                        SyntaxFactory.Argument(scope))));
                partFactoryMethod = SyntaxFactory.CastExpression(funcOfDictionaryObject, createDelegate);
            }

            ExpressionSyntax sharingBoundary = partDefinition.IsShared
                ? SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(this.Configuration.GetEffectiveSharingBoundary(partDefinition)))
                : SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

            // this.GetOrCreateShareableValue(typeof(Part), this.CreatePart, pso, "sharingBoundaries", true)
            var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    scope,
                    SyntaxFactory.IdentifierName("GetOrCreateShareableValue")),
                SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                    SyntaxKind.CommaToken,
                    SyntaxFactory.Argument(partTypeExpression),
                    SyntaxFactory.Argument(partFactoryMethod),
                    SyntaxFactory.Argument(provisionalSharedObjects),
                    SyntaxFactory.Argument(sharingBoundary),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(nonSharedInstanceRequired ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)))));

            if (publicInvocation)
            {
                var invocationCast = SyntaxFactory.CastExpression(
                    SyntaxFactory.GenericName("ILazy").WithTypeArgumentList(SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(this.GetTypeNameSyntax(partType)))),
                    invocation);
                return SyntaxFactory.ParenthesizedExpression(invocationCast);
            }
            else
            {
                return invocation;
            }
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

        private string ReserveLocalVarName(string desiredName, HashSet<string> localSymbols = null)
        {
            localSymbols = localSymbols ?? this.localSymbols;

            if (localSymbols.Add(desiredName))
            {
                return desiredName;
            }

            int i = 0;
            string candidateName;
            do
            {
                i++;
                candidateName = desiredName + "_" + i.ToString(CultureInfo.InvariantCulture);
            } while (!localSymbols.Add(candidateName));

            return candidateName;
        }

        private string ReserveClassSymbolName(string shortName, object namedValue)
        {
            string result;
            if (namedValue != null && this.reservedSymbols.TryGetValue(namedValue, out result))
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

            if (namedValue != null)
            {
                this.reservedSymbols.Add(namedValue, result);
            }

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
