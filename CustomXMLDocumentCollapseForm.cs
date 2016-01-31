using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
namespace Gekka.VisualStudio.Extention.CustomXMLDocumentCollapseForm
{
    [Export( typeof( IWpfTextViewCreationListener ) )]
    [ContentType( "CSharp" )]
    [ContentType( "Basic" )]
    [TextViewRole( PredefinedTextViewRoles.Document )]
    internal sealed class ViewListener : IWpfTextViewCreationListener
    {
        public static ViewListener Default { get; private set; }
        public ViewListener()
        {
            Default = this;
        }
        [Import]
        public IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService = null;
        [Import]
        public IOutliningManagerService OutliningManagerService = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView.TextBuffer.Properties.ContainsProperty( typeof( XMLDocumentTagger ) ))
            {
                XMLDocumentTagger b = (XMLDocumentTagger)textView.TextBuffer.Properties[typeof( XMLDocumentTagger )];
                lock (b.views)
                {
                    b.views.Add( textView );
                    textView.Closed += (s, e) =>
                    {
                        b.views.Remove( (IWpfTextView)s );
                    };
                }
            }
        }
    }

    [TagType( typeof( IOutliningRegionTag ) )]
    [ContentType( "CSharp" )]
    [ContentType( "Basic" )]
    [Export( typeof( ITaggerProvider ) )]
    class XMLDocumentTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            XMLDocumentTagger b;
            var t = typeof( XMLDocumentTagger );
            if (!buffer.Properties.ContainsProperty( t ))
            {
                b = new XMLDocumentTagger();
                buffer.Properties.AddProperty( t, b );
            }
            b = (XMLDocumentTagger)buffer.Properties[t];
            return b as ITagger<T>;
        }
    }

    class XMLDocumentTagger : ITagger<IOutliningRegionTag>
    {
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public List<IWpfTextView> views = new List<IWpfTextView>();

        private bool flag = false;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (flag)
            {
                yield break;
            }
            IWpfTextView view;
            lock (views) { view = this.views.FirstOrDefault(); }
            if (view == null)
            {
                yield break;
            }

            flag = true;
            IList<ICollapsible> regions;
            var om = ViewListener.Default.OutliningManagerService.GetOutliningManager( view );
            regions = om.GetAllRegions( spans ).ToArray();
            flag = false;

            string documentTagString = (view.TextBuffer.ContentType.TypeName == "CSharp") ? "///" : "'''";
            string SUMMARY = "<summary>";
            ITextSnapshot current = view.TextBuffer.CurrentSnapshot;

            foreach (ICollapsible ic in regions.Where
                ( _ => _.Tag != null
                 && _.Tag is IOutliningRegionTag && !(_.Tag is XMLDocumentOutliningRegionTag)
                 && _.Tag.CollapsedForm != null ))
            {
                var tag = (IOutliningRegionTag)ic.Tag;
                var s = tag.CollapsedForm.ToString();
                if (s.StartsWith( documentTagString ))
                {
                    System.IO.StringReader sr = new System.IO.StringReader( s );
                    string line = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        line = sr.ReadLine().TrimStart();
                        if (line.StartsWith( documentTagString ))
                        {
                            line = line.Substring( documentTagString.Length ).TrimStart();
                            if (line.StartsWith( SUMMARY ))
                            {
                                line = line.Substring( SUMMARY.Length ).TrimStart();
                            }
                        }
                        if (line.Length > 0)
                        {
                            break;
                        }
                    }
                    if (line.Length > 0)
                    {
                        XMLDocumentOutliningRegionTag ort = new XMLDocumentOutliningRegionTag( tag.IsDefaultCollapsed, tag.IsImplementation, line, tag.CollapsedHintForm );
                        var span = ic.Extent.GetSpan( current );
                        var tagSpan = new TagSpan<IOutliningRegionTag>( span, ort );
                        yield return tagSpan;
                    }
                }
            }
        }
    }

    class XMLDocumentOutliningRegionTag : OutliningRegionTag
    {
        public XMLDocumentOutliningRegionTag(bool isDefaultCollapsed, bool isImplementation, object collapsedForm, object collapsedHintForm)
            : base( isDefaultCollapsed, isImplementation, collapsedForm, collapsedHintForm )
        {
        }
    }
}
