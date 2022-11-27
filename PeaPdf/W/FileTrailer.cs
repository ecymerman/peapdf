/*
 * Copyright 2021 Elliott Cymerman
 * SPDX-License-Identifier: Apache-2.0
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeaPeaYou.PeaPdf.W
{
    class FileTrailer
    {

        public FileTrailer(PdfDict dict, int versionMajor)
        {
            Root = new Catalog((PdfDict)dict["Root"]);
            Encrypt = (PdfDict)dict["Encrypt"];
            Info = (PdfDict)dict["Info"];
            var idArr = (PdfArray)dict["ID"];
            if (idArr == null && versionMajor >= 2)
                throw new Exception("Version 2+ needs ID");
            if (idArr != null)
                DocID = new DocID(idArr);
        }
        public FileTrailer(Catalog root, PdfDict encrypt, PdfDict info, DocID id, int size, int? prev)
        {
            Root = root;
            Encrypt = encrypt;
            Info = info;
            DocID = id;
            Size = size;
            Prev = prev;
        }

        public int Size { get; }
        public int? Prev { get; }
        public Catalog Root { get; }
        public PdfDict Encrypt { get; }
        public PdfDict Info { get; }
        public DocID DocID { get; }

        public virtual PdfDict ToDict()
        {
            var dict = new PdfDict
            {
                { "Root", Root.Dict },
                { "Prev", (PdfNumeric)Prev },
                { "Size", (PdfNumeric)Size },
                { "Encrypt", Encrypt },
                { "Info", Info },
            };
            if (DocID != null)
                dict["ID"] = new PdfArray((PdfString)DocID.Part1, (PdfString)DocID.Part2);
            return dict;
        }
    }

    class FileTrailerStream : FileTrailer
    {
        public IList<(int objNum, int count)> Index { get; }
        public (int, int, int) W { get; }

        public readonly FileTrailer FileTrailer;

        public FileTrailerStream(PdfDict dict, int versionMajor) : base(dict, versionMajor)
        {
            var w = (PdfArray)dict["W"];
            W = ((int)w[0], (int)w[1], (int)w[2]);
            Index = dict["Index"]?.AsPairs<PdfNumeric>().Select(x => ((int)x.Item1, (int)x.Item2)).ToList();
        }

        public FileTrailerStream(Catalog root, PdfDict encrypt, PdfDict info, DocID docID, IList<(int objNum, int count)> index, (int, int, int) w, int size, int? prev)
            : base(root, encrypt, info, docID, size, prev)
        {
            Index = index;
            W = w;
        }

        public override PdfDict ToDict()
        {
            var dict = base.ToDict();
            dict.Type = "XRef";
            dict["Index"] = new PdfArray(Index.SelectMany(x => new[] { x.objNum, x.count }).Select(x => (PdfNumeric)x).ToArray());
            dict["W"] = new PdfArray((PdfNumeric)W.Item1, (PdfNumeric)W.Item2, (PdfNumeric)W.Item3);
            return dict;
        }



        //public IList<(int objNum, int count)> Index
        //{
        //    get => Dict["Index"]?.AsPairs<PdfNumeric>().Select(x => ((int)x.Item1, (int)x.Item2)).ToList();
        //    set => Dict["Index"] = value == null ? null : new PdfArray(value.SelectMany(x => new[] { (PdfNumeric)x.objNum, (PdfNumeric)x.count }).ToArray());
        //}

        //public (int, int, int) W
        //{
        //    get
        //    {
        //        var w = (PdfArray)Dict["W"];
        //        return ((int)w[0], (int)w[1], (int)w[2]);
        //    }
        //    set
        //    {
        //        Dict["W"] = new PdfArray((PdfNumeric)value.Item1, (PdfNumeric)value.Item2, (PdfNumeric)value.Item3);
        //    }
        //}

    }


}
