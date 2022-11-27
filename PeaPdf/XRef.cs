/*
 * Copyright 2021 Elliott Cymerman
 * SPDX-License-Identifier: Apache-2.0
 */

using SeaPeaYou.PeaPdf.Filters;
using SeaPeaYou.PeaPdf.W;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SeaPeaYou.PeaPdf
{
    abstract class XRef
    {
        public readonly int NextObjNum;
        public readonly bool IsStream;
        public readonly int Offset;
        public abstract PdfDict Dict { get; }

        public static XRef Read(PdfReader r)
        {
            var isStream = !r.ReadString("xref");
            if (isStream)
                return new XRefStream(r);
            else
                return new XRefTable(r);
        }

        protected XRef(PdfReader r)
        {
            Offset = r.Pos;
        }

        protected XRef(int offset) => Offset = offset;

        protected List<Section> GetSections(List<XRefInUseEntry> entries)
        {
            entries.Sort((a, b) => a.ObjID.ObjNum.CompareTo(b.ObjID.ObjNum));
            var sections = new List<Section>();
            for (int i = 0; i < entries.Count; i++)
            {
                int startNum = entries[i].ObjID.ObjNum, count = 0, nextIX;
                do
                {
                    count++;
                    nextIX = i + count;
                } while (nextIX < entries.Count && entries[nextIX].ObjID.ObjNum == startNum + count);
                var section = new Section(startNum, count, entries.Skip(i).Take(count).Select(x => (x.Offset, x.ObjID.GenNum)).ToList());
                sections.Add(section);
                i = nextIX - 1;
            }
            return sections;
        }

        public abstract void Write(PdfWriter w);

        public abstract XRefEntry GetObjOffset(ObjID objID);

        public class XRefTable : XRef
        {
            public override PdfDict Dict => dict;
            readonly List<TableSection> tableSections = new List<TableSection>();
            readonly XRefTable prevXRef;
            readonly PdfDict dict;

            public XRefTable(PdfReader r) : base(r)
            {
                r.ReadString("xref");
                while (true)
                {
                    r.SkipWhiteSpace();
                    var twoNums = TwoNums.TryRead(r, null);
                    if (twoNums == null)
                        break;
                    r.SkipWhiteSpace();
                    var section = new TableSection(twoNums.Num1, twoNums.Num2, r);
                    tableSections.Add(section);
                }

                if (!r.ReadString("trailer"))
                    throw new FormatException();
                r.SkipWhiteSpace();
                dict = new PdfDict(r, null);
                var prev = (int?)dict["Prev"];
                if (prev != null)
                {
                    prevXRef = new XRefTable(r.Clone(prev));
                }
                else
                {
                    //a single xref must start from 0, yet sometimes it pretends it doesn't
                    tableSections[0].EndObjNum -= tableSections[0].StartObjNum;
                    tableSections[0].StartObjNum = 0;
                }

            }

            public XRefTable(List<XRefInUseEntry> entries, int offset, Catalog root, PdfDict encrypt, PdfDict info, DocID docID) : base(offset)
            {
                var section = GetSections(entries).Single();
                var tableSection = new TableSection(section, true);
                tableSections.Add(tableSection);
                dict = new PdfDict
                {
                    { "Root", root.Dict },
                    { "Size", (PdfNumeric)(entries.Count + 1) },
                    { "Encrypt", encrypt },
                    { "Info", info },
                    { "ID", docID.ToPdfArray() },
                };
            }

            public override void Write(PdfWriter w)
            {
                var startxref = w.Count;
                w.WriteString("xref");
                w.WriteNewLine();
                foreach (var ts in tableSections)
                {
                    ts.Write(w);
                }
                w.WriteString("trailer");
                w.WriteNewLine();
                w.WriteObj(dict, null, true);
                w.WriteNewLine();
                w.WriteString("startxref");
                w.WriteNewLine();
                w.WriteString(startxref.ToString());
                w.WriteNewLine();
                w.WriteString("%%EOF");
            }

            public override XRefEntry GetObjOffset(ObjID objID)
            {
                foreach (var section in tableSections)
                {
                    if (section.ContainsObjNum(objID.ObjNum))
                    {
                        var entry = section.GetEntry(objID.ObjNum);
                        return entry;
                    }
                }
                if (prevXRef != null)
                    return prevXRef.GetObjOffset(objID);
                return null;
            }

            class TableSection
            {
                public int StartObjNum, EndObjNum, Size;

                byte[] bytes;
                IEnumerable<(int offset, int genNum)> lines;

                public TableSection(int startObjNum, int size, PdfReader r)
                {
                    StartObjNum = startObjNum;
                    Size = size;
                    EndObjNum = StartObjNum + Size;
                    bytes = r.ReadByteArray(20 * size);
                }

                public TableSection(Section section, bool isFirst = false)
                {
                    StartObjNum = section.StartObjNum;
                    Size = section.Size;
                    EndObjNum = StartObjNum + section.Size;
                    lines = section.Entries;
                    var ms = new MemoryStream();
                    if (isFirst)
                    {
                        ms.WriteString("0000000000 65535 f \n");
                        StartObjNum--;
                        Size++;
                    }
                    foreach (var line in lines)
                    {
                        ms.WriteString(line.offset.ToString("d10"));
                        ms.WriteByte((byte)' ');
                        ms.WriteString(line.genNum.ToString("d5"));
                        ms.WriteByte((byte)' ');
                        ms.WriteByte((byte)'n');
                        ms.WriteByte((byte)' ');
                        ms.WriteByte((byte)'\n');
                    }
                    bytes = ms.ToArray();
                }

                public XRefEntry GetEntry(int objNum)
                {
                    var byteIX = (objNum - StartObjNum) * 20;
                    var entry = new XRefEntry(
                        GetString(byteIX + 17, 1) == "n" ? XRefEntryType.InUse : XRefEntryType.Free,
                        int.Parse(GetString(byteIX, 10)),
                        int.Parse(GetString(byteIX + 11, 5))
                    );
                    return entry;
                }

                public bool ContainsObjNum(int objNum) => objNum >= StartObjNum && objNum < EndObjNum;

                public string GetString(int byteIX, int count)
                {
                    var sb = new StringBuilder();
                    for (var i = 0; i < count; i++)
                        sb.Append((char)bytes[byteIX + i]);
                    return sb.ToString();
                }

                public void Write(PdfWriter w)
                {
                    var twoNums = new TwoNums(StartObjNum, Size, null);
                    twoNums.Write(w);
                    w.WriteNewLine();
                    w.WriteBytes(bytes);
                }

            }

        }

        public class XRefStream : XRef
        {
            public readonly PdfStream PdfStream;
            public override PdfDict Dict => dict;
            readonly IList<(int startObjNum, int size)> streamSections;
            readonly List<XRefEntry> streamEntries = new List<XRefEntry>();
            readonly XRefStream prevXRef;
            readonly PdfDict dict;
            readonly byte[] bytes;
            readonly int size;

            public XRefStream(PdfReader r) : base(r)
            {
                var iRef = r.ReadObjHeader(null);
                dict = new PdfDict(r, null);
                size = (int)dict["Size"];
                int? prev = (int?)dict["Prev"];
                streamSections = dict["Index"]?.AsPairs<PdfNumeric>().Select(x => ((int)x.Item1, (int)x.Item2)).ToList();
                if (streamSections == null)
                    streamSections = new List<(int, int)> { (0, size) };
                r.SkipWhiteSpace();
                PdfStream = new PdfStream(dict, r, null/*xref is not encrypted*/);
                bytes = PdfStream.GetDecodedBytes();
                var byteReader = new ByteReader(bytes);
                var totalEntries = streamSections.Sum(x => x.size);
                var w = (PdfArray)dict["W"];
                var streamWidths = ((int)w[0], (int)w[1], (int)w[2]);
                for (int i = 0; i < totalEntries; i++)
                {
                    var entry = new XRefEntry((XRefEntryType)byteReader.ReadBytes(streamWidths.Item1), byteReader.ReadBytes(streamWidths.Item2), byteReader.ReadBytes(streamWidths.Item3));
                    streamEntries.Add(entry);
                }

                if (prev != null)
                {
                    prevXRef = new XRefStream(r.Clone(prev));
                }

            }

            //XRefStream(List<XRefInUseEntry> entries, int offset, XRefStream prevXRef, Catalog root, PdfDict encrypt, PdfDict info, DocID docID) : base(offset)
            //{
            //    var nextObjNum = Math.Max(entries.Max(x => x.ObjID.ObjNum) + 1, prevXRef.size);
            //    entries.Add(new XRefInUseEntry(offset, new ObjID(nextObjNum, 0))); //for xref object
            //    var sections = GetSections(entries);
            //    if (sections.Count > 0)
            //    {
            //        var lastSection = sections[sections.Count - 1];
            //    }

            //    var ms = new MemoryStream();
            //    streamSections = new List<(int, int)>();
            //    foreach (var section in sections)
            //    {
            //        streamSections.Add((section.StartObjNum, section.Size));
            //        foreach (var entry in section.Entries)
            //        {
            //            streamEntries.Add(new XRefEntry(XRefEntryType.InUse, entry.offset, entry.genNum));
            //            ms.WriteByte(1);
            //            ms.WriteByte((byte)(entry.offset >> 24));
            //            ms.WriteByte((byte)(entry.offset >> 16));
            //            ms.WriteByte((byte)(entry.offset >> 8));
            //            ms.WriteByte((byte)(entry.offset >> 0));
            //            ms.WriteByte((byte)entry.genNum);
            //        }
            //    }
            //    bytes = ms.ToArray();

            //    this.prevXRef = prevXRef;
            //    int size = nextObjNum + 1;
            //    if (sections.Count > 0)
            //    {
            //        var lastSection = sections[sections.Count - 1];
            //        size = Math.Max(prevXRef.size, lastSection.StartObjNum + lastSection.Size);
            //    }

            //    PdfStream = new FileTrailerStream(root, encrypt, info, docID, sections.Select(x => (x.StartObjNum, x.Size)).ToList(), (1, 4, 1), size, prevXRef.Offset);
            //}

            public XRefStream(List<XRefEntry> entries, int offset, Catalog root, PdfDict encrypt, PdfDict info, DocID docID) : base(offset)
            {
                var w = new ByteWriter();
                foreach (var entry in entries)
                {
                    w.WriteByte((byte)entry.Type);
                    w.WriteInt(entry.Offset);
                    w.WriteByte((byte)entry.GenNum);
                }
                bytes = w.ToArray();
                dict = new PdfDict()
                {
                    { "Root", root.Dict },
                    { "Size", (PdfNumeric)(entries.Count + 1) },
                    { "Encrypt", encrypt },
                    { "Info", info },
                    { "Index", new PdfArray((PdfNumeric)1, (PdfNumeric)entries.Count) },
                    { "ID", docID?.ToPdfArray() },
                    { "W", new PdfArray((PdfNumeric)1, (PdfNumeric)4, (PdfNumeric)1) },
                };
                dict.Type = "XRef";
                PdfStream = new PdfStream(bytes, "FlateDecode", dict);
            }

            public override void Write(PdfWriter w)
            {
                //var pdfStream = new PdfStream(bytes, trailerDict.PdfDict);
                //pdfStream.ObjID = new ObjID(trailerDict.Size - 1, 0);
                PdfStream.Write(w, null);
            }

            public override XRefEntry GetObjOffset(ObjID objID)
            {
                var cSoFar = 0;
                foreach (var section in streamSections)
                {
                    if (objID.ObjNum >= section.startObjNum && objID.ObjNum < (section.startObjNum + section.size))
                    {
                        var entry = streamEntries[cSoFar + (objID.ObjNum - section.startObjNum)];
                        return entry;
                    }
                    cSoFar += section.size;
                }
                if (prevXRef != null)
                    return prevXRef.GetObjOffset(objID);
                return null;
            }

        }

        protected class Section
        {
            public int StartObjNum, Size;
            public IList<(int offset, int genNum)> Entries;
            public Section(int startNum, int count, IList<(int offset, int genNum)> entries)
            {
                this.StartObjNum = startNum; this.Size = count; this.Entries = entries;
            }
        }
    }

    enum XRefEntryType { Free, InUse, Compressed }

    class XRefEntry
    {
        public readonly XRefEntryType Type;
        public readonly int Offset, GenNum;
        //for Free: Offset=objNum of next free object
        //for Compressed: Offset=objNum of objStream, GenNum=index in objStream

        public XRefEntry(XRefEntryType type, int offset, int genNum)
        {
            Type = type;
            Offset = offset;
            GenNum = genNum;
        }
    }

    class XRefInUseEntry
    {
        public readonly int Offset;
        public readonly ObjID ObjID;

        public XRefInUseEntry(int offset, ObjID objID) => (Offset, ObjID) = (offset, objID);
    }
}
