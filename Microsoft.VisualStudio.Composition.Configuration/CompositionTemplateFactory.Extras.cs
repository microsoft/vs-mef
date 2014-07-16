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
                statement = this.CreateMemberAssignment(import.ImportingMember, value, partInstanceVar);
            }

            return statement;
        }

        private StatementSyntax CreateMemberAssignment(MemberInfo member, ExpressionSyntax value, IdentifierNameSyntax partInstanceVar)
        {
            Requires.NotNull(member, "member");
            Requires.NotNull(value, "value");
            Requires.NotNull(partInstanceVar, "partInstanceVar");

            ExpressionSyntax expression;
            var importingField = member as FieldInfo;
            var importingProperty = member as PropertyInfo;
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

        private ExpressionSyntax GetMethodInfoExpression(MethodInfo methodInfo)
        {
            Requires.NotNull(methodInfo, "methodInfo");

            // manifest.ResolveMethod(metadataToken/*description*/)
            var resolveMethod = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    this.GetManifestModuleSyntax(methodInfo.DeclaringType.Assembly),
                    SyntaxFactory.IdentifierName("ResolveMethod")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(methodInfo.MetadataToken))
                        .WithTrailingTrivia(SyntaxFactory.Comment("/*" + GetTypeName(methodInfo.DeclaringType, evenNonPublic: true) + "." + methodInfo.Name + "*/"))))));

            ExpressionSyntax methodInfoSyntax;
            if (methodInfo.DeclaringType.IsGenericType)
            {
                // MethodInfo.GetMethodFromHandle({0}.ResolveMethod({1}/*{3}*/).MethodHandle, {2})
                methodInfoSyntax = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("MethodInfo"), SyntaxFactory.IdentifierName("GetMethodFromHandle")),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, resolveMethod, SyntaxFactory.IdentifierName("MethodHandle"))),
                        SyntaxFactory.Argument(this.GetClosedGenericTypeHandleExpression(methodInfo.DeclaringType)))));
            }
            else
            {
                methodInfoSyntax = resolveMethod;
            }

            var castExpression = SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.CastExpression(
                    SyntaxFactory.IdentifierName("MethodInfo"),
                    methodInfoSyntax));
            return castExpression;
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

        private StatementSyntax[] GetImportSatisfyingAssignmentSyntax(KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExport)
        {
            Requires.Argument(satisfyingExport.Key.ImportingMember != null, "satisfyingExport", "No member to satisfy.");
            var import = satisfyingExport.Key;
            var importingMember = satisfyingExport.Key.ImportingMember;
            var exports = satisfyingExport.Value;
            var partInstanceVar = SyntaxFactory.IdentifierName(InstantiatedPartLocalVarName);

            IReadOnlyList<StatementSyntax> prereqs;
            var expression = GetImportSatisfyingExpression(import, exports, out prereqs);
            var statements = new List<StatementSyntax>(prereqs);
            statements.Add(CreateMemberAssignment(import, expression, partInstanceVar));
            return statements.ToArray();
        }

        private ExpressionSyntax GetImportSatisfyingExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                Type enumerableOfTType = typeof(IEnumerable<>).MakeGenericType(import.ImportingSiteTypeWithoutCollection);
                if (import.ImportingSiteType.IsArray || import.ImportingSiteType.IsEquivalentTo(enumerableOfTType))
                {
                    return this.GetSatisfyImportManyArrayExpression(import, exports, out prerequisiteStatements);
                }
                else
                {
                    return this.GetSatisfyImportManyCollectionExpression(import, exports, out prerequisiteStatements);
                }
            }
            else if (exports.Any())
            {
                prerequisiteStatements = ImmutableList<StatementSyntax>.Empty;
                return this.GetValueFactoryExpressionSyntax(import, exports.Single());
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

        private ExpressionSyntax GetSatisfyImportManyArrayExpression(ImportDefinitionBinding import, IEnumerable<ExportDefinitionBinding> exports, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            Requires.NotNull(import, "import");
            Requires.NotNull(exports, "exports");

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
                        exports.Select(export => this.GetValueFactoryExpressionSyntax(import, export)).ToArray())));
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
                            SyntaxFactory.Argument(this.GetValueFactoryExpressionSyntax(import, export)),
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(arrayIndex++))))))));
                }

                return localVar;
            }
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

        private ExpressionSyntax GetSatisfyImportManyCollectionExpression(ImportDefinitionBinding import, IReadOnlyList<ExportDefinitionBinding> exports, out IReadOnlyList<StatementSyntax> prerequisiteStatements)
        {
            Requires.NotNull(import, "import");

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
                if (stronglyTypedCollection)
                {
                    // tempVar.Add(export);
                    addExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, tempVar, SyntaxFactory.IdentifierName("Add")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(this.GetValueFactoryExpressionSyntax(import, export)))));
                }
                else
                {
                    // addMethodInfo.Invoke(tempVar, new object[] { export })
                    addExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetMethodInfoExpression(icollectionType.GetMethod("Add")), SyntaxFactory.IdentifierName("Invoke")),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(tempVar),
                            GetObjectArrayArgument(this.GetValueFactoryExpressionSyntax(import, export)))));
                }

                prereqs.Add(SyntaxFactory.ExpressionStatement(addExpression));
            }

            // (TCollection)tempVar
            var castExpression = SyntaxFactory.CastExpression(
                this.GetTypeNameSyntax(import.ImportingSiteType),
                tempVar);

            return castExpression;
        }

        private ExpressionSyntax GetValueFactoryExpressionSyntax(ImportDefinitionBinding import, ExportDefinitionBinding export, ExpressionSyntax provisionalSharedObjectsSyntax = null)
        {
            return SyntaxFactory.ParseExpression(GetValueFactoryExpression(import, export, provisionalSharedObjectsSyntax));
        }

        private string GetValueFactoryExpression(ImportDefinitionBinding import, ExportDefinitionBinding export, ExpressionSyntax provisionalSharedObjectsSyntax = null)
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
                    string provisionalSharedObjectsExpression;
                    if (provisionalSharedObjectsSyntax != null)
                    {
                        provisionalSharedObjectsExpression = provisionalSharedObjectsSyntax.NormalizeWhitespace().ToString();
                    }
                    else
                    {
                        provisionalSharedObjectsExpression = import.IsExportFactory
                            ? "new Dictionary<Type, object>()"
                            : "provisionalSharedObjects";
                    }

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
                            writer.Write("{0}(", GetPartFactoryMethodName(export.PartDefinition, false, genericTypeArgs.Select(GetTypeName).ToArray()));
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
                var castExpression = SyntaxFactory.CastExpression(
                    this.GetTypeNameSyntax(ctor.DeclaringType),
                    invokeExpression);
                return castExpression;
            }
        }

        /// <summary>
        /// Creates a <c>new object[] { arg1, arg2 }</c> style syntax for a list of arguments.
        /// </summary>
        /// <param name="arguments">The list of arguments to format as an object array.</param>
        /// <returns>The object[] creation syntax.</returns>
        private static ArgumentSyntax GetObjectArrayArgument(params ExpressionSyntax[] arguments)
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

        private MethodDeclarationSyntax CreateInstantiatePartMethod(ComposedPart part)
        {
            var provisionalSharedObjectsIdentifier = SyntaxFactory.IdentifierName("provisionalSharedObjects");
            var partInstanceIdentifier = SyntaxFactory.IdentifierName("result");

            var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
                GetPartFactoryMethodName(part.Definition, true))
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
                    var importSatisfyingExpression = this.GetImportSatisfyingExpression(pair.Key, pair.Value, out prereqStatements);
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
                    statements.AddRange(this.GetImportSatisfyingAssignmentSyntax(satisfyingExport));
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

        private void EmitInstantiatePart(ComposedPart part)
        {
            localSymbols.Clear();

            var createMethod = this.CreateInstantiatePartMethod(part);
            this.extraMembers.Add(createMethod);
            this.Write("return {0}(provisionalSharedObjects);", createMethod.Identifier);
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
                                GetFieldInfoExpressionSyntax((FieldInfo)export.ExportingMember));
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

        private MethodDeclarationSyntax CreateGetExportsCoreHelperMethod(IGrouping<string, ExportDefinitionBinding> exports)
        {
            Requires.NotNull(exports, "exports");

            var importDefinitionIdentifierName = SyntaxFactory.IdentifierName("importDefinition");

            var synthesizedImport = new ImportDefinitionBinding(
                new ImportDefinition(exports.Key, ImportCardinality.ZeroOrMore, ImmutableDictionary<string, object>.Empty, ImmutableList<IImportSatisfiabilityConstraint>.Empty),
                typeof(object));

            var newDictionaryTypeObjectExpression = SyntaxFactory.ObjectCreationExpression(
                dictionaryOfTypeObject,
                SyntaxFactory.ArgumentList(),
                null);

            var exportExpressions = new List<ExpressionSyntax>();
            foreach (var export in exports)
            {
                ExpressionSyntax valueFactoryExpression;
                if (export.ExportingMember == null && !export.PartDefinition.Type.IsGenericType)
                {
                    // GetValueFactoryFunc(GetOrCreate..., provisionalSharedObjects)
                    valueFactoryExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName("GetValueFactoryFunc"),
                        SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                            SyntaxKind.CommaToken,
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(GetPartFactoryMethodName(export.PartDefinition, false))),
                            SyntaxFactory.Argument(newDictionaryTypeObjectExpression))));
                }
                else
                {
                    // () => (GetOrCreate...).Value
                    var inner = this.GetValueFactoryExpressionSyntax(synthesizedImport, export, newDictionaryTypeObjectExpression);
                    valueFactoryExpression = SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.ParenthesizedExpression(inner),
                            SyntaxFactory.IdentifierName("Value")));
                }

                // new Export(importDefinition.ContractName, metadata, valueFactory)
                var exportExpression = SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("Export"),
                    SyntaxFactory.ArgumentList(CodeGen.JoinSyntaxNodes<ArgumentSyntax>(
                        SyntaxKind.CommaToken,
                        SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            importDefinitionIdentifierName,
                            SyntaxFactory.IdentifierName("ContractName"))),
                        SyntaxFactory.Argument(GetExportMetadata(export)),
                        SyntaxFactory.Argument(valueFactoryExpression))),
                    null);

                exportExpressions.Add(exportExpression);
            }

            var exportArrayType = SyntaxFactory.ArrayType(
                    SyntaxFactory.IdentifierName("Export"),
                    SyntaxFactory.SingletonList<ArrayRankSpecifierSyntax>(
                        SyntaxFactory.ArrayRankSpecifier(
                            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                SyntaxFactory.OmittedArraySizeExpression()))));

            var exportArrayExpression = SyntaxFactory.ArrayCreationExpression(exportArrayType)
                .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, CodeGen.JoinSyntaxNodes<ExpressionSyntax>(SyntaxKind.CommaToken, exportExpressions.ToArray())));

            var method = SyntaxFactory.MethodDeclaration(
                exportArrayType,
                ReserveClassSymbolName("GetExportsCore_" + Utilities.MakeIdentifierNameSafe(exports.Key), null))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(importDefinitionIdentifierName.Identifier)
                    .WithType(SyntaxFactory.IdentifierName("ImportDefinition")))
                .AddBodyStatements(SyntaxFactory.ReturnStatement(exportArrayExpression));
            return method;
        }

        private void EmitGetExportsReturnExpression(IGrouping<string, ExportDefinitionBinding> exports)
        {
            using (Indent(4))
            {
                var method = CreateGetExportsCoreHelperMethod(exports);
                this.extraMembers.Add(method);
                var returnStatement = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName(method.Identifier),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("importDefinition"))))));
                this.WriteLine(returnStatement.NormalizeWhitespace().ToString());
            }
        }

        private void WriteExportMetadataReference(ExportDefinitionBinding export, ImportDefinitionBinding import, TextWriter writer)
        {
            if (import.MetadataType != null)
            {
                writer.Write(", ");

                if (import.MetadataType == typeof(IDictionary<string, object>))
                {
                    writer.Write(GetExportMetadata(export).NormalizeWhitespace());
                }
                else if (import.MetadataType.IsInterface)
                {
                    writer.Write("new {0}(", GetClassNameForMetadataView(import.MetadataType));
                    writer.Write(GetExportMetadata(export).NormalizeWhitespace());
                    writer.Write(")");
                }
                else
                {
                    writer.Write(ObjectCreationExpression(
                        import.MetadataType.GetConstructor(new Type[] { typeof(IDictionary<string, object>) }),
                        new ExpressionSyntax[] { GetExportMetadata(export) }).NormalizeWhitespace());
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

        private static string GetPartFactoryMethodNameNoTypeArgs(ComposablePartDefinition part, bool createOnly)
        {
            Requires.NotNull(part, "part");
            string prefix = createOnly ? "Create" : "GetOrCreate";
            string name = prefix + part.Id;
            return name;
        }

        private static string GetPartFactoryMethodName(ComposablePartDefinition part, bool createOnly, params string[] typeArguments)
        {
            if (typeArguments == null || typeArguments.Length == 0)
            {
                typeArguments = part.Type.GetGenericArguments().Select(t => t.Name).ToArray();
            }

            string name = GetPartFactoryMethodNameNoTypeArgs(part, createOnly);

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
                return "this." + GetPartFactoryMethodName(part, false) + "(" + provisionalSharedObjectsExpression + ", " + (nonSharedInstanceRequired ? "true" : "false") + ")";
            }
        }

        private static string GetGenericPartFactoryMethodInfoExpression(ComposablePartDefinition part, string typeArgsParamsArrayExpression)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "this.GetMethodWithArity(\"{0}\", {1})"
                + ".MakeGenericMethod({2})",
                GetPartFactoryMethodNameNoTypeArgs(part, false),
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
                            GetFieldInfoExpressionSyntax((FieldInfo)member),
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
                var create = GetMethodInfoExpression(
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
