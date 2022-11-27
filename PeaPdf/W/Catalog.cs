/*
 * Copyright 2021 Elliott Cymerman
 * SPDX-License-Identifier: Apache-2.0
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace SeaPeaYou.PeaPdf.W
{
    class Catalog
    {
        public readonly PdfDict Dict;

        public Catalog(PdfDict dict)
        {
            Dict = dict;
        }

        public Catalog(string version)
        {
            Dict = new PdfDict { Type = "Catalog" };
            Version = version;
            Pages = new PageTreeNode(new List<Page>());
        }


        public string Version { get => Dict["Version"].As<PdfName>()?.String; set => Dict["Version"] = (PdfName)value; }

        public PdfDict Extensions { get => (PdfDict)Dict["Extensions"]; set => Dict["Extensions"] = value; }

        public PageTreeNode Pages
        {
            get => new PageTreeNode((PdfDict)Dict["Pages"]);
            set => Dict["Pages"] = value.PdfDict;
        }

        public AcroForm AcroForm { get => ((PdfDict)Dict["AcroForm"])?.To(x => new AcroForm(x)); }

    }
}
