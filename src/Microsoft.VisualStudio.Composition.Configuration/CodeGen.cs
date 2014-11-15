namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal static class CodeGen
    {
        internal static ExpressionSyntax GetAssemblySyntax(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Assembly"),
                    SyntaxFactory.IdentifierName("Load")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(assembly.FullName))))));
        }

        internal static ObjectCreationExpressionSyntax WithNewKeywordTrivia(this ObjectCreationExpressionSyntax syntax)
        {
            return syntax
                .WithNewKeyword(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.NewKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)));
        }

        internal static ArrayCreationExpressionSyntax WithNewKeywordTrivia(this ArrayCreationExpressionSyntax syntax)
        {
            return syntax
                .WithNewKeyword(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.NewKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)));
        }

        internal static SeparatedSyntaxList<T> JoinSyntaxNodes<T>(SyntaxKind tokenDelimiter, params T[] nodes)
            where T : SyntaxNode
        {
            return SyntaxFactory.SeparatedList<T>(JoinSyntaxNodes<T>(SyntaxFactory.Token(tokenDelimiter), nodes));
        }

        internal static SyntaxNodeOrTokenList JoinSyntaxNodes<T>(SyntaxToken separatingToken, params T[] nodes)
            where T : SyntaxNode
        {
            Requires.NotNull(nodes, "nodes");

            switch (nodes.Length)
            {
                case 0:
                    return SyntaxFactory.NodeOrTokenList();
                case 1:
                    return SyntaxFactory.NodeOrTokenList(nodes[0]);
                default:
                    var nodesOrTokens = new SyntaxNodeOrToken[(nodes.Length * 2) - 1];
                    nodesOrTokens[0] = nodes[0];
                    for (int i = 1; i < nodes.Length; i++)
                    {
                        int targetIndex = i * 2;
                        nodesOrTokens[targetIndex - 1] = separatingToken;
                        nodesOrTokens[targetIndex] = nodes[i];
                    }

                    return SyntaxFactory.NodeOrTokenList(nodesOrTokens);
            }
        }
    }
}
