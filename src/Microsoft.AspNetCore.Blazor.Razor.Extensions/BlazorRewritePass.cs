// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp;
using AngleSharp.Html;
using AngleSharp.Parser.Html;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Blazor.Razor
{
    // Rewrites the standard IR to a format more suitable for Blazor
    //
    // HTML nodes are rewritten to contain more structure, instead of treating HTML as opaque content
    // it is structured into element/component nodes, and attribute nodes.
    internal class BlazorRewritePass : IntermediateNodePassBase, IRazorOptimizationPass
    {
        // Per the HTML spec, the following elements are inherently self-closing
        // For example, <img> is the same as <img /> (and therefore it cannot contain descendants)
        private readonly static HashSet<string> VoidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
        };

        // Run as early as possible
        public override int Order => int.MinValue;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            var visitor = new RewriteWalker();
            visitor.Visit(documentNode);
        }

        // Visits nodes then rewrites them using a post-order traversal. The result is that the tree
        // is rewritten bottom up.
        //
        // This relies on a few invariants Razor already provides for correctness.
        // - Tag Helpers are the only real nesting construct
        // - Tag Helpers require properly nested HTML inside their body
        //
        // This means that when we find a 'container' for HTML content, we have the guarantee
        // that the content is properly nested, except at the top level of scope. And since the top
        // level isn't nested inside anything, we can't introduce any errors due to misunderstanding
        // the structure.
        private class RewriteWalker : IntermediateNodeWalker
        {
            private bool FoundHtml { get; set; }

			public override void VisitHtml(HtmlContentIntermediateNode node)
            {
                FoundHtml = true;
            }

            public override void VisitDefault(IntermediateNode node)
            {
                base.VisitDefault(node);

                if (FoundHtml)
                {
                    RewriteChildren(node);
                    FoundHtml = false;
                }
            }

            private void RewriteChildren(IntermediateNode node)
            {
                // We expect all of the immediate children of a node (together) to comprise
                // a well-formed tree of elements and components. 
                var stack = new Stack<IntermediateNode>();
                stack.Push(node);

                // This can be non-null when we encounter HtmlAttributeIntermediateNodes inside
                // a of a tag:
                //
                //  <foo bar="17" baz="@baz" />
                //
                // This will lower like:
                //
                //  HtmlContent <foo bar="17"
                //  HtmlAttribute baz=" - "
                //      CSharpAttributeValue baz
                //  HtmlContent  />

                // Make a copy, we will clear and rebuild the child collection of this node.
                var children = node.Children.ToArray();
                node.Children.Clear();


                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i] is HtmlContentIntermediateNode htmlNode)
                    {
                        var content = string.Join("", htmlNode.Children.Cast<IntermediateToken>().Select(t => t.Content));
                        var tokenizer = new HtmlTokenizer(new TextSource(content), HtmlEntityService.Resolver);

                        HtmlToken token;
                        while ((token = tokenizer.Get()).Type != HtmlTokenType.EndOfFile)
                        {
                            switch (token.Type)
                            {
                                case HtmlTokenType.Character:
                                    {
                                        // Text content
                                        stack.Peek().Children.Add(new HtmlContentIntermediateNode()
                                        {
                                            Children =
                                        {
                                            new IntermediateToken() { Content = token.Data, Kind = TokenKind.Html, }
                                        }
                                        });
                                        break;
                                    }

                                case HtmlTokenType.StartTag:
                                    {
                                        var tag = token.Data;
                                        var elementNode = new HtmlElementIntermediateNode()
                                        {
                                            TagName = tag,
                                        };

                                        stack.Peek().Children.Add(elementNode);
                                        stack.Push(elementNode);

                                        break;
                                    }

                                case HtmlTokenType.EndTag:
                                    {
                                        var popped = stack.Pop();
                                        if (popped is HtmlElementIntermediateNode element)
                                        {
                                            if (element.TagName != token.Data)
                                            {
                                                // Unbalanced tag    
                                                throw null;
                                            }
                                        }
                                        else
                                        {
                                            // Unbalanced tag    
                                            throw null;
                                        }

                                        break;
                                    }

                                case HtmlTokenType.Comment:
                                    break;

                                default:
                                    throw new InvalidCastException($"Unsupported token type: {token.Type.ToString()}");
                            }
                        }

                    }
                    else if (children[i] is HtmlAttributeIntermediateNode htmlAttribute)
                    {

                    }
                    else
                    {
                        // not HTML, or already rewritten.
                        stack.Peek().Children.Add(children[i]);
                        i++;
                    }
                }

                if (stack.Peek() != node)
                {
                    // not balanced
                    throw null;
                }
            }
        }
    }
}