/*
 * Copyright (c) 2012-2019, Circonus, Inc. All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above
 *       copyright notice, this list of conditions and the following
 *       disclaimer in the documentation and/or other materials provided
 *       with the distribution.
 *     * Neither the name Circonus, Inc. nor the names of its contributors
 *       may be used to endorse or promote products derived from this
 *       software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Circonus.circllhist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace circllhistTest
{
    [TestClass]
    public class circllhistTest1
    {
        [TestMethod]
        public void Base64Test()
        {
            Histogram hist = new Histogram(64);
            Assert.IsNotNull(hist);
            hist.Insert(1.2, 1);
            Assert.AreEqual((UInt64)1, hist.SampleCount());
            hist.Insert(0.72, 1);
            Assert.AreEqual((UInt64)2, hist.SampleCount());
            var raw = hist.ToRaw();
            Assert.AreEqual(10, raw.Length);
            var b64 = hist.ToBase64String();
            Assert.AreEqual("AAJI/wABDAAAAQ==", b64);
            var hist2 = new Histogram(b64);
            Assert.AreEqual(hist2.ToBase64String(), hist.ToBase64String());
        }

        [TestMethod]
        public void MergeTest()
        {
            Histogram h1 = new Histogram();
            Histogram h2 = new Histogram();
            h1.Insert(14, -9, 1);
            h1.Insert(222, -9, 2);
            Histogram h1copy = new Histogram(h1);
            h2.Insert(228, -9, 1);
            h2.Insert(8e-9, 1);
            h1.Merge(h2);
            h2.Merge(h1copy);
            Assert.IsTrue(0 == h1.CompareTo(h2));

            HistogramBucketPair[] expected = new HistogramBucketPair[3];
            expected[0].count = 1;
            expected[0].bucket = new HistogramBucket(true, -9, 80);
            expected[1].count = 1;
            expected[1].bucket = new HistogramBucket(true, -8, 14);
            expected[2].count = 3;
            expected[2].bucket = new HistogramBucket(true, -7, 22);

            Assert.AreEqual((ushort)3, h1.BucketCount());
            Assert.AreEqual(expected[0], h1.Bucket(0));
            Assert.AreEqual(expected[1], h1.Bucket(1));
            Assert.AreEqual(expected[2], h1.Bucket(2));
        }
    }
}
