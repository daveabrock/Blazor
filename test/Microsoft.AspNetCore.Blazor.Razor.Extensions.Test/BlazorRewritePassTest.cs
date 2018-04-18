// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;


namespace Microsoft.AspNetCore.Blazor.Razor
{
    public class BlazorRewritePassTest
    {
        public BlazorRewritePassTest()
        {
            Pass = new BlazorRewritePass();
            Engine = RazorProjectEngine.Create(
                BlazorExtensionInitializer.DefaultConfiguration, 
                RazorProjectFileSystem.Create(Environment.CurrentDirectory), 
                b =>
                {
                    b.Features.Add(Pass);
                }).Engine;
        }

        private RazorEngine Engine { get; }

        private BlazorRewritePass Pass { get; }

        [Fact]
        public void Execute_RewritesHtml_Basic()
        {
            // Arrange
            var document = CreateDocument(@"
<html>
  <head cool=""beans"">
  </head>
</html>");

            var documentNode = Lower(document);

            // Act
            Pass.Execute(document, documentNode);
            
            // Assert

        }

        [Fact]
        public void Execute_RewritesHtml_Mixed()
        {
            // Arrange
            var document = CreateDocument(@"
<html>
  <head cool=""beans"" csharp=@""yes"">
  </head>
</html>");

            var documentNode = Lower(document);

            // Act
            Pass.Execute(document, documentNode);

            // Assert

        }

        private RazorCodeDocument CreateDocument(string content)
        {
            var source = RazorSourceDocument.Create(content, "test.cshtml");
            return RazorCodeDocument.Create(source);
        }

        private DocumentIntermediateNode Lower(RazorCodeDocument codeDocument)
        {
            for (var i = 0; i < Engine.Phases.Count; i++)
            {
                var phase = Engine.Phases[i];
                if (phase is IRazorOptimizationPhase)
                {
                    break;
                }
                
                phase.Execute(codeDocument);
            }

            return codeDocument.GetDocumentIntermediateNode();
        }
    }
}