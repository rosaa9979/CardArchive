using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TcgEngine.Tools
{
    /// <summary>
    /// Minimal OOXML (.xlsx) writer.  No external dependencies — uses only
    /// System.IO.Compression and string-based XML.  Multiple sheets per file,
    /// inline strings, no shared-strings table, no styles.
    ///
    /// Each sheet stores its content as a row-major grid of cells (object?).
    /// Empty rows are allowed (empty list).
    /// </summary>
    public static class SimpleXlsx
    {
        public class Sheet
        {
            public string Name;
            // Row-major grid.  Each row is a list of cell values
            // (string, int, long, float, double, bool, or null).
            public IList<IList<object>> Rows;
            // Number of top rows to freeze.  0 = none, 1 = header, 2 = header + type row, ...
            public int FreezeRows;
        }

        // Wrap a cell value to attach a style index (see BuildStylesXml).
        // Style 0 = default, 1 = header (bold text + light-blue fill).
        public class StyledCell
        {
            public object Value;
            public int Style;
        }

        // Convenience style index for header rows.
        public const int HeaderStyle = 1;

        public static void Write(string path, IList<Sheet> sheets)
        {
            if (sheets == null || sheets.Count == 0)
                throw new System.ArgumentException("Need at least one sheet", nameof(sheets));

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path))
                File.Delete(path);

            using (var stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml", BuildContentTypes(sheets.Count));
                WriteEntry(zip, "_rels/.rels", RootRels);
                WriteEntry(zip, "xl/workbook.xml", BuildWorkbookXml(sheets));
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(sheets.Count));
                WriteEntry(zip, "xl/styles.xml", BuildStylesXml());

                for (int i = 0; i < sheets.Count; i++)
                {
                    string entryPath = "xl/worksheets/sheet" + (i + 1) + ".xml";
                    WriteEntry(zip, entryPath, BuildSheetXml(sheets[i]));
                }
            }
        }

        // -----------------------------------------------------------------
        // Worksheet body
        // -----------------------------------------------------------------
        static string BuildSheetXml(Sheet sheet)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            // OOXML schema requires sheetViews BEFORE sheetData.
            if (sheet.FreezeRows > 0)
            {
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
                sb.Append("<pane ySplit=\"").Append(sheet.FreezeRows)
                  .Append("\" topLeftCell=\"A").Append(sheet.FreezeRows + 1)
                  .Append("\" activePane=\"bottomLeft\" state=\"frozen\"/>");
                sb.Append("</sheetView></sheetViews>");
            }

            sb.Append("<sheetData>");

            var rows = sheet.Rows ?? new IList<object>[0];
            for (int r = 0; r < rows.Count; r++)
            {
                int rowNum = r + 1;
                var row = rows[r];
                if (row == null || row.Count == 0)
                {
                    // empty row — emit a self-closing <row r=".."/> for traceability
                    sb.Append("<row r=\"").Append(rowNum).Append("\"/>");
                    continue;
                }
                sb.Append("<row r=\"").Append(rowNum).Append("\">");
                for (int c = 0; c < row.Count; c++)
                {
                    AppendCell(sb, ColumnLetter(c + 1), rowNum, row[c]);
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        static void AppendCell(StringBuilder sb, string col, int row, object val)
        {
            int style = 0;
            if (val is StyledCell sc) { style = sc.Style; val = sc.Value; }

            sb.Append("<c r=\"").Append(col).Append(row).Append('"');
            if (style != 0) sb.Append(" s=\"").Append(style).Append('"');

            if (val == null)
            {
                sb.Append("/>");
                return;
            }

            if (val is int || val is long || val is short || val is byte || val is uint || val is ulong || val is ushort || val is sbyte)
            {
                sb.Append("><v>").Append(val).Append("</v></c>");
                return;
            }
            if (val is float f)
            {
                sb.Append("><v>").Append(f.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                return;
            }
            if (val is double d)
            {
                sb.Append("><v>").Append(d.ToString("R", CultureInfo.InvariantCulture)).Append("</v></c>");
                return;
            }
            if (val is bool b)
            {
                sb.Append(" t=\"b\"><v>").Append(b ? 1 : 0).Append("</v></c>");
                return;
            }

            string s = val.ToString();
            if (string.IsNullOrEmpty(s))
            {
                sb.Append("/>");
                return;
            }
            sb.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
            AppendEscaped(sb, s);
            sb.Append("</t></is></c>");
        }

        static void AppendEscaped(StringBuilder sb, string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                switch (ch)
                {
                    case '&':  sb.Append("&amp;"); break;
                    case '<':  sb.Append("&lt;");  break;
                    case '>':  sb.Append("&gt;");  break;
                    case '"':  sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default:
                        if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') break;
                        sb.Append(ch); break;
                }
            }
        }

        static string ColumnLetter(int col)
        {
            string s = string.Empty;
            while (col > 0)
            {
                int rem = (col - 1) % 26;
                s = (char)('A' + rem) + s;
                col = (col - 1) / 26;
            }
            return s;
        }

        // -----------------------------------------------------------------
        // OOXML envelope
        // -----------------------------------------------------------------
        static string BuildContentTypes(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= sheetCount; i++)
            {
                sb.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i)
                  .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            sb.Append("</Types>");
            return sb.ToString();
        }

        const string RootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        static string BuildWorkbookRels(int sheetCount)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 1; i <= sheetCount; i++)
            {
                sb.Append("<Relationship Id=\"rId").Append(i)
                  .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                  .Append(i).Append(".xml\"/>");
            }
            // styles part (rId right after the worksheets)
            sb.Append("<Relationship Id=\"rId").Append(sheetCount + 1)
              .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        // Minimal style table: one default cell format (0) and one header
        // format (1) = bold font + solid light-blue fill. Fill ids 0/1 are the
        // OOXML-reserved 'none'/'gray125'; the custom fill is id 2.
        static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<fonts count=\"2\"><font/><font><b/></font></fonts>"
                + "<fills count=\"3\">"
                  + "<fill><patternFill patternType=\"none\"/></fill>"
                  + "<fill><patternFill patternType=\"gray125\"/></fill>"
                  + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFBDD7EE\"/><bgColor indexed=\"64\"/></patternFill></fill>"
                + "</fills>"
                + "<borders count=\"1\"><border/></borders>"
                + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                + "<cellXfs count=\"2\">"
                  + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
                  + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>"
                + "</cellXfs>"
                + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
                + "</styleSheet>";
        }

        static string BuildWorkbookXml(IList<Sheet> sheets)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheets>");
            for (int i = 0; i < sheets.Count; i++)
            {
                string name = SafeSheetName(string.IsNullOrEmpty(sheets[i].Name) ? "Sheet" + (i + 1) : sheets[i].Name);
                sb.Append("<sheet name=\"");
                AppendEscaped(sb, name);
                sb.Append("\" sheetId=\"").Append(i + 1)
                  .Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
            }
            sb.Append("</sheets></workbook>");
            return sb.ToString();
        }

        static string SafeSheetName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Sheet1";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '[' || ch == ']' || ch == ':' || ch == '*' || ch == '?' || ch == '/' || ch == '\\')
                    sb.Append('_');
                else
                    sb.Append(ch);
            }
            string r = sb.ToString();
            if (r.Length > 31) r = r.Substring(0, 31);
            return r;
        }

        // -----------------------------------------------------------------
        static void WriteEntry(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }
    }
}
