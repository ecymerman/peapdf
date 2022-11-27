using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SeaPeaYou.PeaPdf.W
{
    internal class DocID
    {
        public DocID(PdfArray arr)
        {
            if (arr.Count != 2)
                throw new Exception("ID must have 2 parts");
            if (arr.Any(x => !(x is PdfString)))
                throw new Exception("ID must be strings");            
            Part1 = arr[0].As<PdfString>().Value;
            Part2 = arr[1].As<PdfString>().Value;
            Check();
        }

        public DocID(byte[] part1, byte[] part2)
        {
            Part1 = part1;
            Part2 = part2;
            Check();
        }

        public PdfArray ToPdfArray() => new PdfArray(new PdfString(Part1), new PdfString(Part2));

        void Check()
        {
            if (Part1.Length < 16 || Part2.Length < 16)
                throw new Exception("ID parts must be >= 16 bytes");
        }

        public byte[] Part1 { get; }
        public byte[] Part2 { get; }

    }
}
