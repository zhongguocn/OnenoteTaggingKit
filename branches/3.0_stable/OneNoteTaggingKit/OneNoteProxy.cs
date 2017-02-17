﻿// Author: WetHat | (C) Copyright 2013 - 2017 WetHat Lab, all rights reserved
using Microsoft.Office.Interop.OneNote;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using WetHatLab.OneNote.TaggingKit.Tagger;

namespace WetHatLab.OneNote.TaggingKit
{
    /// <summary>
    /// Proxy class to make method calls into the OneNote application object which are
    /// protected against recoverable errors and offer a tagging specific API.
    /// </summary>
    public class OneNoteProxy : IDisposable
    {
        private const int MAX_RETRIES = 3; // number of retries if OneNote is busy
        private Application _on; // OneNote COM object

        public BackgroundTagger TaggingService { get; private set; }

        /// <summary>
        /// Create a new instance of a OneNote proxy.
        /// </summary>
        /// <param name="onenote">OneNote application object</param>
        internal OneNoteProxy(Application onenote)
        {
            _on = onenote;

            TaggingService = new BackgroundTagger(this);
            TaggingService.Run();
            TraceLogger.Log(TraceCategory.Info(), "OneNote application proxy constructed successfully");
            TraceLogger.Flush();
        }

        /// <summary>
        /// Delete a OneNote page object
        /// </summary>
        /// <remarks>Removes object such as outlines</remarks>
        /// <param name="pageID">  Page ID</param>
        /// <param name="objectID">ID of object on the page</param>
        public void DeletePageContent(string pageID, string objectID)
        {
            ExecuteMethodProtected<bool>(o =>
            {
                o.DeletePageContent(pageID, objectID, default(DateTime));
                return true;
            });
        }

        /// <summary>
        /// Get the ID of the current notebook
        /// </summary>
        public string CurrentNotebookID
        {
            get
            {
                try
                {
                    return CurrentWindow.CurrentNotebookId;
                }
                catch (COMException ex)
                {
                    TraceLogger.Log(TraceCategory.Info(), "Unable to determine Current Notebook ID: {0}", ex.Message);
                }
                return null;
            }
        }

        /// <summary>
        /// Get the id of the current OneNote page
        /// </summary>
        /// ///
        /// <value>null, if current page does not exist or could not be queried</value>
        public string CurrentPageID
        {
            get
            {
                try
                {
                    return CurrentWindow.CurrentPageId;
                }
                catch (COMException ex)
                {
                    TraceLogger.Log(TraceCategory.Info(), "Unable to determine Current Page ID: {0}", ex.Message);
                }
                return null;
            }
        }

        /// <summary>
        /// Get the ID of the current section group.
        /// </summary>
        /// <value>null in no current section group exists</value>
        public string CurrentSectionGroupID
        {
            get
            {
                try
                {
                    return CurrentWindow.CurrentSectionGroupId;
                }
                catch (COMException ex)
                {
                    TraceLogger.Log(TraceCategory.Info(), "Unable to determine Current Section Group ID: {0}", ex.Message);
                }
                return null;
            }
        }

        /// <summary>
        /// Get the id of the current OneNote section
        /// </summary>
        /// <value>null, if current section does not exist or could not be queried</value>
        public string CurrentSectionID
        {
            get
            {
                try
                {
                    return CurrentWindow.CurrentSectionId;
                }
                catch (COMException ex)
                {
                    TraceLogger.Log(TraceCategory.Info(), "Unable to determine Current Section ID: {0}", ex.Message);
                }
                return null;
            }
        }

        /// <summary>
        /// Get the current OneNote window.
        /// </summary>
        public Window CurrentWindow
        {
            get
            {
                return _on.Windows.CurrentWindow;
            }
        }

        private XMLSchema _schema = XMLSchema.xs2007;

