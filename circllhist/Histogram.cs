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
 
using System;
using System.Diagnostics;
using System.IO;

namespace Circonus.circllhist
{
    public struct HistogramBucket : IComparable<HistogramBucket>
    {
        public sbyte exp;
        public sbyte val;
        public bool Isnan()
        {
            int aval = Math.Abs(val);
            if (99 < aval) return true; // in [100... ]: nan
            if (9 < aval) return false; // in [10 - 99]: valid range
            if (0 < aval) return true; // in [1  - 9 ]: nan
            if (0 == aval) return false; // in [0]:       zero bucket
            Trace.Assert(false);
            return false;
        }
        public double ToDouble()
        {
            if (Isnan()) return Double.NaN;
            if (val == 0) return 0.0;
            return ((double)val) / 10.0 * Helpers.Power_of_ten[unchecked((byte)exp)];
        }
        public double BinWidth()
        {
            if (Isnan()) return Double.NaN;
            if (val == 0) return 0;
            return Helpers.Power_of_ten[unchecked((byte)exp)] / 10.0;
        }
        public double Midpoint()
        {
            double bottom, top, interval, ratio;
            if (Isnan()) return Double.NaN;
            if (val == 0) return 0;
            bottom = ToDouble();
            interval = BinWidth();
            if (bottom < 0) interval *= -1.0;
            top = bottom + interval;
            ratio = (bottom) / (bottom + top);
            return bottom + interval * ratio;
        }
        public HistogramBucket(bool unused, sbyte in_exp, sbyte in_val)
        {
            exp = in_exp;
            val = in_val;
        }
        public HistogramBucket(Int64 value, int scale)
        {
            exp = 0;
            val = 0;
            sbyte sign = 1;
            if (value == 0) return;
            scale++;
            if (value < 0)
            {
                if (value == Int64.MinValue) value = Int64.MaxValue;
                else value = 0 - value;
                sign = -1;
            }
            if (value < 10)
            {
                value *= 10;
                scale -= 1;
            }
            while (value >= 100)
            {
                value /= 10;
                scale++;
            }
            if (scale < -128) return;
            if (scale > 127)
            {
                exp = unchecked((sbyte)0xff);
                return;
            }
            val = (sbyte)(sign * (sbyte)value);
            exp = (sbyte)scale;
        }

