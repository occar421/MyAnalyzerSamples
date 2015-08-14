using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace PublicFieldAnalyzer
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PublicFieldCodeFixProvider)), Shared]
	public class PublicFieldCodeFixProvider : CodeFixProvider
	{
		private const string title = "Change field to properties";

		public sealed override ImmutableArray<string> FixableDiagnosticIds
		{
			get
			{
				return ImmutableArray.Create(PublicFieldAnalyzer.DiagnosticId);
			}
		}

		public sealed override FixAllProvider GetFixAllProvider()
		{
			return WellKnownFixAllProviders.BatchFixer;
		}

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			context.RegisterCodeFix(CodeAction.Create(
				title: title,
				createChangedDocument: c => MakePropertiesAsync(context.Document, root, diagnosticSpan, c),
				equivalenceKey: title), diagnostic);
		}

		private Task<Document> MakePropertiesAsync(Document document, SyntaxNode root, TextSpan span, CancellationToken cancellationToken)
		{
			var field = root.FindNode(span) as FieldDeclarationSyntax;

			var semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken);

			// { get; }
			var getAccessor = SyntaxFactory.AccessorList(SyntaxFactory.List(new[] {
				SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(semicolon),
			}));
			// { get; set; }
			var getSetAccessor = getAccessor.AddAccessors(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(semicolon));

			var propertySyntaxes = field.Declaration.Variables.Select(v =>
			{
				var isReadOnly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
				var accessors = isReadOnly ? getAccessor : getSetAccessor;

				var initializer = v.ChildNodes().OfType<EqualsValueClauseSyntax>().FirstOrDefault();
				var property = SyntaxFactory.PropertyDeclaration(
					attributeLists: field.AttributeLists,
					modifiers: isReadOnly ? field.Modifiers.Remove(field.Modifiers.Where(x => x.IsKind(SyntaxKind.ReadOnlyKeyword)).First()) : field.Modifiers,
					type: field.Declaration.Type,
					explicitInterfaceSpecifier: null,
					identifier: v.Identifier,
					accessorList: accessors,
					expressionBody: null,
					initializer: initializer
				);

				// int Foo { get; set; }\n  or  int Bar { get; set; } = 1;\n
				return (initializer != null ? property.WithSemicolonToken(semicolon) : property)
					.WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, Environment.NewLine))
					.WithAdditionalAnnotations(Formatter.Annotation);
			});

			var inserted = root.InsertNodesAfter(field, propertySyntaxes);
			var removingNode = inserted.FindNode(span);
			var newRoot = inserted.RemoveNode(removingNode, SyntaxRemoveOptions.KeepNoTrivia);
			return Task.FromResult(document.WithSyntaxRoot(newRoot));
		}
	}
}