        /// <summary>
        /// Get the highest version of the schema supported by the current OneNote notebook.
        /// </summary>
        internal XMLSchema OneNoteSchema
        {
            get
            {
                if (_schema == XMLSchema.xs2007)
                {
                    string contextID = CurrentNotebookID;
                    if (contextID == null)
                    {
                        contextID = CurrentSectionGroupID;
                        if (contextID == null)
                        {
                            contextID = CurrentSectionID;
                            if (contextID == null)
                            {
                                contextID = CurrentPageID;
                                if (contextID != null)
                                {
                                    TraceLogger.Log(TraceCategory.Info(), "Using current page to determine OneNote version");
                                }
                            }
                            else
                            {
                                TraceLogger.Log(TraceCategory.Info(), "Using current section to determine OneNote version");
                            }
                        }
                        else
                        {
                            TraceLogger.Log(TraceCategory.Info(), "Using current section group to determine OneNote version");
                        }
                    }
                    else
                    {
                        TraceLogger.Log(TraceCategory.Info(), "Using current notebook to determine OneNote version");
                    }

                    if (contextID != null)
                    {
                        foreach (XMLSchema s in new XMLSchema[] { XMLSchema.xs2013, XMLSchema.xs2010 })
                        {
                            try
                            {
                                _schema = ExecuteMethodProtected<XMLSchema>(o =>
                                {
                                    string outXml;
                                    o.GetHierarchy(contextID, HierarchyScope.hsSelf, out outXml, s);
                                    return s;
                                });

                                // we can use this schema
                                TraceLogger.Log(TraceCategory.Info(), "OneNote schema version is {0}", s);
                                break;
                            }
                            catch (COMException ce)
                            {
                                TraceLogger.Log(TraceCategory.Warning(), "Test of OneNote Schema Version {0} failed with {1}. Trying different version...", s, ce);
                                TraceLogger.Flush();
                            }
                        }
                    }

                    if (_schema == XMLSchema.xs2007)
                    {
                        _schema = XMLSchema.xsCurrent;
                        TraceLogger.Log(TraceCategory.Error(), "Schema detection failed! Trying to continue with default schema {0}", _schema);
                    }
                    TraceLogger.Flush();
                }

                return _schema;
            }
        }

        /// <summary>
        /// Find pages by full text search
        /// </summary>
        /// <param name="query">                query string</param>
        /// <param name="scopeID">              
        /// OneNote id of the scope to search for pages. This is the element ID of a
        /// notebook, section group, or section. If given as null or empty string scope is
        /// the entire set of notebooks open in OneNote.
        /// </param>
        /// <param name="includeUnindexedPages">
        /// true to include pages in the search which have not been indexed yet
        /// </param>
        public XDocument FindPages(string scopeID, string query, bool includeUnindexedPages = false)
        {
            TraceLogger.Log(TraceCategory.Info(), "query={0}; Scope = {1}; includeUnindexedPages = {2}", query, scopeID, includeUnindexedPages);

            return ExecuteMethodProtected<XDocument>(o =>
            {
                string outXml;
                o.FindPages(scopeID, query, out outXml, includeUnindexedPages, fDisplay: false, xsSchema: OneNoteSchema);
                return XDocument.Parse(outXml);
            });
        }

        /// <summary>
        /// Find OneNote pages which have meta-data with a given key.
        /// </summary>
        /// <param name="scopeID">              
        /// search scope. The id of a node in the hierarchy (notebook, section group, or
        /// section) below which to search for content. If null or empty string, the search
        /// scope is the entire set of notebooks open in OneNote. for the search.
        /// </param>
        /// <param name="metadataKey">          Key (name) of the meta-data</param>
        /// <param name="includeUnindexedPages">
        /// true to include pages not currently index by the Windows Search indexer
        /// </param>
        /// <returns>page descriptors of pages with the requested meta-data</returns>
        public XDocument FindPagesByMetadata(string scopeID, string metadataKey, bool includeUnindexedPages = false)
        {
            TraceLogger.Log(TraceCategory.Info(), "Scope = {0}; includeUnindexedPages = {1}", scopeID, includeUnindexedPages);

            return ExecuteMethodProtected<XDocument>(o =>
            {
                string outXml;
                o.FindMeta(scopeID, metadataKey, out outXml, includeUnindexedPages, OneNoteSchema);
                return XDocument.Parse(outXml);
            });
        }

        /// <summary>
        /// Get the XML descriptor of nodes in the OneNote hierarchy
        /// </summary>
        /// <remarks>Only basic information (as of OneNote 2010) is returned.</remarks>
        /// <param name="nodeID">id of the starting node</param>
        /// <param name="scope"> scope of the nodes to return</param>
        /// <returns>XML document describing the nodes in the OneNote hierarchy</returns>
        /// <exception cref="COMException">Call to OneNote failed</exception>
        public XDocument GetHierarchy(string nodeID, HierarchyScope scope)
        {
            TraceLogger.Log(TraceCategory.Info(), "Start Node = {0}; Scope = {1}", nodeID, scope);

            return ExecuteMethodProtected<XDocument>(o =>
            {
                string outXml;
                o.GetHierarchy(nodeID, scope, out outXml, OneNoteSchema);

                return XDocument.Parse(outXml);
            });
        }

