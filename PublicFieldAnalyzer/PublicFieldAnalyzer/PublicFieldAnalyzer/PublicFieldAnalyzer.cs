﻿using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AnalyzerTests
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	class PublicFieldAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "PublicField";

		private static readonly string Title = "Public field sucks.";
		private static readonly string MessageFormat = "\"{0}\" is public field.";
		private static readonly string Description = "Using public field is usually bad implemention.";
		private const string Category = "PublicField.CSharp.Suggestion";

		internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, Description);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		{
			get
			{
				return ImmutableArray.Create(Rule);
			}
		}

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
		}

		private static void Analyze(SyntaxNodeAnalysisContext context)
		{
			var field = (FieldDeclarationSyntax)context.Node;

			if (field.Modifiers.Any(SyntaxKind.PublicKeyword) && !field.Modifiers.Any(SyntaxKind.ConstKeyword))
			{
				var fieldNameToken = field.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single()
					.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).Single();

				var diagnostic = Diagnostic.Create(Rule, field.GetLocation(), fieldNameToken.ValueText);
				context.ReportDiagnostic(diagnostic);
			}
		}
	}
}