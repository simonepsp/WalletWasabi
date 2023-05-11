using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.Fluent.Generators;

/// <summary>
/// Report an error if UiContext is referenced in the constructor directly without being closed on by a lambda expression.
/// UiContext cannot be referenced in constructor because it hasn't been initialized yet.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UiContextAnalyzer : DiagnosticAnalyzer
{
	public const string UiContextType = "WalletWasabi.Fluent.Models.UI.UiContext";
	public const string UiContextFileSuffix = "_UiContext.cs";

	private static readonly string[] ExcludedClasses = { "MainViewModel", "RoutableViewModel" };

	internal static readonly DiagnosticDescriptor Rule1 =
		new("WW001",
			"Do not use UiContext or Navigation APIs in ViewModel Constructor",
			"UiContext cannot be referenced in a ViewModel's constructor because it hasn't been initialized yet when constructor runs. Use OnNavigatedTo() or OnActivated() instead. Alternatively, make the constructor public and explicitly initialize UiContext. See https://github.com/zkSNACKs/WalletWasabi/blob/master/CONTRIBUTING.md#source-generated-viewmodel-constructors for details.",
			"Wasabi Wallet",
			DiagnosticSeverity.Error,
			true);

	internal static readonly DiagnosticDescriptor Rule2 =
		new("WW002",
			"Make ViewModel Constructor private",
			"This ViewModel Constructor must be made private, since the only valid public constructor is the autogenerated one.",
			"Wasabi Wallet",
			DiagnosticSeverity.Error,
			true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule1, Rule2);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration);
	}

	private static void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not ConstructorDeclarationSyntax ctor)
		{
			return;
		}

		if (context.Node.IsSourceGenerated())
		{
			return;
		}

		var isViewModel =
			ctor.Parent is ClassDeclarationSyntax cls &&
			cls.Identifier.ValueText is string className &&
			className.EndsWith("ViewModel") &&
			!ExcludedClasses.Contains(className);

		if (!isViewModel)
		{
			return;
		}

		var uiContextReferenceInConstructor =
			 ctor
			.GetUiContextReferences(context.SemanticModel)
			.Where(static x => x.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() != null)
			.Where(static x => x.FirstAncestorOrSelf<LambdaExpressionSyntax>() == null)
			.FirstOrDefault();

		// if constructor already has a UIContext parameter, leave it be. Don't raise any warnings.
		var ctorHasUiContextParameter =
			ctor.ParameterList.Parameters.Any(x => x.Type.IsUiContextType(context.SemanticModel));

		if (ctorHasUiContextParameter)
		{
			return;
		}

		if (uiContextReferenceInConstructor != null)
		{
			var location = uiContextReferenceInConstructor.GetLocation();
			var diagnostic = Diagnostic.Create(Rule1, location);
			context.ReportDiagnostic(diagnostic);
		}

		if (ctor.Parent is not ClassDeclarationSyntax classDeclaration)
		{
			return;
		}

		var uiContextReferencesInClass =
			classDeclaration.GetUiContextReferences(context.SemanticModel);

		if (uiContextReferencesInClass.Any() && !ctor.IsPrivate())
		{
			var location = ctor.GetLocation();
			var diagnostic = Diagnostic.Create(Rule2, location);
			context.ReportDiagnostic(diagnostic);
		}
	}
}