        public HistogramBucket(double d)
        {
            exp = unchecked((sbyte)0xff);
            val = 0;
            if (Double.IsNaN(d) || Double.IsInfinity(d)) return;
            if (d == 0)
            {
                val = 0;
                return;
            }
            int big_exp;
            int sign = (d < 0) ? -1 : 1;
            d = Math.Abs(d);
            big_exp = (int)Math.Floor(Math.Log10(d));
            exp = (sbyte)big_exp;
            if ((int)exp != big_exp)
            { /* we rolled */
                if (big_exp >= 0)
                {
                    val = unchecked((sbyte)0xff);
                    exp = 0;
                    return;
                }
                val = 0;
                exp = 0;
                return;
            }
            d /= Helpers.Power_of_ten[unchecked((byte)exp)];
            d *= 10;
            // avoid rounding problem at the bucket boundary
            // e.g. d=0.11 results in hb.val = 10 (should be 11)
            // by allowing an error margin (in the order or magnitude
            // of the expected rounding errors of the above transformations)
            val = (sbyte)(sign * (int)Math.Floor(d + 1e-13));
            if ((val == 100 || val == -100))
            {
                if (exp < 127)
                {
                    val /= 10;
                    exp++;
                }
                else
                { // can't increase exponent. Return NaN
                    val = unchecked((sbyte)0xff);
                    exp = 0;
                    return;
                }
            }
            if ((val == 0))
            {
                exp = 0;
                return;
            }
            if ((!((val >= 10 && val < 100) ||
                        (val <= -10 && val > -100))))
            {
                val = unchecked((sbyte)0xff);
                exp = 0;
                return;
            }
        }
        public int CompareTo(HistogramBucket o)
        {
            if (exp == o.exp && val == o.val) return 0;
            /* place NaNs at the beginning always */
            if (Isnan()) return 1;
            if (o.Isnan()) return -1;
            /* zero values need special treatment */
            if (val == 0) return (o.val > 0) ? 1 : -1;
            if (o.val == 0) return (val < 0) ? 1 : -1;
            /* opposite signs? */
            if (val < 0 && o.val > 0) return 1;
            if (val > 0 && o.val < 0) return -1;
            /* here they are either both positive or both negative */
            if (exp == o.exp) return (val < o.val) ? 1 : -1;
            if (exp > o.exp) return (val < 0) ? 1 : -1;
            if (exp < o.exp) return (val < 0) ? -1 : 1;
            /* unreachable */
            return 0;
        }
    }
    public struct HistogramBucketPair
    {
        public HistogramBucket bucket;
        public ulong count;
        public uint Write(MemoryStream mem)
        {
            byte tgt_type = 7;
            for (byte i = 0; i < 7; i++)
                if (count <= Helpers.Blimits[i])
                {
                    tgt_type = i;
                    break;
                }
            mem.WriteByte(unchecked((byte)bucket.val));
            mem.WriteByte(unchecked((byte)bucket.exp));
            mem.WriteByte(tgt_type);
            for (int i = (int)tgt_type; i >= 0; i--)
            {
                mem.WriteByte((byte)((count >> (i * 8)) & 0xff));
            }
            return (uint)3 + tgt_type + 1;
        }
    }
    public class Histogram : IComparable<Histogram>
    {
        public const int DEFAULT_HIST_SIZE = 64;
        private ushort allocd;
        private ushort used;
        private HistogramBucketPair[] bvs;

        public Histogram(ushort size)
        {
            allocd = size;
            used = 0;
            bvs = new HistogramBucketPair[allocd];
        }
        public Histogram()
        {
            allocd = used = 0;
            bvs = null;
        }
        public Histogram(byte[] raw)
        {
            uint read = 0;
            ushort cnt = 0;
            allocd = used = 0;
            if (raw.Length < 2) return;
            cnt = (ushort)((ushort)raw[0] << 8);
            cnt |= (ushort)raw[1];
            read = 2;
            HistogramBucketPair[] newbvs = new HistogramBucketPair[cnt];
            for (ushort i = 0; i < cnt; i++)
            {
                if (raw.Length < read + 4) return;
                if (raw.Length < read + 4 + raw[read + 2]) return;
                newbvs[i].bucket = new HistogramBucket(true,
                        unchecked((sbyte)raw[read + 1]),
                        unchecked((sbyte)raw[read]));
                newbvs[i].count = 0;
                for (byte j = 0; j <= raw[read + 2]; j++)
                {
                    newbvs[i].count = (newbvs[i].count << 8) | (ulong)raw[read + 3 + j];
                }
                read += 4 + (uint)raw[read + 2];
            }
            bvs = newbvs;
            allocd = used = cnt;
        }
        public Histogram(string b64) : this(System.Convert.FromBase64String(b64)) { }
        public Histogram(Histogram src)
        {
            used = allocd = src.used;
            bvs = new HistogramBucketPair[used];
            Array.Copy(src.bvs, 0, bvs, 0, used);
        }

