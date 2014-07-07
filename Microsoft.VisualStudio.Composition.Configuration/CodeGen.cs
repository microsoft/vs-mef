namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Validation;

    internal static class CodeGen
    {
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

            var list = SyntaxFactory.NodeOrTokenList();
            foreach (T node in nodes)
            {
                if (list.Count > 0)
                {
                    list = list.Add(separatingToken);
                }

                list = list.Add(node);
            }

            return list;
        }
    }
}
