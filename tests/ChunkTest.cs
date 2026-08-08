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

    static void CheckChunks(string text, List<string> chunks, string label)
    {
        int maxLen = 0, total = 0;
        bool allSub = true, anyEmpty = false;
        int idx = 0;
        foreach (var ch in chunks)
        {
            if (ch.Length > maxLen) maxLen = ch.Length;
            total += ch.Length;
            if (ch.Length == 0) anyEmpty = true;
            int found = text.IndexOf(ch, idx);
            if (found < 0) allSub = false;
            else idx = found + ch.Length;
        }
        Check(maxLen <= 4000, label + ": no chunk exceeds 4000 chars (max " + maxLen + ")");
        Check(allSub, label + ": all chunks are exact in-order substrings of the original");
        Check(!anyEmpty, label + ": no empty chunks");
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

        // 7. AT the limit: exactly 4000 chars, no boundary -> one single chunk
        string atLimit = new string('b', 4000);
        var c7 = Split(atLimit);
        Check(c7.Count == 1 && c7[0].Length == 4000 && c7[0] == atLimit,
              "exactly 4000 chars stays one chunk (chunks=" + c7.Count + ")");

        // 8. BEYOND the limit by one: 4001 chars, no boundary -> 4000 + 1
        string justOver = new string('c', 4001);
        var c8 = Split(justOver);
        Check(c8.Count == 2 && c8[0].Length == 4000 && c8[1].Length == 1,
              "4001 chars hard-breaks into 4000+1 (chunks=" + c8.Count + ", c0=" + c8[0].Length + ", c1=" + c8[1].Length + ")");
        int total8 = 0; foreach (var ch in c8) total8 += ch.Length;
        Check(total8 == 4001, "4001 chars fully preserved (" + total8 + ")");

        // 9. AT the limit with a sentence boundary at the window edge:
        //    '.' is the last char of the 4000-char window => 4000-char chunk ending in '.'
        string edgeAtLimit = new string('d', 3999) + "." + "e";
        var c9 = Split(edgeAtLimit);
        Check(c9.Count == 2 && c9[0].Length == 4000 && c9[0].EndsWith(".") && c9[1] == "e",
              "sentence boundary exactly at 4000 splits cleanly (chunks=" + c9.Count + ", c0=" + c9[0].Length + ")");

        // 10. CJK punctuation boundaries (。！？) are respected
        var sb10 = new System.Text.StringBuilder();
        for (int i = 0; i < 1500; i++) sb10.Append("这是用来测试朗读分段的第" + i + "个句子。");
        string cjk = sb10.ToString(); // ~25k chars, all sentences end in 。
        var c10 = Split(cjk);
        CheckChunks(cjk, c10, "CJK punctuation");
        Check(c10.Count > 5, "CJK text splits into multiple chunks (got " + c10.Count + ")");
        bool cjkClean = true;
        foreach (var ch in c10)
        {
            if (ch.Length < 4000 && !ch.TrimEnd().EndsWith("。") && !ch.TrimEnd().EndsWith("！") && !ch.TrimEnd().EndsWith("？"))
                cjkClean = false;
        }
        Check(cjkClean, "CJK chunks (except full-size hard breaks) end on sentence punctuation");

        // 11. very long text (200k chars) -> all chunks <= 4000, everything preserved.
        //     Paragraphs end with '.' and the following char is non-whitespace, so
        //     nothing is skipped and the chunked text equals the original exactly.
        var sb11 = new System.Text.StringBuilder();
        string para = "The quick brown fox jumps over the lazy dog, and the whole thing keeps going.";
        while (sb11.Length < 200000) sb11.Append(para);
        string big = sb11.ToString();
        var c11 = Split(big);
        CheckChunks(big, c11, "200k-char text");
        int total11 = 0; foreach (var ch in c11) total11 += ch.Length;
        Check(total11 == big.Length, "200k-char text fully preserved (" + total11 + " of " + big.Length + ")");
        Check(c11.Count > 40 && c11.Count < 60,
              "200k-char text chunk count plausible (got " + c11.Count + ")");

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