        private bool InternalFind(HistogramBucket hb, out int idx)
        {
            /* This is a simple binary search returning the idx in which
             * the specified bucket belongs... returning true if it is there
             * or false if the value would need to be inserted here (moving the
             * rest of the buckets forward one).
             */
            int rv = -1, l = 0, r = used - 1;
            idx = 0;
            if (used == 0) return false;
            while (l < r)
            {
                int check = (r + l) / 2;
                rv = bvs[check].bucket.CompareTo(hb);
                if (rv == 0) l = r = check;
                else if (rv > 0) l = check + 1;
                else r = check - 1;
            }
            /* if rv == 0 we found a match, no need to compare again */
            if (rv != 0) rv = bvs[l].bucket.CompareTo(hb);
            idx = l;
            if (rv == 0) return true;   /* this is it */
            if (rv < 0) return false;    /* it goes here (before) */
            idx++;               /* it goes after here */
            return false;
        }
        public ushort BucketCount() => used;
        public HistogramBucketPair Bucket(ushort idx) => bvs[idx];
        public ulong Insert(long val, int scale, ulong count)
        {
            return Insert(new HistogramBucket(val, scale), count);
        }
        public ulong Insert(double val, ulong count)
        {
            return Insert(new HistogramBucket(val), count);
        }
        public ulong Insert(HistogramBucket hb, ulong count)
        {
            bool found;
            int idx;
            if (bvs == null)
            {
                bvs = new HistogramBucketPair[DEFAULT_HIST_SIZE];
                allocd = DEFAULT_HIST_SIZE;
            }
            found = InternalFind(hb, out idx);
            if (!found)
            {
                if (used == allocd)
                {
                    /* A resize is required */
                    HistogramBucketPair[] newbvs = new HistogramBucketPair[allocd + DEFAULT_HIST_SIZE];
                    if (idx > 0)
                        Array.Copy(bvs, 0, newbvs, 0, idx);
                    newbvs[idx].bucket = hb;
                    newbvs[idx].count = count;
                    newbvs[idx].bucket = hb;
                    newbvs[idx].count = count;
                    if (idx < used)
                        Array.Copy(bvs, idx, newbvs, idx + 1, used - idx);
                    bvs = newbvs;
                    allocd += DEFAULT_HIST_SIZE;
                }
                else
                { // used !== alloced
                  /* We need to shuffle out data to poke the new one in */
                    Array.Copy(bvs, idx, bvs, idx + 1, used - idx);
                    bvs[idx].bucket = hb;
                    bvs[idx].count = count;
                }
                used++;
            }
            else
            { // found
              /* Just need to update the counters */
                ulong newval = bvs[idx].count + count;
                if (newval < bvs[idx].count) /* we rolled */
                    newval = ulong.MaxValue;
                count = newval - bvs[idx].count;
                bvs[idx].count = newval;
            }
            return count;
        }
        public ulong SampleCount()
        {
            ulong total = 0, last = 0;
            if (bvs == null) return 0;
            for (int i = 0; i < used; i++)
            {
                last = total;
                total += bvs[i].count;
                if (total < last) return ulong.MaxValue;
            }
            return total;
        }
        public byte[] ToRaw()
        {
            MemoryStream mem = new MemoryStream();
            mem.WriteByte(0);
            mem.WriteByte(0);
            ushort len = 0;

            for (int i = 0; i < used; i++)
            {
                if (bvs[i].count > 0)
                {
                    bvs[i].Write(mem);
                    len++;
                }
            }

            mem.Seek(0, SeekOrigin.Begin);
            mem.WriteByte((byte)((len >> 8) & 0xff));
            mem.WriteByte((byte)(len & 0xff));
            return mem.ToArray();
        }
        public int CompareTo(Histogram o)
        {
            if (this == o) return 0;
            byte[] rep = ToRaw();
            byte[] orep = o.ToRaw();
            if (rep.Length < orep.Length) return -1;
            if (rep.Length > orep.Length) return 1;
            for(int i=0; i<rep.Length; i++)
            {
                if (rep[i] < orep[i]) return -1;
                if (rep[i] > orep[i]) return 1;
            }
            return 0;
        }
        public string ToBase64String()
        {
            byte[] buf = ToRaw();
            return System.Convert.ToBase64String(buf);
        }
        public void Merge(Histogram o)
        {
            for (int i = 0; i < o.used; i++)
                Insert(o.bvs[i].bucket, o.bvs[i].count);
        }
    }
}
