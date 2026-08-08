using System;
using System.Collections.Generic;
using System.Reflection;

class ChunkTest
{
    static int failures = 0;

    static void Check(bool cond, string name)
    {
        if (cond) { Console.WriteLine("PASS: " + name); }
        else { Console.WriteLine("FAIL: " + name); failures++; }
    }

    static List<string> Split(string text)
    {
        // Reflect into the real compiled exe
        Assembly asm = Assembly.LoadFrom("ClipReader/bin/Release/ClipReader.exe");
        Type form1 = asm.GetType("ClipReader.Form1");
        MethodInfo m = form1.GetMethod("SplitIntoChunks", BindingFlags.NonPublic | BindingFlags.Static);
        return (List<string>)m.Invoke(null, new object[] { text });
    }

    static void Main()
    {
        // 1. short text -> single chunk, identical
        string shortText = "Hello world, this is a short test.";
        var c1 = Split(shortText);
        Check(c1.Count == 1 && c1[0] == shortText, "short text returns one identical chunk");

        // 2. null/empty -> no chunks
        Check(Split(null).Count == 0, "null returns no chunks");
        Check(Split("").Count == 0, "empty returns no chunks");

        // 3. long text with sentence boundaries
        var sb = new System.Text.StringBuilder();
        string sentence = "This is sentence number %d in a very long article that we are testing. ";
        for (int i = 0; i < 300; i++) sb.Append(string.Format(sentence, i));
        string longText = sb.ToString(); // ~24,600 chars
        var c3 = Split(longText);
        int maxLen = 0, total = 0;
        bool allSub = true;
        int idx = 0;
        foreach (var ch in c3)
        {
            if (ch.Length > maxLen) maxLen = ch.Length;
            total += ch.Length;
            if (ch.Length == 0) allSub = false;
            // subsequence check: chars appear in order in the original
            int found = longText.IndexOf(ch, idx);
            if (found < 0) allSub = false;
            else idx = found + ch.Length;
        }
        Check(c3.Count > 5, "long text splits into multiple chunks (got " + c3.Count + ")");
        Check(maxLen <= 4000, "no chunk exceeds 4000 chars (max " + maxLen + ")");
        Check(allSub, "all chunks are exact in-order substrings of the original");
        Check(total > 20000, "most text preserved across chunks (" + total + " of " + longText.Length + ")");

        // 4. long text with NO boundary chars -> hard breaks, none exceeding 4000
        string noBoundary = new string('a', 12345); // no punctuation at all
        var c4 = Split(noBoundary);
        int max4 = 0;
        foreach (var ch in c4) { if (ch.Length > max4) max4 = ch.Length; }
        Check(c4.Count == 4 && max4 == 4000, "no-boundary text hard-breaks at 4000 (chunks=" + c4.Count + ", max=" + max4 + ")");
        int total4 = 0; foreach (var ch in c4) total4 += ch.Length;
        Check(total4 == 12345, "no-boundary text fully preserved (" + total4 + ")");

        // 5. markup-heavy text is preserved literally (XML trap)
        string markup = "<html><body>AT&T says 5 < 6 and 10 > 3 & so on.</body></html>";
        var c5 = Split(markup);
        Check(c5.Count == 1 && c5[0] == markup, "markup text preserved literally, unparsed");

        // 6. chunk boundary lands exactly on max with a boundary char at the edge
        //    text of 4001 chars where char 4000 is a '.'
        string edge = new string('x', 3999) + ".y";
        var c6 = Split(edge);
        Check(c6.Count == 2 && c6[0].Length == 4000 && c6[0].EndsWith(".") && c6[1] == "y",
              "boundary at window edge splits cleanly (chunks=" + c6.Count + ", c0=" + c6[0].Length + ")");

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
