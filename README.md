Время от времени, когда я читал о Roslyn и его анализаторах, у меня постоянно возникала мысль: "А ведь этой штукой можно сделать nuget, который будет ходить по коду и делать кодогенерацию". Быстрый поиск не показал ничего интересного, по этому было принято решение копать. Как же я был приятно удивлен, когда обнаружил что моя затея не только реализуемая, но все это будет работать почти без костылей. 

И так кому интересно посмотреть на то как можно сделать "маленькую рефлексию" и запаковать ее в nuget прошу под кат.

 <cut/>

# Введение

 Думаю, первое что стоить уточнить это то, что понимается под "маленькой рефлексией". Я предлагаю реализовать для всех типов метод ``` Dictionary<string, Type> GetBakedType() ```, который будет возвращать имена пропертей и их типы. Поскольку это должно работать со всеми типами, то самым простым вариантом будет генерация метода расширения(extention method) для каждого типа. Ручная его реализация будет иметь следующий вид:
 ``` cs

using System;
using System.Collections.Generic;

public static class testSimpleReflectionUserExtentions
{
    private static Dictionary<string, Type> properties = new Dictionary<string, Type>
        {
            { "Id", typeof(System.Guid)},
            { "FirstName", typeof(string)},
            { "LastName", typeof(string)},
        };

    public static Dictionary<string, Type> GetBakedType(this global::testSimpleReflection.User value)
    {
        return properties;
    }
}

 ```

 Ничего сверхъестественного здесь нет, но реализовывать его для всех типов это муторное и не интересное дело которое, кроме того, грозит опечатками. Почему бы нам не попросить компилятор помочь. Вот тут на арену выходит Roslyn с его анализаторами. Они предоставляют возможность проанализировать код и изменить его. Так давайте же научим компилятор новому трюку. Пускай он ходит по коду и смотрит где мы используем, но еще не реализовали наш ``` GetBakedType ``` и реализовывает его. 

 Чтобы "включить" данный функционал нам нужно будет лишь установить один nuget пакет и все заработает сразу же. Далее просто вызываем ``` GetBakedType ``` там где нужно, получаем ошибку компиляции которая говорит что рефлексия для данного типа еще не готова, вызываем codefix и все готово. У нас есть метод расширения который вернет нам все публичные проперти. 
 
 Думаю в движении будет проще понять как оно вообще работает, вот короткая визуализация:

<oembed>https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3DC9O1413fHac&format=json</oembed>

 Кому интересно попробовать это локально, можно установить nuget пакет под именем SimpleReflection:
```
Install-Package SimpleReflection
```

