using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SimpleReflection.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SimpleReflection
{

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SimpleReflectionAnalyzer : DiagnosticAnalyzer
    {
        public const string SimpleReflectionIsNotReady = "SimpleReflectionIsNotReady";
        public const string SimpleReflectionUpdate = "SimpleReflectionUpdate";

        public static DiagnosticDescriptor SimpleReflectionIsNotReadyDescriptor = new DiagnosticDescriptor(
                   SimpleReflectionIsNotReady,
                   "Simple reflection is not ready.",
                   "Simple reflection is not ready.",
                   "Codegen",
                   DiagnosticSeverity.Error,
                   isEnabledByDefault: true,
                   "Simple reflection is not ready.");

        public static DiagnosticDescriptor SimpleReflectionUpdateDescriptor = new DiagnosticDescriptor(
                SimpleReflectionUpdate,
                "Simple reflection update.",
                "Simple reflection update.",
                "Codegen",
                DiagnosticSeverity.Info,
                isEnabledByDefault: true,
                "Simple reflection update.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(SimpleReflectionIsNotReadyDescriptor, SimpleReflectionUpdateDescriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterOperationAction(this.HandelBuilder, OperationKind.Invocation);
        }

        private void HandelBuilder(OperationAnalysisContext context)
        {
            if (context.Operation.Syntax is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is IdentifierNameSyntax methodName &&
                methodName.Identifier.ValueText == "GetBakedType"
                )
            {
                var semanticModel = context.Compilation
                    .GetSemanticModel(invocation.SyntaxTree);


                var typeInfo = semanticModel
                    .GetSpeculativeTypeInfo(memberAccess.Expression.SpanStart, memberAccess.Expression, SpeculativeBindingOption.BindAsExpression);

                var diagnosticProperties = ImmutableDictionary<string, string>.Empty.Add("type", typeInfo.Type.ToDisplayString());
                if (context.Compilation.GetTypeByMetadataName(typeInfo.Type.GetSimpleReflectionExtentionTypeName()) is INamedTypeSymbol extention)
                {
                    var updateDiagnostic = Diagnostic.Create(SimpleReflectionUpdateDescriptor,
                       methodName.GetLocation(),
                       diagnosticProperties);

                    context.ReportDiagnostic(updateDiagnostic);

                    return;
                }

                var diagnostic = Diagnostic.Create(SimpleReflectionIsNotReadyDescriptor,
                   methodName.GetLocation(),
                   diagnosticProperties);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
