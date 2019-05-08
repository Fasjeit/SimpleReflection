using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using SimpleReflection.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleReflection
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SimpleReflactionCodeFixProvider)), Shared]
    public class SimpleReflactionCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(SimpleReflectionAnalyzer.SimpleReflectionIsNotReady, SimpleReflectionAnalyzer.SimpleReflectionUpdate);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();

            var title = diagnostic.Severity == DiagnosticSeverity.Error
                ? "Generate simple reflection"
                : "Recreate simple reflection";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: token => this.CreateFormatterAsync(context, diagnostic, token),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> CreateFormatterAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        {
            var typeName = diagnostic.Properties["type"];
            var currentDocument = context.Document;

            var model = await context.Document.GetSemanticModelAsync(token);
            var symbol = model.Compilation.GetTypeByMetadataName(typeName);

            var projectPath = Path.GetDirectoryName(context.Document.Project.FilePath);

            var symbolName = symbol.ToDisplayString();
            var rawSource = this.BuildSimpleReflection(symbol);
            var source = Formatter.Format(SyntaxFactory.ParseSyntaxTree(rawSource).GetRoot(), new AdhocWorkspace()).ToFullString();
            var fileName = $"{symbol.GetSimpleReflectionExtentionTypeName()}.cs";

            if (context.Document.Project.Documents.FirstOrDefault(o => o.Name == fileName) is Document document)
            {
                return document.WithText(SourceText.From(source));
            }

            var folders = new[] { "SimpeReflection" };

            return currentDocument.Project
                        .AddDocument(fileName, source)
                        .WithFolders(folders);

        }

        private string BuildSimpleReflection(INamedTypeSymbol symbol) => $@"
    using System;
    using System.Collections.Generic;

    // Simple reflection for {symbol.ToDisplayString()}
    public static class {symbol.GetSimpleReflectionExtentionTypeName()}
    {{
        private static Dictionary<string, Type> properties = new Dictionary<string, Type>
        {{
            { symbol
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .Where(o => (o.DeclaredAccessibility & Accessibility.Public) > 0)
                .Select(o => $@"{{ ""{o.Name}"", typeof({o.Type.ToDisplayString()})}},")
                .JoinWithNewLine() }
        }};

        public static Dictionary<string, Type> GetBakedType(this global::{symbol.ToDisplayString()} value)
        {{
            return properties;
        }}
    }} ";
    }
}