        /// <summary>
        /// Get the hyper-link to a OneNote page or a paragraph on a OneNote page.
        /// </summary>
        /// <param name="pageID">      ID of the page</param>
        /// <param name="pageObjectID">
        /// ID of a paragraph on the OneNote page. If null or an empty string is given the
        /// hyper-link to the page is returned
        /// </param>
        /// <returns>hyper-link to page or paragraph on the page</returns>
        public string GetHyperlinkToObject(string pageID, string pageObjectID)
        {
            return ExecuteMethodProtected<string>(o =>
            {
                string link;

                o.GetHyperlinkToObject(pageID, pageObjectID, out link);
                return link;
            });
        }

        /// <summary>
        /// Get the content of a OneNote page as a XML document
        /// </summary>
        /// <param name="pageID">ID of the OneNote page</param>
        /// <returns>OneNote page XML document</returns>
        public XDocument GetPage(string pageID)
        {
            return ExecuteMethodProtected<XDocument>(o =>
            {
                string strPageContent;
                o.GetPageContent(pageID, out strPageContent, PageInfo.piBasic, OneNoteSchema);
                return XDocument.Parse(strPageContent);
            });
        }

        /// <summary>
        /// Navigate to a OneNote page or paragraph on a OneNote page.
        /// </summary>
        /// <param name="pageID">      ID of the OneNote page to navigate to.</param>
        /// <param name="pageObjectID">
        /// optional ID of a paragraph on a OneNote page or empty string
        /// </param>
        public void NavigateTo(string pageID, string pageObjectID = "")
        {
            ExecuteMethodProtected<bool>(o =>
            {
                _on.NavigateTo(pageID, pageObjectID, fNewWindow: false);
                return true;
            });
        }

        /// <summary>
        /// Update the content of a OneNote page.
        /// </summary>
        /// <param name="page">        OneNote page XML document</param>
        /// <param name="lastModified">time the document was last modified</param>
        public void UpdatePage(XDocument page, DateTime lastModified)
        {
            ExecuteMethodProtected(o =>
            {
                o.UpdatePageContent(page.ToString(), lastModified.ToUniversalTime(), OneNoteSchema);
                return true;
            });
        }

        /// <summary>
        /// Make a call into the OneNote application which is protected against recoverable errors.
        /// </summary>
        /// <param name="cmd">lambda function calling into the OneNote application object</param>
        /// <returns>Return value of the lambda function</returns>
        /// <typeparam name="Tresult">Return type of the protected lambda method</typeparam>
        /// <example>string id = onenote.ExecuteMethodProtected(o =&gt; {return o.Windows.CurrentWindow;});</example>
        private Tresult ExecuteMethodProtected<Tresult>(Func<Application, Tresult> cmd)
        {
            int retries = MAX_RETRIES;
            while (retries-- > 0)
            {
                try
                {
                    return cmd.Invoke(_on);
                }
                catch (COMException ce)
                {
                    if ((uint)ce.ErrorCode == 0x8001010A && retries >= 0)
                    { // RPC_E_SERVERCALL_RETRYLATER
                        TraceLogger.Log(TraceCategory.Info(), "OneNote busy. Retrying method invocation ...");
                        Thread.Sleep(1000); // wait until COM Server becomes responsive
                    }
                    else
                    {
                        TraceLogger.Log(TraceCategory.Error(), "Unrecoverable COM exception while executing OneNote method: {0}", ce.Message);
                        TraceLogger.Log(TraceCategory.Error(), ce.StackTrace);
                        TraceLogger.Log(TraceCategory.Error(), "Re-throwing exception");
                        throw;
                    }
                }
                catch (Exception e)
                {
                    TraceLogger.Log(TraceCategory.Error(), "Exception while executing OneNote method: {0}", e.Message);
                    TraceLogger.Log(TraceCategory.Error(), e.StackTrace);
                    throw;
                }
            }
            return default(Tresult);
        }

        #region IDisposable

        public void Dispose()
        {
            if (TaggingService != null)
            {
                TaggingService.Dispose();
                TaggingService = null;
                GC.SuppressFinalize(this);
            }
        }

        #endregion IDisposable
    }
}