Кому интересны исходники, они лежат [тут](https://github.com/byme8/SimpleReflection).

 Хочу предупредить данная реализация не рассчитана на реальное применение. Я лишь хочу показать способ для организации кодогенерации при помощи Roslyn.
 

# Предварительная подготовка

Перед тем как начать делать свои анализаторы необходимо установить компонент 'Visual Studio extention development' в студийном Installer-е. Для VS 2019 нужно не забыть выбрать ".NET Compiler Platform SDK" как опциональный компонент.


# Реализация анализатора

Я не буду поэтапно описывать как реализовать анализатор, поскольку он ну очень прост, а лишь пройдусь по ключевым моментам. 

И первым ключевым моментом станет то, что если у нас есть настоящая ошибка компиляции, то анализаторы не запускаются вовсе. Как результат, если мы попытаемся вызвать наш ``` GetBakedType() ``` в контексте типа для которого он не реализован, то получим ошибку компиляции и все наши старания не будут иметь смысла. Но тут нам поможет знание о том с каким приоритетом компилятор вызывает методы расширения. Весь сок в том, что конкретные реализации имеют больший приоритет чем универсальные методы(generic method). То есть в следующем примере будет вызван второй метод, а не первый:

``` cs

public static class SomeExtentions
{
    public static void Save<T>(this T value)
    {
        ...
    }

    public static void Save(this User user)
    {
        ...
    }
}

public class Program 
{
    public static void Main(string[] args)
    {
        var user = new User();
        user.Save();
    }
}
```

Данная особенность очень кстати. Мы просто определим универсальный ``` GetBakedType ``` следующим образом:

``` cs
using System;
using System.Collections.Generic;

public static class StubExtention
{
    public static Dictionary<string, Type> GetBakedType<TValue>(this TValue value)
    {
        return new Dictionary<string, Type>();
    }
}
```

Это позволит нам избежать ошибки компиляции в самом начале и сгенерировать нашу собственную "ошибку" компиляции.

Рассмотрим сам анализатор. Он будет предлагать две диагностики. Первая отвечает за случай когда кодогенерация вообще не запускалась, а вторая тогда когда нам нужно обновить уже существующий код. Они будут иметь следующие названия ``` SimpleReflectionIsNotReady ``` и ``` SimpleReflectionUpdate ``` соответственно. Первая диагностика будет генерировать "ошибку" компиляции, а вторая лишь сообщать о том что здесь можно запустить кодогенерацию повторно. 

Описание диагностик имеет следующий вид:

``` cs
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
```

Далее необходимо определится что мы вообще будем искать, в данном случаи это будет вызов метода:
``` cs
public override void Initialize(AnalysisContext context)
{
    context.RegisterOperationAction(this.HandelBuilder, OperationKind.Invocation);
}
```

Потом уже в ``` HandelBuilder ``` идет анализ синтаксического дерева. На вход мы будем получать все вызовы которые были найдены, поэтому необходимо отсеять все кроме нашего ``` GetBakedType ```. Сделать это можно обычным ``` if ``` в котором мы проверим имя метода. Дальше достаем тип переменной над которой вызывается наш метод и сообщаем компилятору о результатах нашего анализа. Это может быть ошибка компиляции, если кодогенерация пока не запускалась или возможность ее перезапустить. 

Все это выглядит следующим образом:

``` cs
private void HandelBuilder(OperationAnalysisContext context)
{
    if (context.Operation.Syntax is InvocationExpressionSyntax invocation &&
        invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
        memberAccess.Name is IdentifierNameSyntax methodName &&
        methodName.Identifier.ValueText == "GetBakedType")
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
```

<spoiler title="Полный код анализатора">

``` cs

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
```
</spoiler>

# Реализация кодогенератора

Кодогенерацию мы будем делать через ``` CodeFixProvider ```, который подписан на наш анализатор. В первую очередь нам необходимо проверить что получилось найти нашему анализатору.

Выглядит это следующим образом:

``` cs
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
```

Вся магия происходит внутри ``` CreateFormatterAsync ```. В нем мы достаем полное описание типа. После чего стартуем кодогенерацию и добвляем новый файл в проект.

Получение информации и добаление файла:

``` cs
 private async Task<Document> CreateFormatterAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
{
    var typeName = diagnostic.Properties["type"];
    var currentDocument = context.Document;

    var model = await context.Document.GetSemanticModelAsync(token);
    var symbol = model.Compilation.GetTypeByMetadataName(typeName);

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
```

Сообствено кодогенерация(подозреаю что хабр сломает всю подвсетку):

``` cs
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
.Select(o => $@"            {{ ""{o.Name}"", typeof({o.Type.ToDisplayString()})}},")
.JoinWithNewLine() }
    }};

    public static Dictionary<string, Type> GetBakedType(this global::{symbol.ToDisplayString()} value)
    {{
        return properties;
    }}
}} ";
}
```
<spoiler title="Полный код кодогенератора">


``` cs
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SimpleReflectionCodeFixProvider)), Shared]
    public class SimpleReflectionCodeFixProvider : CodeFixProvider
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
    .Select(o => $@"            {{ ""{o.Name}"", typeof({o.Type.ToDisplayString()})}},")
    .JoinWithNewLine() }
        }};

        public static Dictionary<string, Type> GetBakedType(this global::{symbol.ToDisplayString()} value)
        {{
            return properties;
        }}
    }} ";
    }
}
```

</spoiler>

# Итоги

В результате у нас получился Roslyn анализатор-кодогенератор при помощи которого реализовуется "маленькая" рефлексия с использованием кодогенерации. Будет сложно придумать реальное применение текущей библиотеке, но она будет прекрасным примером для реализации легко доступных кодогенераторов. Данный подход может быть, как и любая кодогенерация, полезен для написания сериализаторов. Моя тестовая реализация MessagePack-а работала на ~20% быстрее чем [neuecc/MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp), а более быстрого сериализатора я пока не видал. Кроме того данный подход не требует ``` Roslyn.Emit ```, что прекрасно подходит для Unity и AOT сценариях.
