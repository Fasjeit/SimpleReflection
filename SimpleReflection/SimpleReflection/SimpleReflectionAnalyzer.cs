using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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

        public static DiagnosticDescriptor SimpleReflectionIsNotReadyDescriptor = new DiagnosticDescriptor(
                   SimpleReflectionIsNotReady,
                   "Simple reflection is not ready.",
                   "Simple reflection is not ready.",
                   "Codegen",
                   DiagnosticSeverity.Error,
                   isEnabledByDefault: true,
                   "Simple reflection is not ready.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(SimpleReflectionIsNotReadyDescriptor);

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
                var typeInfo = context.Compilation
                    .GetSemanticModel(invocation.SyntaxTree)
                    .GetSpeculativeTypeInfo(memberAccess.Expression.SpanStart, memberAccess.Expression, SpeculativeBindingOption.BindAsExpression);

                var diagnosticProperties = ImmutableDictionary<string, string>.Empty.Add("type", typeInfo.Type.ToDisplayString());
                var diagnostic = Diagnostic.Create(SimpleReflectionIsNotReadyDescriptor,
                   methodName.GetLocation(),
                   diagnosticProperties);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
