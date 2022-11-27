/*
 * Copyright 2021 Elliott Cymerman
 * SPDX-License-Identifier: Apache-2.0
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using System.Diagnostics;

namespace SeaPeaYou.PeaPdf
{
    class PdfDict : PdfObject, IEnumerable<(string key, PdfObject value)>
    {

        readonly Dictionary<string, PdfObject> dict;
        readonly PdfReader r;

        internal PdfDict(PdfReader r, ObjID? baseObjID)
        {
            if (!r.ReadString("<<"))
                throw new FormatException();

            this.r = r;

            dict = new Dictionary<string, PdfObject>();
            r.SkipWhiteSpace();
            while (!r.ReadString(">>"))
            {
                var pdfName = new PdfName(r);
                r.SkipWhiteSpace();
                var val = r.ReadPdfObject(baseObjID);
                dict.Add(pdfName.String, val);
                r.SkipWhiteSpace();
            }
        }

        public PdfDict(Dictionary<string, PdfObject> dict)
        {
            this.dict = dict;
        }

        public PdfDict() { dict = new Dictionary<string, PdfObject>(); }

        public PdfDict(PdfDict cloneFrom)
        {
            dict = new Dictionary<string, PdfObject>();
            foreach (var keyVal in cloneFrom.dict)
            {
                dict.Add(keyVal.Key, Clone(keyVal.Value));
            }
        }

        public string Type
        {
            get => ((PdfName)this["Type"])?.String;
            set => this["Type"] = (PdfName)value;
        }

        public PdfObject this[string key]
        {
            get
            {
                if (dict.TryGetValue(key, out var pdfObject))
                {
                    if (pdfObject is PdfIndirectReference)
                    {
                        var val = r.Deref(pdfObject);
                        dict[key] = val;
                        return val;
                    }
                    return pdfObject;
                }
                return null;
            }
            set
            {
                if (value == null)
                    dict.Remove(key);
                else
                    dict[key] = value;
            }
        }
        public PdfObject this[PdfName key] { get => this[key.String]; set => this[key.String] = value; }

        public void Add(string key, PdfObject val)
        {
            if (val != null)
                dict.Add(key, val);
        }

        internal override void Write(PdfWriter w, ObjID? encryptionObjID)
        {
            w.Indent();
            w.WriteByte('<');
            w.WriteByte('<');
            if (w.IndentLevel != null)
            {
                w.WriteNewLine();
                w.IndentLevel++;
            }

            w.NeedsDeliminator = false;

            foreach (var (key, value) in this)
            {
                w.Indent();
                new PdfName(key).WriteThis(w);
                if (w.IndentLevel != null)
                {
                    w.WriteByte(' ');
                    w.NeedsDeliminator = false;
                }
                w.WriteObj(value, encryptionObjID, false);
                if (w.IndentLevel != null)
                    w.WriteNewLine();
            }

            if (w.IndentLevel != null)
                w.IndentLevel--;
            w.Indent();
            w.WriteByte('>');
            w.WriteByte('>');
            w.NeedsDeliminator = false;
        }

        public PdfDict CloneThis() => new PdfDict(this);
        public override PdfObject Clone() => CloneThis();

        public IEnumerator<(string key, PdfObject value)> GetEnumerator()
        {
            foreach (var keyVal in dict)
            {
                var val = keyVal.Value;
                yield return (keyVal.Key, r == null ? val : r.Deref(val));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }
}
