﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WetHatLab.OneNote.TaggingKit.common;

namespace WetHatLab.OneNote.TaggingKit.find
{
    /// <summary>
    /// Contract vor view models supporting the <see cref="HitHighlightedPageLink"/> control.
    /// </summary>
    public interface IHitHighlightedPageLinkModel
    {
        /// <summary>
        /// Get a description of the matches of a query string against the page title.
        /// </summary>
        MatchCollection Matches { get; }

        /// <summary>
        /// Get the title of the OneNote page.
        /// </summary>
        string PageTitle { get; }
    }

    /// <summary>
    /// Sortable key to support the ranked display of <see cref="HitHighlightedPageLink"/> controls.
    /// </summary>
    public class HitHighlightedPageLinkKey : IComparable<HitHighlightedPageLinkKey>, IEquatable<HitHighlightedPageLinkKey>
    {

        private int _hits;

        private string _title;

        internal HitHighlightedPageLinkKey(string pageTitle, string pageId)
        {
            _title = pageTitle.ToLower();
            PageID = pageId;
        }

        /// <summary>
        /// Get the unique id of the OneNote page.
        /// </summary>
        public string PageID { get; private set; }

        /// <summary>
        /// Get number of hits of the query string against the page title
        /// </summary>
        internal int HitCount
        {
            set
            {
                _hits = value;
            }
        }
        #region IComparable<HitHighlightedPageLinkKey>

        /// <summary>
        /// compare this key with another key.
        /// </summary>
        /// <param name="other">the other key to compare with</param>
        /// <returns><list type="bullet">
        /// <item>a negative number, if this instance of the key comes before the other key</item>
        /// <item>0, if both keys are identical</item>
        /// <item>a positive number, if this instance of the key comes after the other key</item>
        /// </list> 
        /// </returns>
        /// <remarks>ordering takes into account the number of matches of the query against the page title</remarks>
        public int CompareTo(HitHighlightedPageLinkKey other)
        {
            int retval = 0;
            if (_hits < other._hits)
            {
                retval = 1;
            }
            else if (_hits > other._hits)
            {
                retval = -1;
            }

            if (retval == 0)
            {
               retval = _title.CompareTo(other._title); 
            }

            if (retval == 0)
            {
                retval = PageID.CompareTo(other.PageID);
            }

            return retval;
        }
        #endregion IComparable<HitHighlightedPageLinkKey>

        #region IEquatable<HitHighlightedPageLinkKey>

        /// <summary>
        /// Check keys for equality
        /// </summary>
        /// <param name="other">the other key to chack against</param>
        /// <returns>true if both keys are equal; false if they are not</returns>
        public bool Equals(HitHighlightedPageLinkKey other)
        {
            return PageID.Equals(other.PageID);
        }
        #endregion IEquatable<HitHighlightedPageLinkKey>
    }
    /// <summary>
    /// View model to support the <see cref="HitHighlightedPageLink"/> control.
    /// </summary>
    /// <remarks>The view model describes a link to a OneNote page returned from a search operation.
    /// <para>The search query is used to generate a hit highlighted rendering of a link to a OneNote page</para>
    /// </remarks>
    public class HitHighlightedPageLinkModel : HitHighlightedPageLinkKey, ISortableKeyedItem<HitHighlightedPageLinkKey,string>, IHitHighlightedPageLinkModel
    {
        private MatchCollection _matches;
        private TaggedPage _page;
        /// <summary>
        /// create a new instance of the view model.
        /// </summary>
        /// <param name="tp">a OneNote page object</param>
        /// <param name="pattern">regular expression describing the search query. Used for generating hit highlighting of the page link</param>
        internal HitHighlightedPageLinkModel(TaggedPage tp, Regex pattern) : base(tp.Title,tp.ID)
        {
            _page = tp;
            if (pattern != null)
            {
                _matches = pattern.Matches(PageTitle);
            }
            HitCount = Matches != null ? Matches.Count : 0;
            
        }

        /// <summary>
        /// Get the path to the page in the OneNote hierarchy
        /// </summary>
        public IEnumerable<HierarchyElement> Path
        {
            get
            {
                return _page.Path;
            }
        }

        #region IHitHighlightedPageLinkModel

        /// <summary>
        /// Get a description of the matches of the query with a OneNOte page
        /// </summary>
        public MatchCollection Matches
        {
            get
            {
                return _matches;
            }
        }
        /// <summary>
        /// Get the OneNote page title.
        /// </summary>
        public string PageTitle
        {
            get
            {
                return _page.Title;
            }
        }
        #endregion

        #region ISortableKeyedItem<HitHighlightedPageLinkKey,string>
        
        /// <summary>
        /// Get the unique key of the OneNote page
        /// </summary>
        public string Key
        {
            get { return PageID; }
        }

        /// <summary>
        /// Get the sorting key of the page.
        /// </summary>
        /// <remarks>Sort order is determined by the page title and the number of matches
        /// of the search query for this particular page.</remarks>
        public HitHighlightedPageLinkKey SortKey
        {
            get { return this;  }
        }

        #endregion ISortableKeyedItem<HitHighlightedPageLinkKey,string>
    }
}
