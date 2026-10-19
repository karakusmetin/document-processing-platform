using System.Text;

namespace DocumentProcessing.Converters.Text.Rendering;

// Best-effort RFC4180-benzeri parser: tırnaklı alanlar, kaçışlı çift tırnak ve alan içi virgül/satır sonu destekler.
// Bozuk/dengesiz tırnak durumunda sert hata vermez, elinden geleni parse eder.
internal static class CsvParser
{
    public static List<string[]> Parse(string content)
    {
        List<string[]> rows = [];
        List<string> currentRow = [];
        StringBuilder field = new();
        bool inQuotes = false;
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    i++;
                    break;
                case '\r':
                    i++;
                    break;
                case '\n':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add([.. currentRow]);
                    currentRow = [];
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            rows.Add([.. currentRow]);
        }

        return rows;
    }
}
