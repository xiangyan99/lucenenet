using Lucene.Net.Index;
using Lucene.Net.Util;
using System;

namespace Lucene.Net.Search
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// A base class for all collectors that return a <seealso cref="TopDocs"/> output. this
    /// collector allows easy extension by providing a single constructor which
    /// accepts a <seealso cref="PriorityQueue"/> as well as protected members for that
    /// priority queue and a counter of the number of total hits.<br>
    /// Extending classes can override any of the methods to provide their own
    /// implementation, as well as avoid the use of the priority queue entirely by
    /// passing null to <seealso cref="#TopDocsCollector(PriorityQueue)"/>. In that case
    /// however, you might want to consider overriding all methods, in order to avoid
    /// a NullPointerException.
    /// </summary>
#if FEATURE_SERIALIZABLE
    [Serializable]
#endif
    public abstract class TopDocsCollector<T> : ICollector, ITopDocsCollector where T : ScoreDoc
    {
        /// <summary>
        /// this is used in case topDocs() is called with illegal parameters, or there
        ///  simply aren't (enough) results.
        /// </summary>
        protected static readonly TopDocs EMPTY_TOPDOCS = new TopDocs(0, new ScoreDoc[0], float.NaN);

        /// <summary>
        /// The priority queue which holds the top documents. Note that different
        /// implementations of PriorityQueue give different meaning to 'top documents'.
        /// HitQueue for example aggregates the top scoring documents, while other PQ
        /// implementations may hold documents sorted by other criteria.
        /// </summary>
        protected PriorityQueue<T> m_pq;

        /// <summary>
        /// The total number of documents that the collector encountered. </summary>
        protected int m_totalHits;

        protected TopDocsCollector(PriorityQueue<T> pq)
        {
            this.m_pq = pq;
        }

        /// <summary>
        /// Populates the results array with the ScoreDoc instances. this can be
        /// overridden in case a different ScoreDoc type should be returned.
        /// </summary>
        protected virtual void PopulateResults(ScoreDoc[] results, int howMany)
        {
            for (int i = howMany - 1; i >= 0; i--)
            {
                results[i] = m_pq.Pop();
            }
        }

        /// <summary>
        /// Returns a <seealso cref="TopDocs"/> instance containing the given results. If
        /// <code>results</code> is null it means there are no results to return,
        /// either because there were 0 calls to collect() or because the arguments to
        /// topDocs were invalid.
        /// </summary>
        protected virtual TopDocs NewTopDocs(ScoreDoc[] results, int start)
        {
            return results == null ? EMPTY_TOPDOCS : new TopDocs(m_totalHits, results);
        }

        /// <summary>
        /// The total number of documents that matched this query. </summary>
        public virtual int TotalHits
        {
            get
            {
                return m_totalHits;
            }
            internal set
            {
                m_totalHits = value;
            }
        }

        /// <summary>
        /// The number of valid PQ entries 
        /// </summary>
        protected virtual int TopDocsCount
        {
            get
            {
                // In case pq was populated with sentinel values, there might be less
                // results than pq.size(). Therefore return all results until either
                // pq.size() or totalHits.
                return m_totalHits < m_pq.Count ? m_totalHits : m_pq.Count;
            }
        }

        /// <summary>
        /// Returns the top docs that were collected by this collector. </summary>
        public virtual TopDocs GetTopDocs()
        {
            // In case pq was populated with sentinel values, there might be less
            // results than pq.size(). Therefore return all results until either
            // pq.size() or totalHits.
            return GetTopDocs(0, TopDocsCount);
        }

        /// <summary>
        /// Returns the documents in the rage [start .. pq.size()) that were collected
        /// by this collector. Note that if start >= pq.size(), an empty TopDocs is
        /// returned.<br>
        /// this method is convenient to call if the application always asks for the
        /// last results, starting from the last 'page'.<br>
        /// <b>NOTE:</b> you cannot call this method more than once for each search
        /// execution. If you need to call it more than once, passing each time a
        /// different <code>start</code>, you should call <seealso cref="#topDocs()"/> and work
        /// with the returned <seealso cref="TopDocs"/> object, which will contain all the
        /// results this search execution collected.
        /// </summary>
        public virtual TopDocs GetTopDocs(int start)
        {
            // In case pq was populated with sentinel values, there might be less
            // results than pq.size(). Therefore return all results until either
            // pq.size() or totalHits.
            return GetTopDocs(start, TopDocsCount);
        }

        /// <summary>
        /// Returns the documents in the rage [start .. start+howMany) that were
        /// collected by this collector. Note that if start >= pq.size(), an empty
        /// TopDocs is returned, and if pq.size() - start &lt; howMany, then only the
        /// available documents in [start .. pq.size()) are returned.<br>
        /// this method is useful to call in case pagination of search results is
        /// allowed by the search application, as well as it attempts to optimize the
        /// memory used by allocating only as much as requested by howMany.<br>
        /// <b>NOTE:</b> you cannot call this method more than once for each search
        /// execution. If you need to call it more than once, passing each time a
        /// different range, you should call <seealso cref="#topDocs()"/> and work with the
        /// returned <seealso cref="TopDocs"/> object, which will contain all the results this
        /// search execution collected.
        /// </summary>
        public virtual TopDocs GetTopDocs(int start, int howMany)
        {
            // In case pq was populated with sentinel values, there might be less
            // results than pq.size(). Therefore return all results until either
            // pq.size() or totalHits.
            int size = TopDocsCount;

            // Don't bother to throw an exception, just return an empty TopDocs in case
            // the parameters are invalid or out of range.
            // TODO: shouldn't we throw IAE if apps give bad params here so they dont
            // have sneaky silent bugs?
            if (start < 0 || start >= size || howMany <= 0)
            {
                return NewTopDocs(null, start);
            }

            // We know that start < pqsize, so just fix howMany.
            howMany = Math.Min(size - start, howMany);
            ScoreDoc[] results = new ScoreDoc[howMany];

            // pq's pop() returns the 'least' element in the queue, therefore need
            // to discard the first ones, until we reach the requested range.
            // Note that this loop will usually not be executed, since the common usage
            // should be that the caller asks for the last howMany results. However it's
            // needed here for completeness.
            for (int i = m_pq.Count - start - howMany; i > 0; i--)
            {
                m_pq.Pop();
            }

            // Get the requested results from pq.
            PopulateResults(results, howMany);

            return NewTopDocs(results, start);
        }

        // LUCENENET specific - we need to implement these here, since our abstract base class
        // is now an interface.
        /// <summary>
        /// Called before successive calls to <see cref="Collect(int)"/>. Implementations
        /// that need the score of the current document (passed-in to
        /// <see cref="Collect(int)"/>), should save the passed-in <see cref="Scorer"/> and call
        /// scorer.Score() when needed.
        /// </summary>
        public abstract void SetScorer(Scorer scorer);

        /// <summary>
        /// Called once for every document matching a query, with the unbased document
        /// number.
        /// <para/>Note: The collection of the current segment can be terminated by throwing
        /// a <see cref="CollectionTerminatedException"/>. In this case, the last docs of the
        /// current <see cref="AtomicReaderContext"/> will be skipped and <see cref="IndexSearcher"/>
        /// will swallow the exception and continue collection with the next leaf.
        /// <para/>
        /// Note: this is called in an inner search loop. For good search performance,
        /// implementations of this method should not call <see cref="IndexSearcher.Doc(int)"/> or
        /// <see cref="Lucene.Net.Index.IndexReader.Document(int)"/> on every hit.
        /// Doing so can slow searches by an order of magnitude or more.
        /// </summary>
        public abstract void Collect(int doc);

        /// <summary>
        /// Called before collecting from each <see cref="AtomicReaderContext"/>. All doc ids in
        /// <see cref="Collect(int)"/> will correspond to <see cref="Index.IndexReaderContext.Reader"/>.
        ///
        /// Add <see cref="AtomicReaderContext#docBase"/> to the current <see cref="Index.IndexReaderContext.Reader"/>'s
        /// internal document id to re-base ids in <see cref="Collect(int)"/>.
        /// </summary>
        /// <param name="context">next atomic reader context </param>
        public abstract void SetNextReader(AtomicReaderContext context);

        /// <summary>
        /// Return <c>true</c> if this collector does not
        /// require the matching docIDs to be delivered in int sort
        /// order (smallest to largest) to <see cref="Collect"/>.
        ///
        /// <para> Most Lucene Query implementations will visit
        /// matching docIDs in order.  However, some queries
        /// (currently limited to certain cases of <see cref="BooleanQuery"/>) 
        /// can achieve faster searching if the
        /// <see cref="ICollector"/> allows them to deliver the
        /// docIDs out of order.</para>
        ///
        /// <para> Many collectors don't mind getting docIDs out of
        /// order, so it's important to return <c>true</c>
        /// here.</para>
        /// </summary>
        public abstract bool AcceptsDocsOutOfOrder { get; }
    }

    /// <summary>
    /// LUCENENET specific interface used to reference <see cref="TopDocsCollector{T}"/>
    /// without referencing its generic type.
    /// </summary>
    public interface ITopDocsCollector : ICollector
    {
        // From TopDocsCollector<T>
        int TotalHits { get; }
        TopDocs GetTopDocs();
        TopDocs GetTopDocs(int start);
        TopDocs GetTopDocs(int start, int howMany);
    }
}